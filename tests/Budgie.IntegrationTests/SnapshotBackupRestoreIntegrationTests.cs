using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Xunit;

namespace Budgie.IntegrationTests;

public class SnapshotBackupRestoreIntegrationTests : IClassFixture<BudgieWebApplicationFactory>
{
    private readonly HttpClient _client;

    public SnapshotBackupRestoreIntegrationTests(BudgieWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
        factory.EnsureDatabaseCreated();
        AuthHelper.RegisterAndLoginAsync(_client).GetAwaiter().GetResult();
    }

    [Fact]
    public async Task ExportTransactions_Csv_ReturnsCsvContent()
    {
        var accountId = AuthHelper.CreateAccountAsync(_client, "CSVAcc", "USD", 100m).GetAwaiter().GetResult();
        await AuthHelper.CreateExpenseWithDebtAsync(_client, accountId, Guid.NewGuid(), 50m, 30m, 30m);

        var response = await _client.GetAsync("/api/finances/export/transactions?format=csv");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType.Should().Be("text/csv");

        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("Date,Account,Category,Type,Amount");
    }

    [Fact]
    public async Task ExportSnapshot_ThenRestore_IsRoundTrippable()
    {
        var accountId = AuthHelper.CreateAccountAsync(_client, "SnapAcc", "USD", 500m).GetAwaiter().GetResult();
        var personId = AuthHelper.CreatePersonAsync(_client, "Snapshot Person").GetAwaiter().GetResult();
        await AuthHelper.CreateExpenseWithDebtAsync(_client, accountId, personId, 100m, 60m, 60m);

        var export = await _client.GetAsync("/api/finances/export/snapshot");
        export.StatusCode.Should().Be(HttpStatusCode.OK);
        var snapshot = await export.Content.ReadFromJsonAsync<SnapshotData>();
        snapshot!.Accounts.Should().ContainSingle();
        snapshot.Transactions.Should().NotBeEmpty();

        // Echo the snapshot back verbatim for a lossless round-trip
        var rawJson = await export.Content.ReadAsStringAsync();
        var restore = await _client.PostAsync("/api/finances/export/snapshot",
            new StringContent($"{{\"snapshot\":{rawJson}}}", System.Text.Encoding.UTF8, "application/json"));
        restore.StatusCode.Should().Be(HttpStatusCode.OK);

        // Verify data survived the restore
        var accounts = await _client.GetFromJsonAsync<List<AccountResponse>>("/api/finances/accounts");
        accounts!.Should().ContainSingle(a => a.Id == accountId);
    }

    private record SnapshotData(string Version, DateTime ExportedAt, List<AccountItem> Accounts,
        List<CategoryItem> Categories, List<TransactionItem> Transactions, List<object> Participants,
        List<object> Repayments, List<object> RecurringTransactions, List<object> Goals);
    private record AccountItem(Guid Id, string Name);
    private record CategoryItem(Guid Id, string Name);
    private record TransactionItem(Guid Id, Guid AccountId);
    private record AccountResponse(Guid Id, string Name);
}
