using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Dapper;
using FluentAssertions;
using Npgsql;
using Xunit;

namespace Budgie.IntegrationTests;

/// <summary>
/// Slice 1 (§2.1): <c>PUT /api/email-import/connections/{provider}/threshold</c>. Before this the
/// auto-commit threshold was a column with no route to change it, so the settings slider had to
/// be read-only. These tests cover the route contract against real PostgreSQL: it persists, the
/// value survives a read-back, and a bad value is a 400 rather than a 500.
/// </summary>
public class EmailImportThresholdEndpointTests : IClassFixture<BudgieWebApplicationFactory>
{
    private readonly BudgieWebApplicationFactory _factory;

    public EmailImportThresholdEndpointTests(BudgieWebApplicationFactory factory) => _factory = factory;

    /// <summary>
    /// Registers a fresh user, logs in and returns the client. There is no "who am I" endpoint,
    /// so the id comes from the registration response.
    /// </summary>
    private async Task<(HttpClient Client, Guid UserId)> ClientWithUserAsync()
    {
        var client = _factory.CreateClient();
        var email = $"threshold_{Guid.NewGuid():N}@example.com";

        var registered = await client.PostAsJsonAsync("/api/users/register",
            new { Email = email, Password = "Password123!", Name = "Threshold User" });
        var userId = (await registered.Content.ReadFromJsonAsync<IdResponse>())!.Id;

        var login = await client.PostAsJsonAsync("/api/users/login",
            new { Email = email, Password = "Password123!" });
        var token = (await login.Content.ReadFromJsonAsync<LoginResult>())!.Token;
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        // Sin el onboarding de preferencias, un usuario recién registrado recibe 403 en las
        // rutas de dominio (no es un problema de autorización de la ruta probada).
        await client.PostAsJsonAsync("/api/users/settings/setup", new
        {
            MainCurrency = "EUR",
            DefaultCreditConsumptionMode = 1,
            Language = "es"
        });

        return (client, userId);
    }

    /// <summary>
    /// Seeds a connection directly, because there is no way to create one over HTTP without
    /// completing the whole OAuth dance — and the route under test only makes sense once one
    /// exists.
    /// </summary>
    private static async Task SeedConnectionAsync(string connectionString, Guid userId, decimal threshold)
    {
        await using var db = new NpgsqlConnection(connectionString);
        db.Open();
        await db.ExecuteAsync(
            """
            INSERT INTO "EmailImportConnections"
                ("Id","UserId","Provider","Email","EncryptedCredentials","SyncEnabled","AutoCommitMinConfidence","LastSkippedCount")
            VALUES (@Id, @UserId, 0, 'usuario@banco.es', 'v1:bm9uY2U=:Y2lwaGVy:dGFn', TRUE, @Threshold, 0)
            """,
            new { Id = Guid.NewGuid(), UserId = userId, Threshold = threshold });
    }

    private static async Task<decimal?> ThresholdFromApiAsync(HttpClient client)
    {
        var response = await client.GetAsync("/api/email-import/connections");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var connections = await response.Content.ReadFromJsonAsync<List<JsonElement>>();
        return connections?.Count > 0
            ? connections![0].GetProperty("autoCommitMinConfidence").GetDecimal()
            : null;
    }

    [Fact]
    public async Task Put_PersistsTheThreshold_AndTheNextReadReturnsIt()
    {
        var (client, userId) = await ClientWithUserAsync();
        await SeedConnectionAsync(_factory.GetContainerConnectionString(), userId, 0.85m);

        // Precondición explícita: el read-back tiene que empezar en el valor sembrado, o la
        // aserción de abajo no probaría que el PUT hizo nada.
        (await ThresholdFromApiAsync(client)).Should().Be(0.85m);

        var put = await client.PutAsJsonAsync(
            "/api/email-import/connections/gmail/threshold", new { threshold = 0.93m });

        put.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await ThresholdFromApiAsync(client)).Should().Be(0.93m,
            "the threshold must survive the round-trip: this is the whole point of the slice");
    }

    [Fact]
    public async Task Put_PersistsAtTheBoundaries()
    {
        var (client, userId) = await ClientWithUserAsync();
        await SeedConnectionAsync(_factory.GetContainerConnectionString(), userId, 0.85m);

        foreach (var boundary in new[] { 0m, 1m })
        {
            await client.PutAsJsonAsync(
                "/api/email-import/connections/gmail/threshold", new { threshold = boundary });

            (await ThresholdFromApiAsync(client)).Should().Be(boundary,
                $"{boundary} is inside [0, 1] and must be accepted");
        }
    }

    [Fact]
    public async Task Put_RoundsToTwoDecimals_AsTheDomainDoes()
    {
        var (client, userId) = await ClientWithUserAsync();
        await SeedConnectionAsync(_factory.GetContainerConnectionString(), userId, 0.85m);

        await client.PutAsJsonAsync(
            "/api/email-import/connections/gmail/threshold", new { threshold = 0.937m });

        // EmailConnection.NormalizeThreshold rounds to 2 decimals. Asserting it here documents
        // the behaviour instead of letting someone discover it when their slider reads back a
        // different number than the one they set.
        (await ThresholdFromApiAsync(client)).Should().Be(0.94m);
    }

    [Theory]
    [InlineData(1.5)]
    [InlineData(-0.1)]
    [InlineData(2)]
    public async Task Put_OutOfRangeValue_IsRejectedWith400AndChangesNothing(decimal threshold)
    {
        var (client, userId) = await ClientWithUserAsync();
        await SeedConnectionAsync(_factory.GetContainerConnectionString(), userId, 0.85m);

        var response = await client.PutAsJsonAsync(
            "/api/email-import/connections/gmail/threshold", new { threshold });

        // The domain throws ArgumentException for a value outside [0, 1]. Without the handler's
        // catch this would surface as a 500 — which is what the catch exists to prevent.
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await ThresholdFromApiAsync(client)).Should().Be(0.85m,
            "a rejected threshold must not leave the connection half-updated");
    }

    [Fact]
    public async Task Put_RequiresAuthorization()
    {
        using var anonymous = _factory.CreateClient();
        anonymous.DefaultRequestHeaders.Authorization = null;

        var response = await anonymous.PutAsJsonAsync(
            "/api/email-import/connections/gmail/threshold", new { threshold = 0.5m });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Put_UnknownProvider_IsRejected()
    {
        var (client, _) = await ClientWithUserAsync();

        var response = await client.PutAsJsonAsync(
            "/api/email-import/connections/yahoo/threshold", new { threshold = 0.5m });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Put_WithoutAConnectedMailbox_IsNotFound()
    {
        var (client, _) = await ClientWithUserAsync();

        var response = await client.PutAsJsonAsync(
            "/api/email-import/connections/gmail/threshold", new { threshold = 0.5m });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync()).Should().Contain("EmailImport.NotConnected");
    }

    private sealed record LoginResult(string Token, string RefreshToken);

    private sealed record IdResponse(Guid Id);
}
