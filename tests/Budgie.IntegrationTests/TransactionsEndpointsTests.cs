using System.Net;
using System.Net.Http.Json;
using Budgie.Modules.Finances.Application.Accounts;
using Budgie.Modules.Finances.Application.Accounts.CreateAccount;
using Budgie.Modules.Finances.Application.Transactions;
using Budgie.Modules.Finances.Application.Transactions.GetTransactions;
using Budgie.Shared.Enums;
using FluentAssertions;
using Xunit;

namespace Budgie.IntegrationTests;

public class TransactionsEndpointsTests : IClassFixture<BudgieWebApplicationFactory>
{
    private readonly HttpClient _client;
    private string _token = string.Empty;
    private Guid _accountId;

    public TransactionsEndpointsTests(BudgieWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
        factory.EnsureDatabaseCreated();
    }

    private async Task AuthenticateAsync()
    {
        if (!string.IsNullOrEmpty(_token)) return;

        var email = $"tx_{Guid.NewGuid()}@example.com";
        await _client.PostAsJsonAsync("/api/users/register", new { Email = email, Password = "Password123!", Name = "Test" });
        var login = await _client.PostAsJsonAsync("/api/users/login", new { Email = email, Password = "Password123!" });
        var result = await login.Content.ReadFromJsonAsync<LoginResult>();
        _token = result!.Token;
        _client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _token);
        await _client.PostAsJsonAsync("/api/users/settings/setup", new { MainCurrency = "USD", DefaultCreditConsumptionMode = 1 });

