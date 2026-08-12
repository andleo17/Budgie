using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Xunit;

namespace Budgie.IntegrationTests;

/// <summary>
/// Reproduces the original bug: expense on credit -> payment -> debt must clear,
/// summary must reflect repaid amount, and repayment must be filterable by person.
/// Uses a blank Testcontainer PostgreSQL, so state is isolated per test.
/// </summary>
public class CreditDebtFlowTests : IClassFixture<BudgieWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly Guid _accountId;
    private readonly Guid _personId;

    public CreditDebtFlowTests(BudgieWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
        factory.EnsureDatabaseCreated();
        AuthHelper.RegisterAndLoginAsync(_client).GetAwaiter().GetResult();
        _accountId = AuthHelper.CreateAccountAsync(_client, "DebtAcc", "USD", 1000m).GetAwaiter().GetResult();
        _personId = AuthHelper.CreatePersonAsync(_client, "Maria").GetAwaiter().GetResult();
    }

    [Fact]
    public async Task ExpenseOnCredit_ThenPayment_ClearsDebt_AndSummaryReflectsRepayment()
    {
        // Gasto de 50 donde Maria debe 30 (a crédito)
        await AuthHelper.CreateExpenseWithDebtAsync(_client, _accountId, _personId, 50m, 30m, 30m);

        var debts = await _client.GetFromJsonAsync<List<DebtDto>>($"/api/finances/persons/{_personId}/debts");
        debts!.Should().ContainSingle();
        debts[0].remainingAmount.Should().Be(30m);

        // Pago de la deuda
        var repay = await _client.PostAsJsonAsync("/api/finances/transactions/bulk-repay", new
        {
            AccountId = _accountId,
            Amount = 30m,
            Currency = "USD",
            ExchangeRate = 1m,
            Date = DateTime.UtcNow,
            Notes = "Pago",
            Debts = new[]
            {
                new { ParticipantId = debts[0].participantId, Amount = 30m, TransactionId = debts[0].transactionId }
            }
        });
        repay.StatusCode.Should().Be(HttpStatusCode.Created);

        // Deuda liquidada
        var debtsAfter = await _client.GetFromJsonAsync<List<DebtDto>>($"/api/finances/persons/{_personId}/debts");
        debtsAfter!.Should().BeEmpty();

        // Summary refleja el pago
        var summary = await _client.GetFromJsonAsync<SummaryDto>($"/api/finances/persons/{_personId}/summary");
        summary!.TotalPending.Should().Be(0m);
        summary.TotalRepaid.Should().Be(30m);
        summary.TotalContributed.Should().Be(30m);
    }

    [Fact]
    public async Task ExpenseOnCredit_ShouldCreateVirtualDebtChild_AndAssociateParticipant()
    {
        await AuthHelper.CreateExpenseWithDebtAsync(_client, _accountId, _personId, 50m, 30m, 30m);

        var transactions = await _client.GetFromJsonAsync<PagedTransactions>($"/api/finances/transactions?accountIds={_accountId}");
        transactions!.Items.Should().NotBeEmpty();

        var parent = transactions.Items.Single(t => t.type == 1 && t.parentTransactionId == null);
        parent.participants.Should().ContainSingle(p => p.personId == _personId && p.remainingAmount == 30m);

        var virtualDebt = transactions.Items.SingleOrDefault(t => t.type == 3 && t.parentTransactionId != null);
        virtualDebt.Should().NotBeNull("la deuda virtual (transacción hija Loan) debe listarse");
        virtualDebt!.participants.Should().ContainSingle(p => p.personId == _personId);

        transactions.Items.IndexOf(virtualDebt).Should().BeLessThan(
            transactions.Items.IndexOf(parent), "la deuda virtual debe aparecer por encima de la transacción original");
    }

    [Fact]
    public async Task PartialPayment_ShouldReduceDebtRemaining()
    {
        await AuthHelper.CreateExpenseWithDebtAsync(_client, _accountId, _personId, 50m, 30m, 30m);

        var debts = await _client.GetFromJsonAsync<List<DebtDto>>($"/api/finances/persons/{_personId}/debts");
        debts!.Should().ContainSingle();

        // Pago parcial de 10
        var repay = await _client.PostAsJsonAsync("/api/finances/transactions/bulk-repay", new
        {
            AccountId = _accountId,
            Amount = 10m,
            Currency = "USD",
            ExchangeRate = 1m,
            Date = DateTime.UtcNow,
            Notes = "Abono",
            Debts = new[]
            {
                new { ParticipantId = debts[0].participantId, Amount = 10m, TransactionId = debts[0].transactionId }
            }
        });
        repay.StatusCode.Should().Be(HttpStatusCode.Created);

        var debtsAfter = await _client.GetFromJsonAsync<List<DebtDto>>($"/api/finances/persons/{_personId}/debts");
        debtsAfter!.Should().ContainSingle();
        debtsAfter[0].remainingAmount.Should().Be(20m);
    }

    [Fact]
    public async Task Repayment_ShouldBeFilterableByPerson()
    {
        await AuthHelper.CreateExpenseWithDebtAsync(_client, _accountId, _personId, 50m, 30m, 30m);

        var debts = await _client.GetFromJsonAsync<List<DebtDto>>($"/api/finances/persons/{_personId}/debts");
        await _client.PostAsJsonAsync("/api/finances/transactions/bulk-repay", new
        {
            AccountId = _accountId,
            Amount = 30m,
            Currency = "USD",
            ExchangeRate = 1m,
            Date = DateTime.UtcNow,
            Notes = "Pago filtrable",
            Debts = new[]
            {
                new { ParticipantId = debts![0].participantId, Amount = 30m, TransactionId = debts[0].transactionId }
            }
        });

        var transactions = await _client.GetFromJsonAsync<PagedTransactions>(
            $"/api/finances/transactions?personId={_personId}");
        transactions!.Items.Should().NotBeEmpty();
    }

    private record DebtDto(Guid participantId, Guid transactionId, decimal remainingAmount);
    private record SummaryDto(decimal TotalContributed, decimal TotalRepaid, decimal TotalPending);
    private record PagedTransactions(List<TransactionDto> Items, int TotalCount);
    private record TransactionDto(Guid Id, int type, Guid? parentTransactionId, List<ParticipantDto> participants);
    private record ParticipantDto(Guid personId, decimal remainingAmount);
}
