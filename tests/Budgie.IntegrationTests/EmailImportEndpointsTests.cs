using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using Budgie.Modules.EmailImport.Application.Abstractions;
using Budgie.Modules.EmailImport.Application.Connections;
using Budgie.Modules.EmailImport.Domain.Connections;
using Budgie.Modules.EmailImport.Domain.Parsing;
using Budgie.Modules.Finances.Application;
using Budgie.Shared.Enums;
using Dapper;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Npgsql;
using Xunit;

namespace Budgie.IntegrationTests;

/// <summary>
/// End-to-end tests for the EmailImport module against a real PostgreSQL (Testcontainers).
/// Three things are proven here that unit tests cannot reach:
/// <list type="number">
/// <item>the DOUBLE idempotency barrier against the real unique indexes;</item>
/// <item>
/// the OAuth callback end to end, with a fake <see cref="IEmailProviderClient"/> — the
/// <c>state</c> is the only authentication of that endpoint, so it is the only way to
/// cover it without a real provider;
/// </item>
/// <item>multi-tenancy at the SQL level, not just at the repository signature.</item>
/// </list>
/// The worker is explicitly disabled in <see cref="BudgieWebApplicationFactory"/>, so
/// <c>POST /sync</c> owns the schedule and no timer races these tests.
/// </summary>
public class EmailImportEndpointsTests : IClassFixture<BudgieWebApplicationFactory>
{
    private const string FixtureEmail = "Fixtures/email-sample.es.txt";
    private const string FixtureRules = "Fixtures/email-import-rules.json";

    private readonly BudgieWebApplicationFactory _factory;

    public EmailImportEndpointsTests(BudgieWebApplicationFactory factory)
    {
        _factory = factory;
        File.Exists(FixturePath(FixtureEmail)).Should().BeTrue("the MIME fixture must be copied to output");
        File.Exists(FixturePath(FixtureRules)).Should().BeTrue("the rules fixture must be copied to output");
    }

    private static string FixturePath(string relative) =>
        Path.Combine(AppContext.BaseDirectory, relative.Replace('/', Path.DirectorySeparatorChar));

    private static string ReadFixture(string relative) => File.ReadAllText(FixturePath(relative));

    // ── Providers are faked; the state flow is the real thing ──────────────────────────
    private sealed class StubEmailProviderClient : IEmailProviderClient
    {
        public EmailProvider Provider => EmailProvider.Gmail;
        public string? LastState { get; private set; }
        public List<EmailMessage> Messages { get; } = [];
        public string? CursorToEstablish { get; set; }

        /// <summary>The clear-text refresh token the "provider" hands over. It must never
        /// reach the database in this form.</summary>
        public const string ClearTextRefreshToken = "1//0eRefreshTokenEnClaroQueNoDebePersistir";

        public Task<EmailAuthorizationUri> BuildAuthorizationUriAsync(
            EmailConnection connection, string redirectUri, string state, CancellationToken ct)
        {
            LastState = state;
            return Task.FromResult(new EmailAuthorizationUri(
                $"https://accounts.google.test/o/oauth2/auth?state={Uri.EscapeDataString(state)}", state));
        }