        var accResponse = await _client.PostAsJsonAsync("/api/finances/accounts",
            new CreateAccountRequest("TxAcc", "USD", 1000m, 1, null));
        var acc = await accResponse.Content.ReadFromJsonAsync<IdResponse>();
        _accountId = acc!.Id;
    }

    [Fact]
    public async Task CreateTransaction_ShouldReturn201()
    {
        await AuthenticateAsync();

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
        var result = await response.Content.ReadFromJsonAsync<IdResponse>();
        result!.Id.Should().NotBeEmpty();
    }

    [Fact]
    public async Task CreateTransaction_WithoutCategory_ShouldReturn201()
    {
        await AuthenticateAsync();

        var response = await _client.PostAsJsonAsync("/api/finances/transactions", new
        {
            AccountId = _accountId,
            Amount = 50m,
            Currency = "USD",
            ExchangeRate = 1.0m,
            Date = DateTime.UtcNow,
            Type = 1
        });
        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task CreateTransaction_Income_ShouldIncreaseBalance()
    {
        await AuthenticateAsync();

        await _client.PostAsJsonAsync("/api/finances/transactions", new
        {
            AccountId = _accountId,
            Amount = 200m,
            Currency = "USD",
            ExchangeRate = 1.0m,
            Date = DateTime.UtcNow,
            Type = 0
        });

        var getAcc = await _client.GetAsync($"/api/finances/accounts/{_accountId}");
        var account = await getAcc.Content.ReadFromJsonAsync<AccountResponse>();
        account!.Balance.Should().Be(1200m);
    }

    [Fact]
    public async Task CreateTransaction_WithParticipants_ShouldReturn201()
    {
        await AuthenticateAsync();

        var person = await _client.PostAsJsonAsync("/api/relationships/persons", new { Name = "Friend" });
        var personResult = await person.Content.ReadFromJsonAsync<IdResponse>();

        var response = await _client.PostAsJsonAsync("/api/finances/transactions", new
        {
            AccountId = _accountId,
            Amount = 300m,
            Currency = "USD",
            ExchangeRate = 1.0m,
            Date = DateTime.UtcNow,
            Type = 4,
            Notes = "Loan to friend",
            Participants = new[]
            {
                new { PersonId = personResult!.Id, ContributedAmount = 0m, RemainingAmount = 300m }
            }
        });
        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task GetTransactions_ShouldReturnPagedResult()
    {
        await AuthenticateAsync();

        var response = await _client.GetAsync("/api/finances/transactions");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var paged = await response.Content.ReadFromJsonAsync<PagedResponse<TransactionResponse>>();
        paged.Should().NotBeNull();
    }

    [Fact]
    public async Task GetTransactions_WithFilters_ShouldWork()
    {
        await AuthenticateAsync();

        var response = await _client.GetAsync($"/api/finances/transactions?accountIds={_accountId}&transactionTypes=1&page=1&pageSize=10");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetTransactionById_ShouldReturnTransaction()
    {
        await AuthenticateAsync();

        var create = await _client.PostAsJsonAsync("/api/finances/transactions", new
        {
            AccountId = _accountId,
            Amount = 75m,
            Currency = "USD",
            ExchangeRate = 1.0m,
            Date = DateTime.UtcNow,
            Type = 1,
            Notes = "Get me"
        });
        var created = await create.Content.ReadFromJsonAsync<IdResponse>();

        var response = await _client.GetAsync($"/api/finances/transactions/{created!.Id}");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var tx = await response.Content.ReadFromJsonAsync<TransactionResponse>();
        tx!.Amount.Should().Be(75m);
    }

    [Fact]
    public async Task GetTransactionById_NotFound_ShouldReturn404()
    {
        await AuthenticateAsync();

        var response = await _client.GetAsync($"/api/finances/transactions/{Guid.NewGuid()}");
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetTransactionsSummary_ShouldReturnSummary()
    {
        await AuthenticateAsync();

        var response = await _client.GetAsync("/api/finances/transactions/summary");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task UpdateTransaction_ShouldReturn204()
    {
        await AuthenticateAsync();

        var create = await _client.PostAsJsonAsync("/api/finances/transactions", new
        {
            AccountId = _accountId,
            Amount = 100m,
            Currency = "USD",
            ExchangeRate = 1.0m,
            Date = DateTime.UtcNow,
            Type = 1,
            Notes = "Before update"
        });
        var created = await create.Content.ReadFromJsonAsync<IdResponse>();

        var update = await _client.PutAsJsonAsync($"/api/finances/transactions/{created!.Id}", new
        {
            Amount = 150m,
            Currency = "USD",
            ExchangeRate = 1.0m,
            Date = DateTime.UtcNow,
            Type = 1,
            Notes = "After update",
            Installments = 1,
            CategoryId = (Guid?)null
        });
        update.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task DeleteTransaction_ShouldReturn204()
    {
        await AuthenticateAsync();

        var create = await _client.PostAsJsonAsync("/api/finances/transactions", new
        {
            AccountId = _accountId,
            Amount = 50m,
            Currency = "USD",
            ExchangeRate = 1.0m,
            Date = DateTime.UtcNow,
            Type = 1,
            Notes = "Delete me"
        });
        var created = await create.Content.ReadFromJsonAsync<IdResponse>();

        var delete = await _client.DeleteAsync($"/api/finances/transactions/{created!.Id}");
        delete.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Transfer_ShouldReturn201()
    {
        await AuthenticateAsync();

        var toAccResponse = await _client.PostAsJsonAsync("/api/finances/accounts",
            new CreateAccountRequest("TargetAcc", "USD", 0m, 1, null));
        var toAcc = await toAccResponse.Content.ReadFromJsonAsync<IdResponse>();

        var response = await _client.PostAsJsonAsync("/api/finances/transactions/transfer", new
        {
            FromAccountId = _accountId,
            ToAccountId = toAcc!.Id,
            Amount = 200m,
            Fee = 0m,
            Currency = "USD",
            ExchangeRate = 1.0m,
            Date = DateTime.UtcNow,
            Notes = "Transfer test"
        });
        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task Transfer_WithFee_ShouldWork()
    {
        await AuthenticateAsync();

        var toAcc = await _client.PostAsJsonAsync("/api/finances/accounts",
            new CreateAccountRequest("TargetAcc2", "USD", 0m, 1, null));
        var toAccResult = await toAcc.Content.ReadFromJsonAsync<IdResponse>();

        var response = await _client.PostAsJsonAsync("/api/finances/transactions/transfer", new
        {
            FromAccountId = _accountId,
            ToAccountId = toAccResult!.Id,
            Amount = 100m,
            Fee = 5m,
            Currency = "USD",
            ExchangeRate = 1.0m,
            Date = DateTime.UtcNow
        });
        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task RepayLoan_ShouldReturn201()
    {
        await AuthenticateAsync();

        var person = await _client.PostAsJsonAsync("/api/relationships/persons", new { Name = "Borrower" });
        var personResult = await person.Content.ReadFromJsonAsync<IdResponse>();

        var loan = await _client.PostAsJsonAsync("/api/finances/transactions", new
        {
            AccountId = _accountId,
            Amount = 500m,
            Currency = "USD",
            ExchangeRate = 1.0m,
            Date = DateTime.UtcNow,
            Type = 3,
            Notes = "Loan given",
            Participants = new[]
            {
                new { PersonId = personResult!.Id, ContributedAmount = 0m, RemainingAmount = 500m }
            }
        });
        var loanResult = await loan.Content.ReadFromJsonAsync<IdResponse>();

        var repay = await _client.PostAsJsonAsync("/api/finances/transactions/repay-loan", new
        {
            OriginalTransactionId = loanResult!.Id,
            Amount = 200m,
            Currency = "USD",
            ExchangeRate = 1.0m,
            Date = DateTime.UtcNow,
            Notes = "Partial repayment"
        });
        repay.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task BulkRepay_ShouldReturn201()
    {
        await AuthenticateAsync();

        var person = await _client.PostAsJsonAsync("/api/relationships/persons", new { Name = "Debtor" });
        var personResult = await person.Content.ReadFromJsonAsync<IdResponse>();

        var loan1 = await _client.PostAsJsonAsync("/api/finances/transactions", new
        {
            AccountId = _accountId,
            Amount = 300m,
            Currency = "USD",
            ExchangeRate = 1.0m,
            Date = DateTime.UtcNow,
            Type = 3,
            Participants = new[]
            {
                new { PersonId = personResult!.Id, ContributedAmount = 0m, RemainingAmount = 300m }
            }
        });
        var loan1Result = await loan1.Content.ReadFromJsonAsync<IdResponse>();

        var response = await _client.PostAsJsonAsync("/api/finances/transactions/bulk-repay", new
        {
            AccountId = _accountId,
            Amount = 300m,
            Currency = "USD",
            ExchangeRate = 1.0m,
            Date = DateTime.UtcNow,
            Debts = new[]
            {
                new { ParticipantId = personResult.Id, Amount = 300m, TransactionId = loan1Result!.Id }
            }
        });
        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task Unauthenticated_ShouldReturn401()
    {
        var client = new BudgieWebApplicationFactory().CreateClient();

        var responses = await Task.WhenAll(
            client.GetAsync("/api/finances/transactions"),
            client.PostAsJsonAsync("/api/finances/transactions", new { }),
            client.DeleteAsync($"/api/finances/transactions/{Guid.NewGuid()}")
        );

        responses.Should().AllSatisfy(r => r.StatusCode.Should().Be(HttpStatusCode.Unauthorized));
    }

    [Fact]
    public async Task TransactionOnNonExistentAccount_ShouldReturn400()
    {
        await AuthenticateAsync();

        var response = await _client.PostAsJsonAsync("/api/finances/transactions", new
        {
            AccountId = Guid.NewGuid(),
            Amount = 100m,
            Currency = "USD",
            ExchangeRate = 1.0m,
            Date = DateTime.UtcNow,
            Type = 1
        });
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    private record LoginResult(string Token);
}
