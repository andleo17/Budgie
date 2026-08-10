using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Xunit;

namespace Budgie.IntegrationTests;

public class CreditDebtFlowTests : IClassFixture<BudgieWebApplicationFactory>
{
    private readonly HttpClient _client;
    private string _token = string.Empty;
    private Guid _accountId;

    public CreditDebtFlowTests(BudgieWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
        factory.EnsureDatabaseCreated();
    }

    private async Task AuthenticateAsync()
    {
        if (!string.IsNullOrEmpty(_token)) return;

        var email = $"debt_{Guid.NewGuid()}@example.com";
        await _client.PostAsJsonAsync("/api/users/register", new { Email = email, Password = "Password123!", Name = "Test" });
        var login = await _client.PostAsJsonAsync("/api/users/login", new { Email = email, Password = "Password123!" });
        var result = await login.Content.ReadFromJsonAsync<LoginResult>();
        _token = result!.Token;
        _client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _token);
        await _client.PostAsJsonAsync("/api/users/settings/setup", new { MainCurrency = "USD", DefaultCreditConsumptionMode = 1 });

        var accResponse = await _client.PostAsJsonAsync("/api/finances/accounts",
            new { Name = "DebtAcc", Currency = "USD", InitialBalance = 1000m, Type = 1 });
        var acc = await accResponse.Content.ReadFromJsonAsync<IdResponse>();
        _accountId = acc!.Id;
    }

    [Fact]
    public async Task ExpenseOnCredit_ThenPayment_ClearsDebt_AndRepaymentCarriesPerson()
    {
        await AuthenticateAsync();

        var person = await _client.PostAsJsonAsync("/api/relationships/persons", new { Name = "Maria" });
        var personResult = await person.Content.ReadFromJsonAsync<IdResponse>();
        var personId = personResult!.Id;

        // Expense of 50 where Maria owes 30 (on credit)
        var expense = await _client.PostAsJsonAsync("/api/finances/transactions", new
        {
            AccountId = _accountId,
            Amount = 50m,
            Currency = "USD",
            ExchangeRate = 1.0m,
            Date = DateTime.UtcNow,
            Type = 1,
            Notes = "Gasto de 50",
            Participants = new[]
            {
                new { PersonId = personId, ContributedAmount = 30m, RemainingAmount = 30m }
            }
        });
        expense.StatusCode.Should().Be(HttpStatusCode.Created);

        var debtsBefore = await _client.GetFromJsonAsync<List<DebtDto>>($"/api/finances/persons/{personId}/debts");
        debtsBefore!.Should().ContainSingle();
        debtsBefore[0].remainingAmount.Should().Be(30m);

        var repay = await _client.PostAsJsonAsync("/api/finances/transactions/bulk-repay", new
        {
            AccountId = _accountId,
            Amount = 30m,
            Currency = "USD",
            ExchangeRate = 1.0m,
            Date = DateTime.UtcNow,
            Notes = "Pago",
            Debts = new[]
            {
                new { ParticipantId = debtsBefore[0].participantId, Amount = 30m, TransactionId = debtsBefore[0].transactionId }
            }
        });
        repay.StatusCode.Should().Be(HttpStatusCode.Created);

        var debtsAfter = await _client.GetFromJsonAsync<List<DebtDto>>($"/api/finances/persons/{personId}/debts");
        debtsAfter!.Should().BeEmpty();

        var summary = await _client.GetFromJsonAsync<SummaryDto>($"/api/finances/persons/{personId}/summary");
        summary!.totalPending.Should().Be(0m);
        summary.totalRepaid.Should().Be(30m);

        var tx = await _client.GetFromJsonAsync<PagedDto>($"/api/finances/transactions?personId={personId}");
        var repaymentTx = tx!.items.Single(i => i.type == 4);
        repaymentTx.notes.Should().Be("Pago");
        repaymentTx.participants.Should().ContainSingle();
        repaymentTx.participants![0].personId.Should().Be(personId);
    }

    private record LoginResult(string Token);
    private record DebtDto(Guid participantId, Guid transactionId, decimal remainingAmount);
    private record SummaryDto(int totalTransactions, decimal totalContributed, decimal totalRepaid, decimal totalPending);
    private record PagedDto(List<TxDto> items);
    private record TxDto(Guid id, string? notes, int type, List<ParticipantDto>? participants);
    private record ParticipantDto(Guid personId);
}
