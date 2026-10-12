using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Dapper;
using FluentAssertions;
using Npgsql;
using Xunit;

namespace Budgie.IntegrationTests;

// Exercises the whole transfer-pair contract against a real PostgreSQL (Testcontainers):
//   - the destination reported by GET must be the other leg, never the row itself
//   - retrying a transfer with the same ExternalUid must not duplicate the money
//   - PUT on one leg must update the pair, including moving the destination account
//   - DELETE on any leg must remove the whole group
public class TransferPairIntegrityTests : IClassFixture<BudgieWebApplicationFactory>
{
    private readonly BudgieWebApplicationFactory _factory;

    public TransferPairIntegrityTests(BudgieWebApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task Get_ReportsTheRealDestination_NotTheSourceAccount()
    {
        var (client, from, to) = await SetupAsync();
        await CreateTransferAsync(client, from, to, 100m, "alquiler");

        var page = await GetTransactionsAsync(client);
        // Scoped to the accounts this test created: /api/finances/transactions does not
        // filter by user (see the multi-tenancy defect reported separately), so a query
        // without filters can return other users' rows.
        var outgoing = page.Where(t => t.AccountId == from && t.Type == 2 && t.Amount == 100m)
            .Should().ContainSingle().Subject;

        outgoing.TransferDestinationAccountId.Should().Be(to,
            "the destination must be the other leg, not the row itself");
        outgoing.TransferDestinationAccountName.Should().Be("Cuenta destino");
    }

    [Fact]
    public async Task Create_WithRepeatedExternalUid_DoesNotDuplicateTheMoney()
    {
        var (client, from, to) = await SetupAsync();
        var body = new { fromAccountId = from, toAccountId = to, amount = 250m, fee = 0m,
                         currency = "PEN", exchangeRate = 1m, date = DateTime.Today,
                         notes = "reintento", externalUid = "email:dup-1" };

        var first = await client.PostAsJsonAsync("/api/finances/transactions/transfer", body);
        first.StatusCode.Should().Be(HttpStatusCode.Created);
        var firstId = (await first.Content.ReadFromJsonAsync<CreatedResult>())!.Id;

        var second = await client.PostAsJsonAsync("/api/finances/transactions/transfer", body);
        second.StatusCode.Should().Be(HttpStatusCode.Created);
        var secondId = (await second.Content.ReadFromJsonAsync<CreatedResult>())!.Id;

        secondId.Should().Be(firstId, "a repeated transfer is a no-op");

        var rows = await GetTransactionsAsync(client);
        rows.Count(t => t.Type == 2 && t.Amount == 250m).Should().Be(1);
        rows.Count(t => t.Type == 0 && t.Amount == 250m).Should().Be(1);

        var fromBalance = await GetBalanceAsync(from);
        fromBalance.Should().Be(1000m - 250m);
    }

    [Fact]
    public async Task Put_OnOneLeg_UpdatesTheWholePair()
    {
        var (client, from, to) = await SetupAsync();
        var transferId = await CreateTransferAsync(client, from, to, 100m, "original");
        var outgoing = (await GetTransactionsAsync(client))
            .Single(t => t.Id == transferId);

        var response = await client.PutAsJsonAsync($"/api/finances/transactions/{transferId}",
            new
            {
                categoryId = (Guid?)null,
                amount = 400m,
                currency = "PEN",
                exchangeRate = 1m,
                date = DateTime.Today,
                notes = "corregido",
                type = 2,
                installments = 1
            });
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var rows = await GetTransactionsAsync(client);
        rows.Where(t => t.Id == outgoing.Id || t.Id == outgoing.LinkedTransactionId)
            .Should().HaveCount(1, "the link points at the group id, not at a row");

        rows.Count(t => t.Type == 2 && t.Amount == 400m).Should().Be(1);
        rows.Count(t => t.Type == 0 && t.Amount == 400m).Should().Be(1);
        rows.Should().NotContain(t => t.Amount == 100m);

        (await GetBalanceAsync(from)).Should().Be(1000m - 400m);
        (await GetBalanceAsync(to)).Should().Be(400m);
    }

    [Fact]
    public async Task Put_ChangingDestination_MovesTheIncomeLegAndBothBalances()
    {
        var (client, from, to) = await SetupAsync();
        var third = await CreateAccountAsync(client, "Tercera");
        var transferId = await CreateTransferAsync(client, from, to, 100m, "mover");

        var response = await client.PutAsJsonAsync($"/api/finances/transactions/{transferId}",
            new
            {
                categoryId = (Guid?)null,
                amount = 100m,
                currency = "PEN",
                exchangeRate = 1m,
                date = DateTime.Today,
                notes = "mover",
                type = 2,
                installments = 1,
                transferDestinationAccountId = third
            });
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await GetBalanceAsync(from)).Should().Be(900m);
        (await GetBalanceAsync(to)).Should().Be(0m, "the old destination gave the money back");
        (await GetBalanceAsync(third)).Should().Be(100m, "the new destination received it");

        var rows = await GetTransactionsAsync(client);
        rows.Count(t => t.Type == 0 && t.Amount == 100m).Should().Be(1);
        rows.Single(t => t.Type == 0 && t.Amount == 100m).AccountId.Should().Be(third);
    }