        public Task<EmailCredentials> ExchangeCodeAsync(
            string encryptedCredentials, string code, string redirectUri, CancellationToken ct) =>
            Task.FromResult(new EmailCredentials(
                "ya29.access-token-en-claro",
                // The provider's own protected envelope, persisted VERBATIM: re-encrypting
                // here would produce a double envelope nobody could decrypt.
                $"v1:bm9uY2UtcHJvdmllZGVy:{Convert.ToBase64String(
                    System.Text.Encoding.UTF8.GetBytes(ClearTextRefreshToken))}:dGFn",
                DateTimeOffset.UtcNow.AddHours(1)));

        public Task<EmailCredentials> RefreshAsync(string encryptedCredentials, CancellationToken ct) =>
            Task.FromResult(new EmailCredentials("token", encryptedCredentials, DateTimeOffset.UtcNow.AddHours(1)));

        public Task<IReadOnlyList<EmailMessage>> ListNewAsync(
            EmailConnection connection, CancellationToken ct)
        {
            if (CursorToEstablish is not null)
                connection.AdvanceCursor(CursorToEstablish);
            return Task.FromResult<IReadOnlyList<EmailMessage>>([.. Messages]);
        }

        public Task<string?> GetCurrentCursorAsync(EmailConnection connection, CancellationToken ct) =>
            Task.FromResult<string?>(null);

        public Task<EmailMessage> GetAsync(
            EmailConnection connection, string messageId, CancellationToken ct) =>
            Task.FromResult(Messages.First(m => m.MessageId == messageId));
    }

    /// <summary>
    /// A client whose provider is the stub above. RemoveAll + AddScoped replaces the real
    /// Gmail registration, so <c>POST /authorize</c> and the callback need no network and no
    /// OAuth client configuration.
    /// </summary>
    private sealed class StubbedEmailImportFactory(BudgieWebApplicationFactory inner) : BudgieWebApplicationFactory
    {
        public string ConnectionString => inner.GetContainerConnectionString();

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            // The inner factory owns the container; mirror its settings onto this host.
            builder.UseEnvironment("Testing");
            builder.UseSetting("UseInMemoryDatabase", "false");
            builder.UseSetting("ConnectionStrings:BudgieDb", inner.GetContainerConnectionString());
            builder.UseSetting("Registration:Mode", "Open");
            builder.UseSetting("EmailImport:Enabled", "false");

            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IEmailProviderClient>();
                // SINGLETON on purpose. The stub carries the queued messages, and a scoped
                // registration would hand every request its own empty instance — the sync
                // would then always see an empty mailbox.
                services.AddSingleton<StubEmailProviderClient>();
                services.AddScoped<IEmailProviderClient>(sp =>
                    sp.GetRequiredService<StubEmailProviderClient>());
            });
        }
    }

    private StubbedEmailImportFactory StubbedFactory => _stubbed ??= CreateStubbed();
    private StubbedEmailImportFactory? _stubbed;

    /// <summary>
    /// A client that does NOT follow redirects. The OAuth callback answers 302 to the
    /// frontend ("/settings?emailImport=connected"), a route the API does not serve, so a
    /// default client would chase it and report the resulting 404 instead of the 302 that
    /// is actually being asserted.
    /// </summary>
    private HttpClient NoRedirectClient()
    {
        var handler = new HttpClientHandler { AllowAutoRedirect = false };
        var client = StubbedFactory.CreateClient(
            new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false,
                HandleCookies = false
            });
        if (_redirectHandler is not null) _redirectHandler.Dispose();
        _redirectHandler = handler;
        return client;
    }

    private HttpClientHandler? _redirectHandler;

    private StubbedEmailImportFactory CreateStubbed()
    {
        var factory = new StubbedEmailImportFactory(_factory);
        // The stub host must not start a second Testcontainers container.
        typeof(BudgieWebApplicationFactory)
            .GetField("_container", System.Reflection.BindingFlags.Instance
                | System.Reflection.BindingFlags.NonPublic)!
            .SetValue(factory, typeof(BudgieWebApplicationFactory)
                .GetField("_container", System.Reflection.BindingFlags.Instance
                    | System.Reflection.BindingFlags.NonPublic)!
                .GetValue(_factory));
        return factory;
    }

    // ── Helpers ────────────────────────────────────────────────────────────────────────
    private async Task<HttpClient> ClientAsync()
    {
        var client = StubbedFactory.CreateClient();
        await AuthHelper.RegisterAndLoginAsync(client, "EUR");
        // Same reason as in ClientWithUserAsync: the dd/MM/yyyy fixture needs "es". The
        // MainCurrency must be resent because UpdateSettings replaces the whole row.
        await client.PostAsJsonAsync("/api/users/settings/setup", new
        {
            MainCurrency = "EUR",
            DefaultCreditConsumptionMode = 1,
            Language = "es"
        });
        return client;
    }

    private async Task<(HttpClient Client, Guid UserId)> ClientWithUserAsync()
    {
        var client = StubbedFactory.CreateClient();
        var email = $"emailimport_{Guid.NewGuid():N}@example.com";
        // POST /api/users/register answers { Id = userId }; there is no "who am I" endpoint.
        var registered = await client.PostAsJsonAsync("/api/users/register",
            new { Email = email, Password = "Password123!", Name = "Email Import User" });
        var userId = (await registered.Content.ReadFromJsonAsync<IdResponse>())!.Id;
        var login = await client.PostAsJsonAsync("/api/users/login",
            new { Email = email, Password = "Password123!" });
        var token = (await login.Content.ReadFromJsonAsync<LoginResult>())!.Token;
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        // Language = "es" is REQUIRED, not cosmetic: ILanguageProvider feeds the date and
        // amount normalizers, and the fixture writes dd/MM/yyyy. Under "en" the same text
        // would parse as month-first and the operation date would be wrong.
        await client.PostAsJsonAsync("/api/users/settings/setup", new
        {
            MainCurrency = "EUR",
            DefaultCreditConsumptionMode = 1,
            Language = "es"
        });

        return (client, userId);
    }

    private async Task<Guid> CreateRuleAsync(
        HttpClient client, Guid accountId, bool autoCommit = true, string? patterns = null)
    {
        var response = await client.PostAsJsonAsync("/api/email-import/rules", new
        {
            Name = "Banco Ejemplo",
            AdapterKey = "generic.regex",
            Priority = 0,
            IsEnabled = true,
            AccountId = accountId,
            CategoryId = (Guid?)null,
            TransactionType = 1,          // Expense (enums travel as NUMBERS on the wire)
            Currency = "EUR",
            ExchangeRate = (decimal?)null,
            FromAddress = (string?)null,
            SubjectContains = (string?)null,
            BodyRegex = (string?)null,
            FieldPatternsJson = patterns ?? ReadFixture(FixtureRules),
            AutoCommit = autoCommit
        });
        response.StatusCode.Should().Be(HttpStatusCode.Created,
            "the rule must be creatable: " + await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<IdResponse>())!.Id;
    }

    /// <summary>Drives the whole OAuth flow and asserts the connection was created.</summary>
    private async Task ConnectGmailAsync(HttpClient client)
    {
        var authorize = await client.PostAsync(
            "/api/email-import/connections/authorize?provider=gmail", null);
        authorize.StatusCode.Should().Be(HttpStatusCode.OK);
        var uri = (await authorize.Content.ReadFromJsonAsync<EmailAuthorizationUriDto>())!;

        // The state the module issued is enough to complete the callback: no JWT travels
        // on the provider's redirect.
        var callback = await NoRedirectClient().GetAsync(
            $"/api/email-import/connections/gmail/callback?code=auth-code-1&state={Uri.EscapeDataString(uri.State)}");
        callback.StatusCode.Should().Be(HttpStatusCode.Redirect);
        callback.Headers.Location!.OriginalString.Should().Contain("emailImport=connected");

        // The status DTO is metadata-only by construction: it carries no Id, which is why
        // a test can never learn an internal identifier from this endpoint.
        var connections = await client.GetFromJsonAsync<List<ConnectionStatusDto>>(
            "/api/email-import/connections");
        connections.Should().ContainSingle();
    }

    private NpgsqlConnection OpenDb()
    {
        var connection = new NpgsqlConnection(_factory.GetContainerConnectionString());
        connection.Open();
        return connection;
    }

    /// <summary>
    /// Transactions of one user, EXCLUDING the initial-balance Income rows that account
    /// creation inserts. Without that filter a user who created an account with 500 EUR
    /// already has one transaction, and every "exactly one imported transaction" assertion
    /// below would be off by one.
    /// </summary>
    private async Task<int> CountTransactionsAsync(Guid userId)
    {
        await using var db = OpenDb();
        return await db.ExecuteScalarAsync<int>(
            """
            SELECT COUNT(*) FROM "Transactions"
             WHERE "UserId" = @UserId AND COALESCE("IsInitialBalance", FALSE) = FALSE
            """,
            new { UserId = userId });
    }

    private async Task<List<(Guid Id, decimal Amount, int Type, string? ExternalUid, DateTime Date, string? Notes)>>
        ReadTransactionsAsync(Guid userId)
    {
        await using var db = OpenDb();
        var rows = await db.QueryAsync<TransactionRow>(
            // Initial-balance rows excluded for the same reason as CountTransactionsAsync.
            """
            SELECT "Id","Amount","Type","ExternalUid","Date","Notes"
              FROM "Transactions"
             WHERE "UserId" = @UserId AND COALESCE("IsInitialBalance", FALSE) = FALSE
             ORDER BY "Date"
            """, new { UserId = userId });
        return rows.Select(r => (r.Id, r.Amount, r.Type, r.ExternalUid, r.Date, r.Notes)).ToList();
    }

    private sealed record TransactionRow(
        Guid Id, decimal Amount, int Type, string? ExternalUid, DateTime Date, string? Notes);

    /// <summary>
    /// Seeds a connection directly so a sync can run without going through OAuth.
    /// </summary>
    private async Task<Guid> SeedConnectionAsync(Guid userId, Guid accountId)
    {
        var connectionId = Guid.NewGuid();
        await using var db = OpenDb();
        await db.ExecuteAsync(
            """
            INSERT INTO "EmailImportConnections"
                ("Id","UserId","Provider","Email","EncryptedCredentials","SyncEnabled","AutoCommitMinConfidence","LastSkippedCount")
            VALUES (@Id, @UserId, 0, 'usuario@banco.es', 'v1:bm9uY2U=:Y2lwaGVy:dGFn', TRUE, 0.85, 0)
            """,
            new { Id = connectionId, UserId = userId });
        return connectionId;
    }

    // ── Authorization ──────────────────────────────────────────────────────────────────
    [Fact]
    public async Task NormalEndpoint_WithoutToken_ReturnsUnauthorized()
    {
        var client = StubbedFactory.CreateClient();

        var response = await client.GetAsync("/api/email-import/rules");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task SyncEndpoint_WithoutToken_ReturnsUnauthorized()
    {
        var client = StubbedFactory.CreateClient();

        (await client.PostAsync("/api/email-import/sync", null))
            .StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task OAuthCallback_WithoutToken_IsNotUnauthorized()
    {
        // THE deliberate exception of the module (spec §6): the provider redirects the
        // browser without Budgie's JWT, so the endpoint authenticates with the signed state
        // alone. It must NOT answer 401, or the connection could never be completed.
        var client = StubbedFactory.CreateClient();

        var response = await NoRedirectClient().GetAsync(
            "/api/email-import/connections/gmail/callback?code=x&state=basura");

        response.StatusCode.Should().NotBe(HttpStatusCode.Unauthorized);
        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
    }

    [Fact]
    public async Task OAuthCallback_WithoutCodeOrState_RedirectsWithInvalidState()
    {
        var client = StubbedFactory.CreateClient();

        var response = await NoRedirectClient().GetAsync("/api/email-import/connections/gmail/callback");

        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        response.Headers.Location!.OriginalString.Should().Contain("EmailImport.InvalidState");
    }

    // ── OAuth state: the only authentication of the callback ───────────────────────────
    [Fact]
    public async Task OAuthFlow_CompletesTheCallbackAndPersistsTheConnection()
    {
        var (client, _) = await ClientWithUserAsync();

        await ConnectGmailAsync(client);

        var connections = await client.GetFromJsonAsync<List<ConnectionStatusDto>>(
            "/api/email-import/connections");
        connections.Should().ContainSingle();
        connections![0].Provider.Should().Be(0);   // Gmail, as a number
        connections[0].Email.Should().Be("connected");
        connections[0].SyncEnabled.Should().BeTrue();
    }

    [Fact]
    public async Task OAuthFlow_SameStateCannotBeConsumedTwice()
    {
        // The nonce is burned on the first consume, so a leaked authorization code cannot
        // be replayed against the callback.
        var (client, _) = await ClientWithUserAsync();

        var authorize = await client.PostAsync(
            "/api/email-import/connections/authorize?provider=gmail", null);
        var state = (await authorize.Content.ReadFromJsonAsync<EmailAuthorizationUriDto>())!.State;
        var callbackUrl =
            $"/api/email-import/connections/gmail/callback?code=auth-code&state={Uri.EscapeDataString(state)}";

        var first = await NoRedirectClient().GetAsync(callbackUrl);
        var second = await NoRedirectClient().GetAsync(callbackUrl);

        first.Headers.Location!.OriginalString.Should().Contain("emailImport=connected");
        second.Headers.Location!.OriginalString.Should().Contain("EmailImport.InvalidState");
    }

    [Fact]
    public async Task OAuthFlow_TamperedStateIsRejected()
    {
        var (client, _) = await ClientWithUserAsync();

        var authorize = await client.PostAsync(
            "/api/email-import/connections/authorize?provider=gmail", null);
        var state = (await authorize.Content.ReadFromJsonAsync<EmailAuthorizationUriDto>())!.State;
        var tampered = state[..^4] + "AAAA";

        var response = await NoRedirectClient().GetAsync(
            $"/api/email-import/connections/gmail/callback?code=c&state={Uri.EscapeDataString(tampered)}");

        response.Headers.Location!.OriginalString.Should().Contain("EmailImport.InvalidState");
        (await client.GetFromJsonAsync<List<ConnectionStatusDto>>("/api/email-import/connections"))
            .Should().BeEmpty();
    }

    [Fact]
    public async Task OAuthFlow_StateOfOneUserCannotCreateAConnectionForAnother()
    {
        // The state seals userId+provider+redirectUri, so a state issued for user A stays
        // bound to A no matter whose browser completes the flow.
        var (clientA, userAId) = await ClientWithUserAsync();
        var (clientB, _) = await ClientWithUserAsync();

        var authorize = await clientA.PostAsync(
            "/api/email-import/connections/authorize?provider=gmail", null);
        var stateOfA = (await authorize.Content.ReadFromJsonAsync<EmailAuthorizationUriDto>())!.State;

        // User B's browser completes the flow, presenting A's state.
        await NoRedirectClient().GetAsync(
            $"/api/email-import/connections/gmail/callback?code=c&state={Uri.EscapeDataString(stateOfA)}");

        await using var db = OpenDb();
        // The connection belongs to A — the state decided, not the caller.
        (await db.ExecuteScalarAsync<int>(
            """SELECT COUNT(*) FROM "EmailImportConnections" WHERE "UserId" = @UserId""",
            new { UserId = userAId })).Should().Be(1);
        (await db.ExecuteScalarAsync<int>(
            """SELECT COUNT(*) FROM "EmailImportConnections" WHERE "Email" = 'connected'"""))
            .Should().Be(1);
    }

    [Fact]
    public async Task OAuthFlow_AlreadyConnectedProvider_IsRefused()
    {
        var (client, _) = await ClientWithUserAsync();
        await ConnectGmailAsync(client);

        var second = await client.PostAsync(
            "/api/email-import/connections/authorize?provider=gmail", null);

        second.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await second.Content.ReadAsStringAsync()).Should().Contain("EmailImport.AlreadyConnected");
    }

    [Fact]
    public async Task OAuthFlow_UnknownProvider_IsRefused()
    {
        var (client, _) = await ClientWithUserAsync();

        var response = await client.PostAsync(
            "/api/email-import/connections/authorize?provider=outlook", null);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync()).Should().Contain("EmailImport.UnknownProvider");
    }

    // ── Credentials are never in clear text at rest ────────────────────────────────────
    [Fact]
    public async Task Credentials_AreNeverStoredInClearText()
    {
        var (client, userId) = await ClientWithUserAsync();

        await ConnectGmailAsync(client);

        // Read the RAW column: a DTO-level assertion could never prove what is on disk.
        await using var db = OpenDb();
        var stored = await db.ExecuteScalarAsync<string>(
            """SELECT "EncryptedCredentials" FROM "EmailImportConnections" WHERE "UserId" = @UserId""",
            new { UserId = userId });

        stored.Should().NotBeNullOrEmpty();
        stored.Should().StartWith("v1:");
        stored.Should().NotContain(StubEmailProviderClient.ClearTextRefreshToken);
        stored.Should().NotContain("1//0e");
        stored.Should().NotContain("ya29.access-token-en-claro");
    }

    [Fact]
    public async Task ConnectionStatus_NeverExposesTheCredential()
    {
        var (client, _) = await ClientWithUserAsync();
        await ConnectGmailAsync(client);

        var raw = await (await client.GetAsync("/api/email-import/connections")).Content
            .ReadAsStringAsync();

        raw.Should().NotContain("EncryptedCredentials");
        raw.Should().NotContain("v1:");
        raw.Should().NotContain(StubEmailProviderClient.ClearTextRefreshToken);
    }

    [Fact]
    public async Task Disconnect_RemovesTheConnection()
    {
        var (client, userId) = await ClientWithUserAsync();
        await ConnectGmailAsync(client);

        var response = await client.DeleteAsync("/api/email-import/connections/gmail");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        await using var db = OpenDb();
        (await db.ExecuteScalarAsync<int>(
            """SELECT COUNT(*) FROM "EmailImportConnections" WHERE "UserId" = @UserId""",
            new { UserId = userId })).Should().Be(0);
    }

    // ── Rules CRUD ─────────────────────────────────────────────────────────────────────
    [Fact]
    public async Task Rules_CreateListUpdateDelete()
    {
        var client = await ClientAsync();
        var accountId = await AuthHelper.CreateAccountAsync(client, "Corriente", "EUR", 500m);

        var created = await client.PostAsJsonAsync("/api/email-import/rules", new
        {
            Name = "Banco Ejemplo",
            AdapterKey = "generic.regex",
            Priority = 0,
            IsEnabled = true,
            AccountId = accountId,
            CategoryId = (Guid?)null,
            TransactionType = 1,
            Currency = "EUR",
            ExchangeRate = (decimal?)null,
            FromAddress = (string?)null,
            SubjectContains = (string?)null,
            BodyRegex = (string?)null,
            FieldPatternsJson = ReadFixture(FixtureRules),
            AutoCommit = false
        });
        created.StatusCode.Should().Be(HttpStatusCode.Created);
        var id = (await created.Content.ReadFromJsonAsync<IdResponse>())!.Id;

        var list = await client.GetFromJsonAsync<List<RuleDto>>("/api/email-import/rules");
        list.Should().ContainSingle();
        list![0].Id.Should().Be(id);
        list[0].TransactionType.Should().Be(1);   // the enum travels as a number

        var updated = await client.PutAsJsonAsync($"/api/email-import/rules/{id}", new
        {
            Name = "Banco Ejemplo (v2)",
            AdapterKey = "generic.regex",
            Priority = 5,
            IsEnabled = false,
            AccountId = accountId,
            CategoryId = (Guid?)null,
            TransactionType = 0,                  // Income
            Currency = "EUR",
            ExchangeRate = (decimal?)null,
            FromAddress = (string?)null,
            SubjectContains = (string?)null,
            BodyRegex = (string?)null,
            FieldPatternsJson = ReadFixture(FixtureRules),
            AutoCommit = false
        });
        updated.StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await client.GetFromJsonAsync<List<RuleDto>>("/api/email-import/rules"))!
            .Single().TransactionType.Should().Be(0);

        (await client.DeleteAsync($"/api/email-import/rules/{id}"))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await client.GetFromJsonAsync<List<RuleDto>>("/api/email-import/rules")).Should().BeEmpty();
    }

    [Fact]
    public async Task Rules_InvalidRegexIsRejectedWithAStableCode()
    {
        // Validation happens at WRITE time on purpose: a broken regex saved now would fail
        // silently at sync time, when the user is not looking.
        var client = await ClientAsync();
        var accountId = await AuthHelper.CreateAccountAsync(client, "Corriente", "EUR", 500m);

        var response = await client.PostAsJsonAsync("/api/email-import/rules", new
        {
            Name = " Rota",
            AdapterKey = "generic.regex",
            Priority = 0,
            IsEnabled = true,
            AccountId = accountId,
            CategoryId = (Guid?)null,
            TransactionType = 1,
            Currency = "EUR",
            ExchangeRate = (decimal?)null,
            FromAddress = (string?)null,
            SubjectContains = (string?)null,
            BodyRegex = (string?)null,
            FieldPatternsJson = """{"amount":"[unclosed"}""",
            AutoCommit = false
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync()).Should().Contain("EmailImport.InvalidRule");
    }

    [Fact]
    public async Task Rules_ForeignAccountIsRejected()
    {
        var client = await ClientAsync();
        var otherAccountId = await AuthHelper.CreateAccountAsync(client, "Otra", "EUR", 100m);
        await CreateRuleAsync(client, otherAccountId, autoCommit: false);

        var response = await client.PostAsJsonAsync("/api/email-import/rules", new
        {
            Name = "Ajena",
            AdapterKey = "generic.regex",
            Priority = 0,
            IsEnabled = true,
            AccountId = Guid.NewGuid(),
            CategoryId = (Guid?)null,
            TransactionType = 1,
            Currency = "EUR",
            ExchangeRate = (decimal?)null,
            FromAddress = (string?)null,
            SubjectContains = (string?)null,
            BodyRegex = (string?)null,
            FieldPatternsJson = ReadFixture(FixtureRules),
            AutoCommit = false
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync()).Should().Contain("Account.NotFound");
    }

    // ── The rule tester ───────────────────────────────────────────────────────────────
    [Fact]
    public async Task PreviewRule_ParsesTheFixtureAndReportsPerFieldConfidence()
    {
        var client = await ClientAsync();
        var accountId = await AuthHelper.CreateAccountAsync(client, "Corriente", "EUR", 500m);

        var response = await client.PostAsJsonAsync("/api/email-import/rules/preview", new
        {
            RuleId = (Guid?)null,
            Draft = new
            {
                Name = "Banco Ejemplo",
                AdapterKey = "generic.regex",
                Priority = 0,
                AccountId = accountId,
                CategoryId = (Guid?)null,
                TransactionType = 1,
                Currency = "EUR",
                ExchangeRate = (decimal?)null,
                FromAddress = (string?)"notificaciones@banco-ejemplo.es",
                SubjectContains = (string?)null,
                BodyRegex = (string?)null,
                FieldPatternsJson = ReadFixture(FixtureRules),
                AutoCommit = false
            },
            RawEmail = ReadFixture(FixtureEmail),
            FromAddress = "notificaciones@banco-ejemplo.es",
            Subject = "Justificante de operacion"
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var preview = (await response.Content.ReadFromJsonAsync<PreviewDto>())!;
        // The MIME really was decoded: the table became tabs and the base64 became text.
        preview.PlainText.Should().Contain("Importe: 45,20 EUR");
        preview.Fields["amount"].Should().Be("45.20");
        preview.Fields["currency"].Should().Be("EUR");
        preview.Fields["date"].Should().Be("2026-10-01");
        preview.Fields["type"].Should().Be("Expense");
        preview.SkipReason.Should().BeNull();

        // The anchored amount pattern matched exactly once, so the score is a full 1.0.
        preview.FieldConfidence["amount"].Should().Be(1.0m);
        preview.Confidence.Should().Be(1.0m);
        preview.Explanation.Should().HaveCount(5);
    }

    [Fact]
    public async Task PreviewRule_UnanchoredAmountPatternFlagsTheRowAndFallsBelowTheThreshold()
    {
        // The trap the anchored pattern exists to avoid: GenericRegexAdapter takes
        // amountMatches[0], and with the balances first that IS the previous balance. The
        // 0.3 penalty is what routes such a message to a human.
        var client = await ClientAsync();
        var accountId = await AuthHelper.CreateAccountAsync(client, "Corriente", "EUR", 500m);

        var response = await client.PostAsJsonAsync("/api/email-import/rules/preview", new
        {
            RuleId = (Guid?)null,
            Draft = new
            {
                Name = "Sin anclar",
                AdapterKey = "generic.regex",
                Priority = 0,
                AccountId = accountId,
                CategoryId = (Guid?)null,
                TransactionType = 1,
                Currency = "EUR",
                ExchangeRate = (decimal?)null,
                FromAddress = (string?)null,
                SubjectContains = (string?)null,
                BodyRegex = (string?)null,
                FieldPatternsJson = """{"amount":"[0-9.]+,[0-9]{2}"}""",
                AutoCommit = false
            },
            RawEmail = ReadFixture(FixtureEmail),
            FromAddress = "notificaciones@banco-ejemplo.es",
            Subject = "Justificante de operacion"
        });

        var preview = (await response.Content.ReadFromJsonAsync<PreviewDto>())!;

        // 3 matches → amount 0.3, and the whole parse drops far below the threshold, so the
        // message MUST reach a human instead of being auto-committed.
        preview.FieldConfidence["amount"].Should().Be(0.3m);
        preview.Confidence.Should().BeLessThan(0.85m);

        // The remaining fields score as designed: this draft declares only an amount
        // pattern, so the direction comes from the rule (1.0) but there is no merchant or
        // date pattern at all (both 0), and no symbol is captured so the rule's declared
        // currency stands (1.0). 0.35*0.3 + 0.20 + 0 + 0 + 0.10 = 0.405. The important
        // assertion is the one above: whatever the exact total, it is far from 1.0.
        preview.FieldConfidence["direction"].Should().Be(1.0m);
        preview.FieldConfidence["merchant"].Should().Be(0m);
        preview.FieldConfidence["currency"].Should().Be(1.0m);

        // No date pattern means the adapter falls back to the RECEIPT date, scored 0.5 (not
        // 1.0): that timestamp is reliable but carries the arrival time, not the day of the
        // operation. Total: 0.35*0.3 + 0.20 + 0 + 0.15*0.5 + 0.10 = 0.48.
        preview.FieldConfidence["date"].Should().Be(0.5m);
        preview.Confidence.Should().Be(0.48m);

        // The first match is the REAL Importe line, which comes first in the fixture — the
        // exact reason the balance lines are ordered AFTER it.
        preview.Fields["amount"].Should().Be("45.20");
    }

    [Fact]
    public async Task PreviewRule_WritesNothing()
    {
        var (client, _) = await ClientWithUserAsync();
        var accountId = await AuthHelper.CreateAccountAsync(client, "Corriente", "EUR", 0m);

        var response = await client.PostAsJsonAsync("/api/email-import/rules/preview", new
        {
            RuleId = (Guid?)null,
            Draft = new
            {
                Name = "Banco Ejemplo", AdapterKey = "generic.regex", Priority = 0,
                AccountId = accountId, CategoryId = (Guid?)null, TransactionType = 1,
                Currency = "EUR", ExchangeRate = (decimal?)null,
                FromAddress = (string?)null, SubjectContains = (string?)null,
                BodyRegex = (string?)null,
                FieldPatternsJson = ReadFixture(FixtureRules), AutoCommit = false
            },
            RawEmail = ReadFixture(FixtureEmail),
            FromAddress = "notificaciones@banco-ejemplo.es",
            Subject = "Justificante de operacion"
        });
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Scoped to THIS user: the database is shared by the whole collection, so a global
        // count would see the other tests' rows.
        var userId = CurrentUserIdAsync(client);
        (await CountTransactionsAsync(userId)).Should().Be(0);
        await using var db = OpenDb();
        (await db.ExecuteScalarAsync<int>(
            """SELECT COUNT(*) FROM "PendingEmailTransactions" WHERE "UserId" = @UserId""",
            new { UserId = userId })).Should().Be(0);
    }

    [Fact]
    public async Task PreviewRule_WithoutRuleIdOrDraft_IsRejected()
    {
        var client = await ClientAsync();

        var response = await client.PostAsJsonAsync("/api/email-import/rules/preview", new
        {
            RuleId = (Guid?)null,
            Draft = (object?)null,
            RawEmail = ReadFixture(FixtureEmail),
            FromAddress = "a@b.es",
            Subject = "x"
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync())
            .Should().Contain("EmailImport.PreviewRuleMissing");
    }

    // ── Sync and idempotency ───────────────────────────────────────────────────────────

    /// <summary>
    /// The operation date carried by the fixture ("Fecha: 01/10/2026"), parsed in the
    /// Spanish dd/MM/yyyy convention the module must apply.
    /// </summary>
    private static DateOnly FixtureOperationDate => new(2026, 10, 1);

    /// <summary>
    /// Creates the account the rules point at, with <c>ControlStartDate</c> pinned to the
    /// fixture's operation date.
    /// <para>This is how the time trap is defused, and deliberately NOT by rewriting the
    /// date in the message: the fixture body is base64-encoded inside the MIME, so a text
    /// substitution cannot reach it, and a date anchored to "today" would stop working as
    /// soon as the fixture aged past the account's control start date. Pinning the ACCOUNT's
    /// control date to the FIXTURE's date keeps the pair consistent forever, and it exercises
    /// the real Ledger Rule instead of bypassing it.</para>
    /// </summary>
    private async Task<Guid> CreateAccountForFixtureAsync(HttpClient client)
    {
        var response = await client.PostAsJsonAsync("/api/finances/accounts", new
        {
            Name = "Corriente",
            Currency = "EUR",
            InitialBalance = 0m,
            Type = 1,                               // Checking
            CreatedAt = FixtureOperationDate.ToDateTime(TimeOnly.MinValue),
            ControlStartDate = FixtureOperationDate.ToDateTime(TimeOnly.MinValue)
        });
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        return (await response.Content.ReadFromJsonAsync<IdResponse>())!.Id;
    }

    /// <summary>
    /// Registers one message in the stub provider, served to whichever connection the
    /// service resolves. The message body is the real MIME fixture; the OPERATION DATE is
    /// rewritten to the run's own date on purpose.
    /// <para>Why not a fixed date in the fixture: a freshly created account gets
    /// <c>ControlStartDate = today</c> and <c>CreateTransactionCommand</c> rejects anything
    /// before it, so a date pinned in the fixture would start failing
    /// <c>Transaction.BeforeControlStartDate</c> as soon as the suite runs on a later day.
    /// Anchoring the operation date to the run keeps the test correct forever, and the very
    /// same value is what the assertions compare against.</para>
    /// </summary>
    private static EmailMessage FixtureMessage(string messageId, DateOnly? operationDate = null)
    {
        // The body is the RAW MIME fixture, unmodified: it is base64-encoded inside the
        // message, so its operation date cannot be rewritten by a text substitution. The
        // pairing with the account's ControlStartDate is what CreateAccountForFixtureAsync
        // handles.
        _ = operationDate;
        return EmailMessage.Create(
            messageId, "thread-1", "notificaciones@banco-ejemplo.es",
            "Justificante de operacion", ReadFixture(FixtureEmail),
            DateTimeOffset.UtcNow);
    }

    /// <summary>
    /// Moves an account's ControlStartDate back to the fixture's operation date, so the
    /// fixed date inside the MIME fixture is always acceptable to the Ledger Rule. The
    /// initial balance is left untouched.
    /// </summary>
    private async Task UpdateAccountControlDateAsync(HttpClient client, Guid accountId)
    {
        await using var db = OpenDb();
        await db.ExecuteAsync(
            """
            UPDATE "Accounts" SET "ControlStartDate" = @Date
             WHERE "Id" = @Id
            """,
            new { Id = accountId, Date = FixtureOperationDate.ToDateTime(TimeOnly.MinValue) });
    }

    /// <summary>Serves <paramref name="messages"/> from the stub provider for the next sync.</summary>
    private void ProviderServes(params EmailMessage[] messages)
    {
        var stub = StubbedFactory.Services.GetRequiredService<StubEmailProviderClient>();
        stub.Messages.Clear();
        stub.Messages.AddRange(messages);
        stub.CursorToEstablish = "cursor-tras-el-sincronismo";
    }

    /// <summary>
    /// The operation date every test uses. It is TODAY and not a literal date in the fixture
    /// because an account created now has <c>ControlStartDate = today</c> and anything
    /// earlier is rejected by the Ledger Rule.
    /// </summary>
    private static DateOnly OperationDay => DateOnly.FromDateTime(DateTime.UtcNow);

    [Fact]
    public async Task Sync_AutoCommitRule_CommitsTheParsedTransactionAndStagesNothing()
    {
        var (client, userId) = await ClientWithUserAsync();
        var accountId = await CreateAccountForFixtureAsync(client);
        await CreateRuleAsync(client, accountId, autoCommit: true);
        await SeedConnectionAsync(userId, accountId);
        ProviderServes(FixtureMessage("msg-sync-1", OperationDay));

        var response = await client.PostAsync("/api/email-import/sync", null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var tally = (await response.Content.ReadFromJsonAsync<SyncResultDto>())!;
        tally.MessagesFetched.Should().Be(1);
        tally.AutoCommitted.Should().Be(1);
        tally.Staged.Should().Be(0);
        tally.Failed.Should().Be(0);

        var transactions = await ReadTransactionsAsync(userId);
        transactions.Should().ContainSingle();
        transactions[0].Amount.Should().Be(45.20m);
        transactions[0].Type.Should().Be(1);                 // Expense, as a number
        transactions[0].ExternalUid.Should().Be("email:gmail:msg-sync-1");
        transactions[0].Date.Should().Be(FixtureOperationDate.ToDateTime(TimeOnly.MinValue));
        // Auto-commit deliberately sends NO notes: nobody vouched for the row. The merchant
        // lives on the staging row, where a human can see and correct it. (The manual
        // approval DOES send it — a person reviewed it.)
        transactions[0].Notes.Should().BeNull();
    }

    [Fact]
    public async Task Sync_AutoCommittedTransaction_MovesTheAccountBalance()
    {
        // Proves the dispatch went through the real CreateTransactionCommand handler and
        // inherited the TransactionCreatedEvent that updates the balance.
        var (client, userId) = await ClientWithUserAsync();
        var accountId = await AuthHelper.CreateAccountAsync(client, "Corriente", "EUR", 500m);
        await UpdateAccountControlDateAsync(client, accountId);
        await CreateRuleAsync(client, accountId, autoCommit: true);
        await SeedConnectionAsync(userId, accountId);
        ProviderServes(FixtureMessage("msg-sync-balance", OperationDay));

        await client.PostAsync("/api/email-import/sync", null);

        await using var db = OpenDb();
        var balance = await db.ExecuteScalarAsync<decimal>(
            """SELECT "Balance" FROM "Accounts" WHERE "Id" = @Id""", new { Id = accountId });
        balance.Should().Be(500m - 45.20m);
    }

    [Fact]
    public async Task Sync_RunTwice_CreatesNothingNew()
    {
        // THE most important test in the system: re-running the sync must not duplicate.
        // Counted straight off "Transactions" before and after, not off the tally.
        var (client, userId) = await ClientWithUserAsync();
        var accountId = await CreateAccountForFixtureAsync(client);
        await CreateRuleAsync(client, accountId, autoCommit: true);
        await SeedConnectionAsync(userId, accountId);
        ProviderServes(FixtureMessage("msg-sync-twice", OperationDay));

        var first = await client.PostAsync("/api/email-import/sync", null);
        first.StatusCode.Should().Be(HttpStatusCode.OK);
        var countAfterFirst = await CountTransactionsAsync(userId);
        countAfterFirst.Should().Be(1);

        var second = await client.PostAsync("/api/email-import/sync", null);
        second.StatusCode.Should().Be(HttpStatusCode.OK);
        var countAfterSecond = await CountTransactionsAsync(userId);

        countAfterSecond.Should().Be(countAfterFirst);
        countAfterSecond.Should().Be(1);

        // The second pass reported the message as fetched but committed nothing, and the
        // staging barrier is what stopped it.
        var tally = (await second.Content.ReadFromJsonAsync<SyncResultDto>())!;
        tally.AutoCommitted.Should().Be(0);

        // And exactly one staging row exists for that MessageId.
        await using var db = OpenDb();
        (await db.ExecuteScalarAsync<int>(
            """SELECT COUNT(*) FROM "PendingEmailTransactions" WHERE "UserId" = @UserId""",
            new { UserId = userId })).Should().Be(1);
    }

    [Fact]
    public async Task Sync_RunTwice_KeepsASingleStagingRowPerMessageId()
    {
        // The staging UNIQUE("UserId","MessageId") is the first barrier; the ExternalUid
        // index is the second. Both are exercised by running the same batch twice.
        var (client, userId) = await ClientWithUserAsync();
        var accountId = await CreateAccountForFixtureAsync(client);
        await CreateRuleAsync(client, accountId, autoCommit: true);
        await SeedConnectionAsync(userId, accountId);
        ProviderServes(FixtureMessage("msg-uniq", OperationDay), FixtureMessage("msg-uniq-2", OperationDay));

        await client.PostAsync("/api/email-import/sync", null);
        await client.PostAsync("/api/email-import/sync", null);
        await client.PostAsync("/api/email-import/sync", null);

        await using var db = OpenDb();
        (await db.ExecuteScalarAsync<int>(
            """SELECT COUNT(*) FROM "PendingEmailTransactions" WHERE "UserId" = @UserId""",
            new { UserId = userId })).Should().Be(2);
        (await CountTransactionsAsync(userId)).Should().Be(2);
    }

    [Fact]
    public async Task Sync_StagingRowOfAnAutoCommittedMessage_IsMarkedCommittedAndLinked()
    {
        var (client, userId) = await ClientWithUserAsync();
        var accountId = await CreateAccountForFixtureAsync(client);
        await CreateRuleAsync(client, accountId, autoCommit: true);
        await SeedConnectionAsync(userId, accountId);
        ProviderServes(FixtureMessage("msg-linked", OperationDay));

        await client.PostAsync("/api/email-import/sync", null);

        await using var db = OpenDb();
        var rows = (await db.QueryAsync<StagingRow>(
            """
            SELECT "Id","Status","CreatedTransactionId","Amount","Confidence","Date","FailureReason"
              FROM "PendingEmailTransactions" WHERE "UserId" = @UserId
            """,
            new { UserId = userId })).ToList();

        rows.Should().ContainSingle();
        rows[0].Status.Should().Be(1);          // Committed, as a number
        rows[0].CreatedTransactionId.Should().NotBeNull();
        rows[0].Amount.Should().Be(45.20m);
        rows[0].Confidence.Should().Be(1.0m);
    }

    private sealed record StagingRow(
        Guid Id, int Status, Guid? CreatedTransactionId, decimal Amount, decimal Confidence,
        DateTime Date, string? FailureReason);

    private sealed record SyncResultDto(
        int ConnectionsProcessed, int MessagesFetched, int AutoCommitted,
        int Staged, int Skipped, int Failed, List<string> ConnectionErrors);

    [Fact]
    public async Task Sync_RuleWithoutAutoCommit_LeavesTheRowPendingForReview()
    {
        var (client, userId) = await ClientWithUserAsync();
        var accountId = await CreateAccountForFixtureAsync(client);
        await CreateRuleAsync(client, accountId, autoCommit: false);
        await SeedConnectionAsync(userId, accountId);
        ProviderServes(FixtureMessage("msg-pending", OperationDay));

        var response = await client.PostAsync("/api/email-import/sync", null);
        var tally = (await response.Content.ReadFromJsonAsync<SyncResultDto>())!;

        tally.Staged.Should().Be(1);
        tally.AutoCommitted.Should().Be(0);
        (await CountTransactionsAsync(userId)).Should().Be(0);
    }

    [Fact]
    public async Task Inbox_ListsPendingRowsWithTheOriginalEmail()
    {
        var (client, userId) = await ClientWithUserAsync();
        var accountId = await CreateAccountForFixtureAsync(client);
        await CreateRuleAsync(client, accountId, autoCommit: false);
        await SeedConnectionAsync(userId, accountId);
        ProviderServes(FixtureMessage("msg-inbox", OperationDay));

        await client.PostAsync("/api/email-import/sync", null);

        var page = (await client.GetFromJsonAsync<InboxPageDto>("/api/email-import/inbox"))!;
        page.Total.Should().Be(1);
        page.Items.Should().ContainSingle();
        var row = page.Items[0];
        row.Amount.Should().Be(45.20m);
        row.Currency.Should().Be("EUR");
        row.TransactionType.Should().Be(1);
        row.Merchant.Should().Be("Cafetería Central");
        // "Ver correo original" needs the WHOLE message: the raw MIME source is stored
        // verbatim, headers and base64 included, so the user can inspect what the provider
        // actually delivered rather than a lossy re-rendering of it.
        row.RawBody.Should().Contain("Content-Transfer-Encoding: base64");
        row.RawBody.Should().Contain("--=_BANCO_EJEMPLO_001");
        row.RawBody.Should().Contain("From: \"Banco Ejemplo, S.A.\"");
    }

    private sealed record InboxPageDto(
        List<InboxItemDto> Items, int Total, int Page, int PageSize);

    private sealed record InboxItemDto(
        Guid Id, Guid ConnectionId, Guid? RuleId, string? Subject, string? Merchant,
        string? Notes, decimal Amount, string Currency, decimal ExchangeRate, DateTime Date,
        int TransactionType, decimal Confidence, string? FieldConfidenceJson,
        string? RawBody, string? FailureReason, DateTime CreatedAt);

    // ── ?status= binding (debt plan §3.3: PendingEmailStatus moved to Budgie.Shared.Enums) ─
    [Fact]
    public async Task Inbox_UnknownStatusValue_IsRejectedWith400()
    {
        // The enum moved to Shared (slice 9) with its numeric values intact. This pins that
        // binding accepts ONLY the documented members: any other integer is a 400, never a
        // silent fall-through to Pending.
        var (client, _) = await ClientWithUserAsync();

        var response = await client.GetAsync("/api/email-import/inbox?status=99");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Theory]
    [InlineData("Pending")]
    [InlineData("Committed")]
    [InlineData("Dismissed")]
    [InlineData("Failed")]
    public async Task Inbox_DocumentedStatusValues_AreAcceptedByTheBinding(string status)
    {
        // Binding of the moved enum keeps working for every member, by name as the FE sends
        // the numeric values and the API accepts both. A fresh user has no rows, so the page
        // is empty — the point is the 200, not the data.
        var (client, _) = await ClientWithUserAsync();

        var page = await client.GetFromJsonAsync<InboxPageDto>(
            $"/api/email-import/inbox?status={status}");

        page.Should().NotBeNull();
        page!.Total.Should().Be(0);
    }

    // ── Manual approval ────────────────────────────────────────────────────────────────
    private async Task<Guid> StageOnePendingRowAsync(HttpClient client, Guid userId)
    {
        var accountId = await CreateAccountForFixtureAsync(client);
        await CreateRuleAsync(client, accountId, autoCommit: false);
        await SeedConnectionAsync(userId, accountId);
        ProviderServes(FixtureMessage("msg-approve", OperationDay));

        await client.PostAsync("/api/email-import/sync", null);
        var page = (await client.GetFromJsonAsync<InboxPageDto>("/api/email-import/inbox"))!;
        page.Items.Should().ContainSingle();
        return page.Items[0].Id;
    }

    /// <summary>
    /// The id of the user behind a plain <see cref="HttpClient"/> produced by
    /// <c>AuthHelper</c>. There is no "who am I" endpoint, so it is read from the JWT the
    /// helper just installed on the client's default headers.
    /// </summary>
    private static Guid CurrentUserIdAsync(HttpClient client)
    {
        var header = client.DefaultRequestHeaders.Authorization
            ?? throw new InvalidOperationException("The client is not authenticated.");
        var payload = header.Parameter.Split('.')[1];
        var padded = payload.Replace('-', '+').Replace('_', '/')
            .PadRight((payload.Length + 3) / 4 * 4, '=');
        var json = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(padded));
        foreach (var claim in JsonDocument.Parse(json).RootElement.EnumerateObject())
        {
            // "nameid" is ClaimTypes.NameIdentifier in the short JWT form.
            if (claim.NameEquals("nameid") || claim.NameEquals("sub"))
                return claim.Value.GetGuid();
        }
        throw new InvalidOperationException("The JWT carries no user id claim.");
    }

    [Fact]
    public async Task Approve_WithoutCorrections_CreatesExactlyOneTransactionWithTheStagedValues()
    {
        var (client, userId) = await ClientWithUserAsync();
        var pendingId = await StageOnePendingRowAsync(client, userId);

        var response = await client.PostAsJsonAsync(
            $"/api/email-import/inbox/{pendingId}/approve", new { });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await CountTransactionsAsync(userId)).Should().Be(1);

        var transaction = (await ReadTransactionsAsync(userId)).Single();
        transaction.Amount.Should().Be(45.20m);
        transaction.Type.Should().Be(1);
        transaction.Date.Should().Be(FixtureOperationDate.ToDateTime(TimeOnly.MinValue));
        transaction.ExternalUid.Should().Be("email:gmail:msg-approve");
    }

    [Fact]
    public async Task Approve_WithCorrections_CreatesOneTransactionWithTheEditedValues()
    {
        var (client, userId) = await ClientWithUserAsync();
        var pendingId = await StageOnePendingRowAsync(client, userId);

        var response = await client.PostAsJsonAsync(
            $"/api/email-import/inbox/{pendingId}/approve", new
            {
                Amount = 50.00m,
                // A correction must stay on or after the account's ControlStartDate
                // (pinned to the fixture's date), otherwise the Ledger Rule rejects it.
                Date = FixtureOperationDate.AddDays(2).ToDateTime(TimeOnly.MinValue),
                Merchant = "Otro comercio",
                Notes = "corregido a mano",
                AccountId = (Guid?)null,
                CategoryId = (Guid?)null,
                Type = 0            // Income
            });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var transactions = await ReadTransactionsAsync(userId);
        transactions.Should().ContainSingle();
        transactions[0].Amount.Should().Be(50.00m);      // NOT the staged 45.20
        transactions[0].Type.Should().Be(0);
        transactions[0].Date.Should().Be(FixtureOperationDate.AddDays(2).ToDateTime(TimeOnly.MinValue));
        transactions[0].Notes.Should().Be("Otro comercio");
    }

    [Fact]
    public async Task Approve_TheSameRowTwice_DoesNotCreateASecondTransaction()
    {
        var (client, userId) = await ClientWithUserAsync();
        var pendingId = await StageOnePendingRowAsync(client, userId);

        var first = await client.PostAsJsonAsync($"/api/email-import/inbox/{pendingId}/approve", new { });
        first.StatusCode.Should().Be(HttpStatusCode.OK);
        (await CountTransactionsAsync(userId)).Should().Be(1);

        var second = await client.PostAsJsonAsync($"/api/email-import/inbox/{pendingId}/approve", new { });

        second.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await second.Content.ReadAsStringAsync()).Should().Contain("EmailImport.PendingNotFound");
        (await CountTransactionsAsync(userId)).Should().Be(1);   // still exactly ONE
    }

    [Fact]
    public async Task Approve_AfterTheWorkerAlreadyCommittedTheSameEmail_DoesNotDuplicate()
    {
        // The cross-route race the byte-identical ExternalUid exists for: the worker
        // auto-commits, then the user approves the very same message.
        var (client, userId) = await ClientWithUserAsync();
        var accountId = await CreateAccountForFixtureAsync(client);
        await CreateRuleAsync(client, accountId, autoCommit: true);
        await SeedConnectionAsync(userId, accountId);
        ProviderServes(FixtureMessage("msg-race", OperationDay));

        await client.PostAsync("/api/email-import/sync", null);
        (await CountTransactionsAsync(userId)).Should().Be(1);

        // The staging row is Committed, so the approve is refused by the Status guard.
        var page = (await client.GetFromJsonAsync<InboxPageDto>("/api/email-import/inbox"))!;
        page.Total.Should().Be(0);

        var forced = await client.PostAsJsonAsync(
            $"/api/email-import/inbox/{await PendingIdOfAsync(client, userId)}/approve", new { });
        forced.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        (await CountTransactionsAsync(userId)).Should().Be(1);
    }

    private async Task<Guid> PendingIdOfAsync(HttpClient client, Guid userId)
    {
        await using var db = OpenDb();
        return await db.ExecuteScalarAsync<Guid>(
            """SELECT "Id" FROM "PendingEmailTransactions" WHERE "UserId" = @UserId LIMIT 1""",
            new { UserId = userId });
    }

    [Fact]
    public async Task Approve_RowWithoutATargetAccount_IsRejectedWithAStableCode()
    {
        // Its rule was deleted (RuleId is ON DELETE SET NULL): without an account there is
        // nothing to dispatch to.
        var (client, userId) = await ClientWithUserAsync();
        var pendingId = await StageOnePendingRowAsync(client, userId);
        await using (var db = OpenDb())
            await db.ExecuteAsync(
                """UPDATE "PendingEmailTransactions" SET "RuleId" = NULL WHERE "Id" = @Id""",
                new { Id = pendingId });

        var response = await client.PostAsJsonAsync($"/api/email-import/inbox/{pendingId}/approve", new { });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync()).Should().Contain("EmailImport.AccountRequired");
        (await CountTransactionsAsync(userId)).Should().Be(0);
    }

    [Fact]
    public async Task Dismiss_KeepsTheRowButStopsItFromBeingReviewed()
    {
        var (client, userId) = await ClientWithUserAsync();
        var pendingId = await StageOnePendingRowAsync(client, userId);

        var dismissed = await client.PostAsync($"/api/email-import/inbox/{pendingId}/dismiss", null);
        dismissed.StatusCode.Should().Be(HttpStatusCode.NoContent);

        await using var db = OpenDb();
        // The row is KEPT: its MessageId must keep blocking re-import of the same email.
        (await db.ExecuteScalarAsync<int>(
            """SELECT COUNT(*) FROM "PendingEmailTransactions" WHERE "UserId" = @UserId AND "Status" = 2""",
            new { UserId = userId })).Should().Be(1);

        var page = (await client.GetFromJsonAsync<InboxPageDto>("/api/email-import/inbox"))!;
        page.Total.Should().Be(0);
    }

    // ── Multi-tenancy ─────────────────────────────────────────────────────────────────
    [Fact]
    public async Task MultiTenancy_UserBDoesNotSeeUserAsRulesStagingOrConnections()
    {
        var (clientA, userAId) = await ClientWithUserAsync();
        var accountA = await AuthHelper.CreateAccountAsync(clientA, "Cuenta A", "EUR", 500m);
        await CreateRuleAsync(clientA, accountA, autoCommit: false);
        await ConnectGmailAsync(clientA);
        await SeedConnectionAsync(userAId, accountA);
        ProviderServes(FixtureMessage("msg-tenancy", OperationDay));
        await clientA.PostAsync("/api/email-import/sync", null);

        (await clientA.GetFromJsonAsync<List<RuleDto>>("/api/email-import/rules"))
            .Should().HaveCount(1);

        var clientB = StubbedFactory.CreateClient();
        await AuthHelper.RegisterAndLoginAsync(clientB, "EUR");

        (await clientB.GetFromJsonAsync<List<RuleDto>>("/api/email-import/rules")).Should().BeEmpty();
        (await clientB.GetFromJsonAsync<InboxPageDto>("/api/email-import/inbox"))!.Total.Should().Be(0);
        (await clientB.GetFromJsonAsync<List<ConnectionStatusDto>>("/api/email-import/connections"))
            .Should().BeEmpty();
    }

    [Fact]
    public async Task MultiTenancy_UpdatingAForeignRuleIsRefusedWithRuleNotFound()
    {
        var (clientA, _) = await ClientWithUserAsync();
        var accountA = await AuthHelper.CreateAccountAsync(clientA, "Cuenta A", "EUR", 500m);
        var ruleIdA = await CreateRuleAsync(clientA, accountA, autoCommit: false);

        var (clientB, _) = await ClientWithUserAsync();
        var accountB = await AuthHelper.CreateAccountAsync(clientB, "Cuenta B", "EUR", 100m);

        var response = await clientB.PutAsJsonAsync($"/api/email-import/rules/{ruleIdA}", new
        {
            Name = "Secuestrada",
            AdapterKey = "generic.regex",
            Priority = 0,
            IsEnabled = true,
            AccountId = accountB,
            CategoryId = (Guid?)null,
            TransactionType = 1,
            Currency = "EUR",
            ExchangeRate = (decimal?)null,
            FromAddress = (string?)null,
            SubjectContains = (string?)null,
            BodyRegex = (string?)null,
            FieldPatternsJson = ReadFixture(FixtureRules),
            AutoCommit = false
        });

        // RulesEndpoints maps every ErrorOr to BadRequest, so a foreign id is a 400 whose
        // BODY carries the code — not a 404.
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync()).Should().Contain("EmailImport.RuleNotFound");

        // And the rule was not modified.
        var list = (await clientA.GetFromJsonAsync<List<RuleDto>>("/api/email-import/rules"))!;
        list.Should().ContainSingle();
        list![0].Name.Should().Be("Banco Ejemplo");
    }

    [Fact]
    public async Task MultiTenancy_DeletingAForeignRuleIsRefused()
    {
        var (clientA, _) = await ClientWithUserAsync();
        var accountA = await AuthHelper.CreateAccountAsync(clientA, "Cuenta A", "EUR", 500m);
        var ruleIdA = await CreateRuleAsync(clientA, accountA, autoCommit: false);

        var (clientB, _) = await ClientWithUserAsync();
        var response = await clientB.DeleteAsync($"/api/email-import/rules/{ruleIdA}");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync()).Should().Contain("EmailImport.RuleNotFound");
        (await clientA.GetFromJsonAsync<List<RuleDto>>("/api/email-import/rules"))
            .Should().HaveCount(1);
    }

    [Fact]
    public async Task MultiTenancy_ApprovingAForeignPendingRowIsRefused()
    {
        var (clientA, userAId) = await ClientWithUserAsync();
        var pendingIdA = await StageOnePendingRowAsync(clientA, userAId);

        var (clientB, userBId) = await ClientWithUserAsync();
        var response = await clientB.PostAsJsonAsync(
            $"/api/email-import/inbox/{pendingIdA}/approve", new { });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync()).Should().Contain("EmailImport.PendingNotFound");
        (await CountTransactionsAsync(userAId)).Should().Be(0);
        (await CountTransactionsAsync(userBId)).Should().Be(0);
    }

    [Fact]
    public async Task MultiTenancy_DismissingAForeignPendingRowIsRefused()
    {
        var (clientA, userAId) = await ClientWithUserAsync();
        var pendingIdA = await StageOnePendingRowAsync(clientA, userAId);

        var (clientB, _) = await ClientWithUserAsync();
        var response = await clientB.PostAsync($"/api/email-import/inbox/{pendingIdA}/dismiss", null);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync()).Should().Contain("EmailImport.PendingNotFound");

        // A's row is untouched and still reviewable.
        var page = (await clientA.GetFromJsonAsync<InboxPageDto>("/api/email-import/inbox"))!;
        page.Total.Should().Be(1);
    }

    [Fact]
    public async Task MultiTenancy_WorkerSyncStoresEachMessageUnderItsOwnOwner()
    {
        // GetEnabledAsync is the ONE deliberate exception to the tenancy rule (the worker has
        // no HttpContext). What must still hold is that every staging row is written under
        // its own connection's owner.
        var (clientA, userAId) = await ClientWithUserAsync();
        var accountA = await AuthHelper.CreateAccountAsync(clientA, "Cuenta A", "EUR", 500m);
        await CreateRuleAsync(clientA, accountA, autoCommit: false);
        await SeedConnectionAsync(userAId, accountA);

        var (clientB, userBId) = await ClientWithUserAsync();
        var accountB = await AuthHelper.CreateAccountAsync(clientB, "Cuenta B", "EUR", 500m);
        await CreateRuleAsync(clientB, accountB, autoCommit: false);
        await SeedConnectionAsync(userBId, accountB);

        ProviderServes(FixtureMessage("msg-owner-a", OperationDay));
        await clientA.PostAsync("/api/email-import/sync", null);

        await using var db = OpenDb();
        var owner = await db.ExecuteScalarAsync<Guid>(
            """SELECT "UserId" FROM "PendingEmailTransactions" WHERE "MessageId" = 'msg-owner-a'""");

        owner.Should().Be(userAId);
        // B's sync must not have picked up A's message.
        (await clientB.GetFromJsonAsync<InboxPageDto>("/api/email-import/inbox"))!.Total.Should().Be(0);
    }

    [Fact]
    public async Task Sync_ConnectionStatusReportsTheCursorWasAdvancedAndNoError()
    {
        var (client, userId) = await ClientWithUserAsync();
        var accountId = await CreateAccountForFixtureAsync(client);
        await CreateRuleAsync(client, accountId, autoCommit: false);
        await SeedConnectionAsync(userId, accountId);
        ProviderServes(FixtureMessage("msg-cursor", OperationDay));

        await client.PostAsync("/api/email-import/sync", null);

        await using var db = OpenDb();
        var cursor = await db.ExecuteScalarAsync<string?>(
            """SELECT "SyncCursor" FROM "EmailImportConnections" WHERE "UserId" = @UserId""",
            new { UserId = userId });
        // The cursor is persisted exactly once, at the end of the batch.
        cursor.Should().Be("cursor-tras-el-sincronismo");

        var status = (await client.GetFromJsonAsync<List<ConnectionStatusDto>>(
            "/api/email-import/connections"))!.Single();
        status.LastError.Should().BeNull();
        status.LastSyncAt.Should().NotBeNull();
    }

    private sealed record LoginResult(string Token, string RefreshToken);
    private sealed record IdResponse(Guid Id);
    private sealed record MeResponse(Guid Id);

    private sealed record EmailAuthorizationUriDto(string Uri, string State);

    /// <summary>
    /// The connection status DTO. <c>Provider</c> is an INT on the wire — BudgieJsonContext
    /// does not use JsonStringEnumConverter, so enums travel as numbers (0 = Gmail).
    /// </summary>
    private sealed record ConnectionStatusDto(
        int Provider, string Email, bool SyncEnabled,
        decimal AutoCommitMinConfidence, DateTime? LastSyncAt,
        string? LastError, int LastSkippedCount);

    private sealed record RuleDto(
        Guid Id, string Name, string AdapterKey, int Priority, bool IsEnabled,
        Guid AccountId, int TransactionType, string Currency, bool AutoCommit);

    private sealed record PreviewDto(
        string PlainText,
        Dictionary<string, string?> Fields,
        Dictionary<string, decimal> FieldConfidence,
        decimal Confidence,
        List<string> Explanation,
        string? SkipReason);
}
