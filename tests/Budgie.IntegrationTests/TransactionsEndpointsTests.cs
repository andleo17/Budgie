using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Xunit;

namespace Budgie.IntegrationTests;

public class TransactionsEndpointsTests : IClassFixture<BudgieWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly Guid _accountId;
    private readonly Guid _personId;

    public TransactionsEndpointsTests(BudgieWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
        factory.EnsureDatabaseCreated();
        AuthHelper.RegisterAndLoginAsync(_client).GetAwaiter().GetResult();
        _accountId = AuthHelper.CreateAccountAsync(_client, "TxAcc", "USD", 1000m).GetAwaiter().GetResult();
        _personId = AuthHelper.CreatePersonAsync(_client, "Maria").GetAwaiter().GetResult();
    }

    [Fact]
    public async Task CreateTransaction_ShouldReturn201()
    {
        var response = await _client.PostAsJsonAsync("/api/finances/transactions", new
        {
            AccountId = _accountId,
            Amount = 100m,
            Currency = "USD",
            ExchangeRate = 1.0m,
            Date = DateTime.UtcNow,
            Type = 1,
            Notes = "Test transaction"
        });
        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task GetTransactions_ShouldReturnList()
    {
        await _client.PostAsJsonAsync("/api/finances/transactions", new
        {
            AccountId = _accountId, Amount = 50m, Currency = "USD", ExchangeRate = 1m,
            Date = DateTime.UtcNow, Type = 1, Notes = "Get me"
        });

        var response = await _client.GetAsync("/api/finances/transactions");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var paged = await response.Content.ReadFromJsonAsync<PagedTransactions>();
        paged!.Items.Should().NotBeEmpty();
    }

    [Fact]
    public async Task DeleteTransaction_ShouldReturn204()
    {
        var create = await _client.PostAsJsonAsync("/api/finances/transactions", new
        {
            AccountId = _accountId, Amount = 50m, Currency = "USD", ExchangeRate = 1m,
            Date = DateTime.UtcNow, Type = 1, Notes = "Delete me"
        });
        var created = await create.Content.ReadFromJsonAsync<IdResponse>();

        var delete = await _client.DeleteAsync($"/api/finances/transactions/{created!.Id}");
        delete.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task BulkRepay_ShouldClearDebt()
    {
        // Expense de 50 con 30 a crédito (deuda de 30)
        await AuthHelper.CreateExpenseWithDebtAsync(_client, _accountId, _personId, 50m, 30m, 30m);

        var debts = await _client.GetFromJsonAsync<List<DebtDto>>($"/api/finances/persons/{_personId}/debts");
        debts!.Should().ContainSingle();
        debts[0].remainingAmount.Should().Be(30m);

        // Pagar la deuda completa
        var repay = await _client.PostAsJsonAsync("/api/finances/transactions/bulk-repay", new
        {
            AccountId = _accountId,
            Amount = 30m,
            Currency = "USD",
            ExchangeRate = 1m,
            Date = DateTime.UtcNow,
            Notes = "Pago total",
            Debts = new[]
            {
                new { ParticipantId = debts[0].participantId, Amount = 30m, TransactionId = debts[0].transactionId }
            }
        });
        repay.StatusCode.Should().Be(HttpStatusCode.Created);

        var debtsAfter = await _client.GetFromJsonAsync<List<DebtDto>>($"/api/finances/persons/{_personId}/debts");
        debtsAfter!.Should().BeEmpty();
    }

    [Fact]
    public async Task GetTransactionsSummary_ShouldReturnMainCurrency()
    {
        var response = await _client.GetAsync("/api/finances/transactions/summary");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var summary = await response.Content.ReadFromJsonAsync<TxSummary>();
        summary!.MainCurrency.Should().Be("USD");
    }

    private record IdResponse(Guid Id);
    private record PagedTransactions(List<TransactionItem> Items, int TotalCount);
    private record TransactionItem(Guid Id);
    private record DebtDto(Guid participantId, Guid transactionId, decimal remainingAmount);
    private record TxSummary(string MainCurrency, List<CurrencySummary> Currencies);
    private record CurrencySummary(decimal Income, decimal Expenses, decimal Balance, string Currency);
}