    [Fact]
    public async Task Delete_OnAnyLeg_RemovesTheWholeGroup()
    {
        var (client, from, to) = await SetupAsync();
        var transferId = await CreateTransferAsync(client, from, to, 100m, "borrar");
        var outgoing = (await GetTransactionsAsync(client)).Single(t => t.Id == transferId);
        var incomingLegId = await GetIncomeLegIdAsync(outgoing);

        // delete from the INCOMING side: the old code removed only that row
        var response = await client.DeleteAsync($"/api/finances/transactions/{incomingLegId}");
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var rows = await GetTransactionsAsync(client);
        rows.Should().NotContain(t => t.Id == transferId, "the outgoing leg must go too");
        rows.Should().NotContain(t => t.Id == incomingLegId);

        (await GetBalanceAsync(from)).Should().Be(1000m, "the origin got its money back");
        (await GetBalanceAsync(to)).Should().Be(0m);
    }

    // --- helpers -------------------------------------------------------------

    private async Task<(HttpClient Client, Guid From, Guid To)> SetupAsync()
    {
        var client = _factory.CreateClient();
        var email = $"tr_{Guid.NewGuid():N}@example.com";
        await RegisterAsync(client, email);

        var from = await CreateAccountAsync(client, "Cuenta origen", 1000m);
        var to = await CreateAccountAsync(client, "Cuenta destino");

        return (client, from, to);
    }

    private static async Task RegisterAsync(HttpClient client, string email)
    {
        var register = await client.PostAsJsonAsync("/api/users/register",
            new { Email = email, Password = "Password123!", Name = "Transfer Tester" });
        register.EnsureSuccessStatusCode();

        var login = await client.PostAsJsonAsync("/api/users/login",
            new { Email = email, Password = "Password123!" });
        login.EnsureSuccessStatusCode();
        var result = await login.Content.ReadFromJsonAsync<LoginResult>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", result!.Token);

        await client.PostAsJsonAsync("/api/users/settings/setup",
            new { MainCurrency = "PEN", DefaultCreditConsumptionMode = 1 });
    }

    private static async Task<Guid> CreateAccountAsync(HttpClient client, string name, decimal initial = 0m)
    {
        var response = await client.PostAsJsonAsync("/api/finances/accounts",
            new { name, currency = "PEN", type = 0, initialBalance = initial });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<CreatedAccountResult>())!.Id;
    }

    private static async Task<Guid> CreateTransferAsync(
        HttpClient client, Guid from, Guid to, decimal amount, string notes)
    {
        var response = await client.PostAsJsonAsync("/api/finances/transactions/transfer",
            new
            {
                fromAccountId = from,
                toAccountId = to,
                amount,
                fee = 0m,
                currency = "PEN",
                exchangeRate = 1m,
                date = DateTime.Today,
                notes,
                externalUid = $"email:{Guid.NewGuid():N}"
            });
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        return (await response.Content.ReadFromJsonAsync<CreatedResult>())!.Id;
    }

    private static async Task<List<TransactionRow>> GetTransactionsAsync(HttpClient client)
    {
        var response = await client.GetAsync("/api/finances/transactions?pageSize=100");
        response.EnsureSuccessStatusCode();
        var page = await response.Content.ReadFromJsonAsync<PagedTransactions>();
        return page!.Items;
    }

    /// <summary>
    /// A transfer's two legs share a group id that is not the Id of any row, so the
    /// incoming leg has to be located through the database.
    /// </summary>
    private async Task<Guid> GetIncomeLegIdAsync(TransactionRow outgoing)
    {
        await using var conn = new NpgsqlConnection(_factory.GetContainerConnectionString());
        await conn.OpenAsync();
        return await conn.ExecuteScalarAsync<Guid>(
            @"SELECT t.""Id"" FROM ""Transactions"" t
              WHERE t.""LinkedTransactionId"" = @Group AND t.""Type"" = 0",
            new { Group = outgoing.LinkedTransactionId });
    }

    private async Task<decimal> GetBalanceAsync(Guid accountId)
    {
        await using var conn = new NpgsqlConnection(_factory.GetContainerConnectionString());
        await conn.OpenAsync();
        return await conn.ExecuteScalarAsync<decimal>(
            @"SELECT ""Balance"" FROM ""Accounts"" WHERE ""Id"" = @Id", new { Id = accountId });
    }

    private record LoginResult(string Token);
    private record CreatedResult(Guid Id);
    private record CreatedAccountResult(Guid Id);
    private record TransactionRow(Guid Id, Guid AccountId, decimal Amount, int Type,
        Guid? LinkedTransactionId, Guid? TransferDestinationAccountId, string? TransferDestinationAccountName);
    private record PagedTransactions(List<TransactionRow> Items);
}
