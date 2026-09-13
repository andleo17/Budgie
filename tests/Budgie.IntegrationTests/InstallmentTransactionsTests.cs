using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Xunit;

namespace Budgie.IntegrationTests;

public class InstallmentTransactionsTests : IClassFixture<BudgieWebApplicationFactory>
{
    private readonly HttpClient _client;

    public InstallmentTransactionsTests(BudgieWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
        factory.EnsureDatabaseCreated();
        AuthHelper.RegisterAndLoginAsync(_client).GetAwaiter().GetResult();
    }

    [Fact]
    public async Task CreateTransaction_InInstallments_ShouldCreateMultipleRowsWithGroupId()
    {
        var accountId = await AuthHelper.CreateAccountAsync(_client, $"CuotasAcc_{Guid.NewGuid():N}", "USD", 0m);

        var date = DateTime.UtcNow;
        var response = await _client.PostAsJsonAsync("/api/finances/transactions", new
        {
            AccountId = accountId,
            Amount = 120m,
            Currency = "USD",
            ExchangeRate = 1m,
            Date = date,
            Type = 1,
            Notes = "Compra en 3 cuotas",
            Installments = 3,
            IsInterestFree = true
        });
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await response.Content.ReadFromJsonAsync<IdResponse>();
        created!.Id.Should().NotBeEmpty();

        // GroupId is returned as Id when installments>1
        var list = await _client.GetFromJsonAsync<PagedTransactions>($"/api/finances/transactions?accountIds={accountId}&pageSize=100");
        list!.Items.Should().HaveCount(3);
        list.Items.Should().OnlyContain(t => t.InstallmentGroupId != null);
        var groupIds = list.Items.Select(t => t.InstallmentGroupId).Distinct().ToList();
        groupIds.Should().HaveCount(1);
        groupIds[0].Should().Be(created.Id);

        // Verify installment numbers and amounts sum to 120
        list.Items.Select(t => t.InstallmentNumber).OrderBy(n => n).Should().Equal(1, 2, 3);
        list.Items.Select(t => t.TotalInstallments).Should().OnlyContain(n => n == 3);
        list.Items.Sum(t => t.Amount).Should().Be(120m);
        list.Items.Should().Contain(t => t.Amount == 40m);

        // Dates should be spaced by months
        var dates = list.Items.OrderBy(t => t.InstallmentNumber).Select(t => t.Date).ToList();
        dates[1].Month.Should().Be(dates[0].AddMonths(1).Month);
        dates[2].Month.Should().Be(dates[0].AddMonths(2).Month);
    }

    [Fact]
    public async Task CreateTransaction_InInstallments_WithRounding_ShouldSumExactly()
    {
        var accountId = await AuthHelper.CreateAccountAsync(_client, $"CuotasRound_{Guid.NewGuid():N}", "USD", 0m);

        var response = await _client.PostAsJsonAsync("/api/finances/transactions", new
        {
            AccountId = accountId,
            CategoryId = (Guid?)null,
            Amount = 100m,
            Currency = "USD",
            ExchangeRate = 1m,
            Date = DateTime.UtcNow,
            Type = 1,
            Installments = 3,
            IsInterestFree = true
        });
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var list = await _client.GetFromJsonAsync<PagedTransactions>($"/api/finances/transactions?accountIds={accountId}&pageSize=100");
        list!.Items.Sum(t => t.Amount).Should().Be(100m);
        // 33.33,33.33,33.34
        list.Items.OrderBy(t => t.InstallmentNumber).Select(t => t.Amount).Should().Equal(33.33m, 33.33m, 33.34m);
    }

    [Fact]
    public async Task CreateTransaction_InInstallments_ShouldUpdateBalanceByTotal()
    {
        var accountId = await AuthHelper.CreateAccountAsync(_client, $"CuotasBal_{Guid.NewGuid():N}", "USD", 1000m);

        // Get initial balance
        var accountsBefore = await _client.GetFromJsonAsync<List<AccountDto>>("/api/finances/accounts");
        var before = accountsBefore!.First(a => a.Id == accountId).Balance;

        await _client.PostAsJsonAsync("/api/finances/transactions", new
        {
            AccountId = accountId,
            Amount = 90m,
            Currency = "USD",
            ExchangeRate = 1m,
            Date = DateTime.UtcNow,
            Type = 1,
            Installments = 3,
            IsInterestFree = true
        });

        var accountsAfter = await _client.GetFromJsonAsync<List<AccountDto>>("/api/finances/accounts");
        var after = accountsAfter!.First(a => a.Id == accountId).Balance;
        // Expense of 90 should deduct 90 regardless of installments
        after.Should().Be(before - 90m);
    }

