using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Dapper;
using FluentAssertions;
using Npgsql;
using Xunit;

namespace Budgie.IntegrationTests;

// End-to-end tests for the Money Manager (.mmbak) import flow against a real
// PostgreSQL (Testcontainers) database. The fixture is a small anonimized backup
// (tests/Budgie.IntegrationTests/Fixtures/mm-import-test.mmbak) with:
//   2 accounts (acc1 Checking PEN, acc2 Credit PEN) + 1 placeholder skipped
//   5 active categories (3 parents + 2 children) + 1 deleted skipped
//   10 transactions: 3 income (1 initial balance), 5 expense, 1 transfer pair (3+4)
//   1 budget (cat4 = 300) and 1 monthly recurring income.
public class ImportMoneyManagerEndpointsTests : IClassFixture<BudgieWebApplicationFactory>
{
    private readonly BudgieWebApplicationFactory _factory;
    private readonly string _fixturePath;

    public ImportMoneyManagerEndpointsTests(BudgieWebApplicationFactory factory)
    {
        _factory = factory;
        _fixturePath = Path.Combine(AppContext.BaseDirectory, "Fixtures", "mm-import-test.mmbak");
        File.Exists(_fixturePath).Should().BeTrue("fixture must be copied to output");
    }

    // --- Preview ---