    [Fact]
    public async Task CreateTransaction_InInstallments_WithParticipant_ShouldProrateAndCreateChildLoans()
    {
        var accountId = await AuthHelper.CreateAccountAsync(_client, $"CuotasPart_{Guid.NewGuid():N}", "USD", 0m);
        var personId = await AuthHelper.CreatePersonAsync(_client, $"Person_{Guid.NewGuid():N}");

        var response = await _client.PostAsJsonAsync("/api/finances/transactions", new
        {
            AccountId = accountId,
            Amount = 90m,
            Currency = "USD",
            ExchangeRate = 1m,
            Date = DateTime.UtcNow,
            Type = 1,
            Installments = 3,
            Participants = new[]
            {
                new { PersonId = personId, ContributedAmount = 30m, RemainingAmount = 30m, IsTrackingOnly = false, DestinationAccountId = (Guid?)null }
            },
            IsInterestFree = true
        });
        var body = await response.Content.ReadAsStringAsync();
        response.StatusCode.Should().Be(HttpStatusCode.Created, body);

        var debts = await _client.GetFromJsonAsync<List<DebtDto>>($"/api/finances/persons/{personId}/debts");
        // Should have 3 child loan debts (one per installment) each 10 = total 30
        debts.Should().HaveCount(3);
        debts!.Sum(d => d.RemainingAmount).Should().Be(30m);
        debts.Select(d => d.RemainingAmount).Should().OnlyContain(a => a == 10m);
    }

    [Fact]
    public async Task CreateTransaction_SingleInstallment_ShouldNotCreateGroup()
    {
        var accountId = await AuthHelper.CreateAccountAsync(_client, $"Single_{Guid.NewGuid():N}", "USD", 500m);

        var response = await _client.PostAsJsonAsync("/api/finances/transactions", new
        {
            AccountId = accountId,
            Amount = 50m,
            Currency = "USD",
            ExchangeRate = 1m,
            Date = DateTime.UtcNow,
            Type = 1,
            Installments = 1
        });
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await response.Content.ReadFromJsonAsync<IdResponse>();

        var list = await _client.GetFromJsonAsync<PagedTransactions>($"/api/finances/transactions?accountIds={accountId}&pageSize=100");
        var tx = list!.Items.First(t => t.Id == created!.Id);
        tx.InstallmentGroupId.Should().BeNull();
        tx.InstallmentNumber.Should().Be(1);
        tx.TotalInstallments.Should().Be(1);
        tx.Amount.Should().Be(50m);
    }

    [Fact]
    public async Task UpdateInstallment_ShouldPreserveGroupReference()
    {
        var accountId = await AuthHelper.CreateAccountAsync(_client, $"CuotasEdit_{Guid.NewGuid():N}", "USD", 0m);

        var create = await _client.PostAsJsonAsync("/api/finances/transactions", new
        {
            AccountId = accountId,
            Amount = 120m,
            Currency = "USD",
            ExchangeRate = 1m,
            Date = DateTime.UtcNow,
            Type = 1,
            Notes = "Compra en 3 cuotas",
            Installments = 3,
            IsInterestFree = true
        });
        create.StatusCode.Should().Be(HttpStatusCode.Created);

        var before = await _client.GetFromJsonAsync<PagedTransactions>($"/api/finances/transactions?accountIds={accountId}&pageSize=100");
        var second = before!.Items.OrderBy(t => t.InstallmentNumber).ElementAt(1);
        second.InstallmentGroupId.Should().NotBeNull();
        second.InstallmentNumber.Should().Be(2);
        second.TotalInstallments.Should().Be(3);

        // Ajusta el monto de una sola cuota (el formulario de edición no envía datos de grupo)
        var put = await _client.PutAsJsonAsync($"/api/finances/transactions/{second.Id}", new
        {
            CategoryId = (Guid?)null,
            Amount = 45m,
            Currency = "USD",
            ExchangeRate = 1m,
            Date = second.Date,
            Notes = "Ajuste cuota 2",
            Type = 1,
            Installments = 1
        });
        put.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var after = await _client.GetFromJsonAsync<PagedTransactions>($"/api/finances/transactions?accountIds={accountId}&pageSize=100");
        var edited = after!.Items.First(t => t.Id == second.Id);
        edited.Amount.Should().Be(45m);
        edited.InstallmentGroupId.Should().Be(second.InstallmentGroupId);
        edited.InstallmentNumber.Should().Be(2);
        edited.TotalInstallments.Should().Be(3);
        after.Items.Should().HaveCount(3);
    }

    private record IdResponse(Guid Id);
    private record PagedTransactions(List<TransactionItem> Items, int TotalCount, int Page, int PageSize);
    private record TransactionItem(Guid Id, decimal Amount, Guid? InstallmentGroupId, int? InstallmentNumber, int? TotalInstallments, DateTime Date);
    private record DebtDto(Guid ParticipantId, Guid TransactionId, decimal RemainingAmount);
    private record AccountDto(Guid Id, string Name, decimal Balance, string Currency);
}