    [Fact]
    public async Task Preview_UploadsFixture_ReturnsCountsWithoutWriting()
    {
        var client = _factory.CreateClient();
        await RegisterAsync(client, $"preview_{Guid.NewGuid():N}@example.com");

        var response = await client.PostAsync("/api/import/money-manager/preview", BuildMultipart());
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var preview = await response.Content.ReadFromJsonAsync<PreviewResponse>();
        preview.Should().NotBeNull();

        preview!.Accounts.Should().HaveCount(2);
        preview.SkippedPlaceholders.Should().Be(1);
        preview.Categories.Should().HaveCount(5);
        preview.Budgets.Should().ContainSingle(b => b.CategoryUid == "cat4" && b.Amount == 300m);
        preview.Recurrings.Should().ContainSingle();

        // 3 income (2 + 1 initial) + 5 expense + 1 transfer = 9; 1 mirror deduped
        preview.Summary.Income.Should().Be(3);
        preview.Summary.Expense.Should().Be(5);
        preview.Summary.Transfer.Should().Be(1);
        preview.Summary.InitialBalance.Should().Be(1);
        preview.Summary.Total.Should().Be(9);
        preview.Summary.DedupedTransfers.Should().Be(1);
        preview.Summary.DateRange.Should().Be("2025-01-01 → 2025-01-12");

        preview.AlreadyImported.Should().BeFalse();
        preview.Errors.Should().BeEmpty();
        preview.FileHash.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Preview_InvalidFile_ReturnsBadRequest()
    {
        var client = _factory.CreateClient();
        await RegisterAsync(client, $"previewbad_{Guid.NewGuid():N}@example.com");

        var content = new MultipartFormDataContent();
        var bytes = new ByteArrayContent([1, 2, 3, 4]);
        bytes.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        content.Add(bytes, "file", "broken.mmbak");

        var response = await client.PostAsync("/api/import/money-manager/preview", content);
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // --- Commit + DB verification + idempotency + multi-tenancy ---

    [Fact]
    public async Task Commit_ThenReimport_AndOverwrite_AreIdempotent()
    {
        var client = _factory.CreateClient();
        var email = $"commit_{Guid.NewGuid():N}@example.com";
        await RegisterAsync(client, email);
        var userId = await GetUserIdAsync(email);

        // First commit
        var commit = await client.PostAsync("/api/import/money-manager/commit?overwrite=false", BuildMultipart());
        commit.StatusCode.Should().Be(HttpStatusCode.OK);
        var first = await commit.Content.ReadFromJsonAsync<CommitResponse>();
        first!.CreatedAccounts.Should().Be(2);
        first.SkippedAccounts.Should().Be(0);
        first.CreatedCategories.Should().Be(5);
        first.CreatedTransactions.Should().Be(9);
        first.SkippedTransactions.Should().Be(0);
        first.CreatedRecurring.Should().Be(1);
        first.FileHash.Should().NotBeNullOrWhiteSpace();

        await VerifyDatabaseAsync(userId, first.FileHash);

        // Re-import same file without overwrite → 409 Conflict
        var reimport = await client.PostAsync("/api/import/money-manager/commit?overwrite=false", BuildMultipart());
        reimport.StatusCode.Should().Be(HttpStatusCode.Conflict);

        // Re-import with overwrite=true → 200, everything skipped (including recurring)
        var overwrite = await client.PostAsync(
            "/api/import/money-manager/commit?overwrite=true", BuildMultipart());
        overwrite.StatusCode.Should().Be(HttpStatusCode.OK);
        var second = await overwrite.Content.ReadFromJsonAsync<CommitResponse>();
        second!.CreatedAccounts.Should().Be(0);
        second.SkippedAccounts.Should().Be(2);
        second.CreatedCategories.Should().Be(0);
        second.SkippedCategories.Should().Be(5);
        second.CreatedTransactions.Should().Be(0);
        second.SkippedTransactions.Should().Be(9);
        second.CreatedRecurring.Should().Be(0); // recurrencias idempotentes por ExternalUid

        // No duplicate rows
        await VerifyDatabaseAsync(userId, first.FileHash);
    }

    [Fact]
    public async Task Commit_SecondUser_DoesNotSeeFirstUsersData()
    {
        var firstClient = _factory.CreateClient();
        await RegisterAsync(firstClient, $"tenant1_{Guid.NewGuid():N}@example.com");
        var commit = await firstClient.PostAsync("/api/import/money-manager/commit?overwrite=false", BuildMultipart());
        commit.StatusCode.Should().Be(HttpStatusCode.OK);

        // Second user: accounts endpoint returns empty and same file is NOT "already imported"
        var secondClient = _factory.CreateClient();
        var email2 = $"tenant2_{Guid.NewGuid():N}@example.com";
        await RegisterAsync(secondClient, email2);

        var accounts = await secondClient.GetFromJsonAsync<List<AccountDto>>("/api/finances/accounts");
        accounts.Should().BeEmpty();

        var preview = await secondClient.PostAsync("/api/import/money-manager/preview", BuildMultipart());
        var previewBody = await preview.Content.ReadFromJsonAsync<PreviewResponse>();
        previewBody!.AlreadyImported.Should().BeFalse();

        // Second user can commit the same file independently
        var secondCommit = await secondClient.PostAsync("/api/import/money-manager/commit?overwrite=false", BuildMultipart());
        secondCommit.StatusCode.Should().Be(HttpStatusCode.OK);
        var secondBody = await secondCommit.Content.ReadFromJsonAsync<CommitResponse>();
        secondBody!.CreatedAccounts.Should().Be(2);
    }

    // --- Helpers ---

    private MultipartFormDataContent BuildMultipart()
    {
        var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(File.ReadAllBytes(_fixturePath));
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        content.Add(fileContent, "file", "mm-import-test.mmbak");
        return content;
    }

    private static async Task RegisterAsync(HttpClient client, string email)
    {
        var response = await client.PostAsJsonAsync("/api/users/register",
            new { Email = email, Password = "Password123!", Name = "Import Tester" });
        response.EnsureSuccessStatusCode();

        var login = await client.PostAsJsonAsync("/api/users/login", new { Email = email, Password = "Password123!" });
        login.EnsureSuccessStatusCode();
        var result = await login.Content.ReadFromJsonAsync<LoginResult>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", result!.Token);

        await client.PostAsJsonAsync("/api/users/settings/setup",
            new { MainCurrency = "PEN", DefaultCreditConsumptionMode = 1 });
    }

    private async Task<Guid> GetUserIdAsync(string email)
    {
        await using var conn = new NpgsqlConnection(_factory.GetContainerConnectionString());
        await conn.OpenAsync();
        return await conn.ExecuteScalarAsync<Guid>(
            @"SELECT ""Id"" FROM ""Users"" WHERE ""Email"" = @Email", new { Email = email });
    }

    private async Task VerifyDatabaseAsync(Guid userId, string fileHash)
    {
        await using var conn = new NpgsqlConnection(_factory.GetContainerConnectionString());
        await conn.OpenAsync();

        // Accounts: 2 with ExternalUid, balances 1320 (acc1) and -45 (acc2)
        var accounts = (await conn.QueryAsync<AccountRow>(
            @"SELECT ""Id"",""ExternalUid"",""Balance"",""Type"" FROM ""Accounts"" WHERE ""UserId"" = @UserId ORDER BY ""ExternalUid""",
            new { UserId = userId })).ToList();
        accounts.Should().HaveCount(2);
        accounts[0].ExternalUid.Should().Be("acc1");
        accounts[0].Balance.Should().Be(1320m);
        accounts[0].Type.Should().Be(0); // Checking
        accounts[1].ExternalUid.Should().Be("acc2");
        accounts[1].Balance.Should().Be(-45m);
        accounts[1].Type.Should().Be(2); // Credit

        // Categories: 5, children resolve ParentId, MonthlyBudget for cat4
        var categories = (await conn.QueryAsync<CategoryRow>(
            @"SELECT ""Id"",""ExternalUid"",""ParentId"",""MonthlyBudget"" FROM ""Categories"" WHERE ""UserId"" = @UserId",
            new { UserId = userId })).ToList();
        categories.Should().HaveCount(5);
        var cat2 = categories.Single(c => c.ExternalUid == "cat2");
        var cat3 = categories.Single(c => c.ExternalUid == "cat3");
        cat3.ParentId.Should().Be(cat2.Id);
        var cat4 = categories.Single(c => c.ExternalUid == "cat4");
        var cat5 = categories.Single(c => c.ExternalUid == "cat5");
        cat5.ParentId.Should().Be(cat4.Id);
        cat4.MonthlyBudget.Should().Be(300m);

        // Transactions: 9 created, 1 initial balance, 1 transfer, orphan category null
        var txs = (await conn.QueryAsync<TransactionRow>(
            @"SELECT ""Id"",""ExternalUid"",""CategoryId"",""IsInitialBalance"",""Type"" FROM ""Transactions"" WHERE ""UserId"" = @UserId",
            new { UserId = userId })).ToList();
        txs.Should().HaveCount(9);
        txs.Count(t => t.IsInitialBalance).Should().Be(1);
        txs.Count(t => t.Type == 2).Should().Be(1); // Transfer
        txs.Single(t => t.ExternalUid == "tx9").CategoryId.Should().BeNull(); // orphan category

        // ImportRuns: exactly 1 for this hash (ImportRun.Create stores the hash lowercase)
        var runs = (await conn.QueryAsync<Guid>(
            @"SELECT ""Id"" FROM ""ImportRuns"" WHERE ""UserId"" = @UserId AND ""FileHash"" = @FileHash",
            new { UserId = userId, FileHash = fileHash.ToLowerInvariant() })).ToList();
        runs.Should().HaveCount(1);

        // RecurringTransactions: exactly 1 with ExternalUid (no duplicates on re-import)
        var recurrings = (await conn.QueryAsync<Guid>(
            @"SELECT ""Id"" FROM ""RecurringTransactions"" WHERE ""UserId"" = @UserId",
            new { UserId = userId })).ToList();
        recurrings.Should().HaveCount(1);
    }

    private record PreviewResponse(
        List<PreviewAccount> Accounts, List<PreviewCategory> Categories, PreviewSummary Summary,
        List<PreviewBudget> Budgets, List<PreviewRecurring> Recurrings, List<string> Warnings,
        List<string> Errors, int SkippedPlaceholders, string FileHash, bool AlreadyImported);
    private record PreviewAccount(string Name, string Type, string Currency, string ExternalUid);
    private record PreviewCategory(string Name, string Type, string? ParentUid, string ExternalUid, decimal? MonthlyBudget);
    private record PreviewSummary(int Income, int Expense, int Transfer, int InitialBalance, int Total, int DedupedTransfers, string DateRange);
    private record PreviewBudget(string CategoryUid, decimal Amount);
    private record PreviewRecurring(string Frequency, string? Notes);

    private record CommitResponse(
        int CreatedAccounts, int SkippedAccounts, int CreatedCategories, int SkippedCategories,
        int CreatedTransactions, int SkippedTransactions, int CreatedRecurring, List<string> Warnings, string FileHash);

    private record LoginResult(string Token, string RefreshToken);
    private record AccountDto(Guid Id, string Name, decimal Balance, string Currency);
    private record AccountRow(Guid Id, string ExternalUid, decimal Balance, int Type);
    private record CategoryRow(Guid Id, string ExternalUid, Guid? ParentId, decimal? MonthlyBudget);
    private record TransactionRow(Guid Id, string ExternalUid, Guid? CategoryId, bool IsInitialBalance, int Type);
}