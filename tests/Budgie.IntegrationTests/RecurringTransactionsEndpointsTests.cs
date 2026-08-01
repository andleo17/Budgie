using System.Net;
using System.Net.Http.Json;
using Budgie.Modules.Finances.Application.Accounts.CreateAccount;
using Budgie.Shared.Enums;
using FluentAssertions;
using Xunit;

namespace Budgie.IntegrationTests;

public class RecurringTransactionsEndpointsTests : IClassFixture<BudgieWebApplicationFactory>
{
    private readonly HttpClient _client;
    private string _token = string.Empty;
    private Guid _accountId;

    public RecurringTransactionsEndpointsTests(BudgieWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
        factory.EnsureDatabaseCreated();
    }

    private async Task AuthenticateAsync()
    {
        if (!string.IsNullOrEmpty(_token)) return;

        var email = $"recurring_{Guid.NewGuid()}@example.com";
        await _client.PostAsJsonAsync("/api/users/register", new { Email = email, Password = "Password123!", Name = "Test" });
        var login = await _client.PostAsJsonAsync("/api/users/login", new { Email = email, Password = "Password123!" });
        var result = await login.Content.ReadFromJsonAsync<LoginResult>();
        _token = result!.Token;
        _client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _token);
        await _client.PostAsJsonAsync("/api/users/settings/setup", new { MainCurrency = "USD", DefaultCreditConsumptionMode = 1 });

        var acc = await _client.PostAsJsonAsync("/api/finances/accounts",
            new CreateAccountRequest("RecurringAcc", "USD", 1000m, 1, null));
        var accResult = await acc.Content.ReadFromJsonAsync<IdResponse>();
        _accountId = accResult!.Id;
    }

    [Fact]
    public async Task CreateRecurringTransaction_ShouldReturn201()
    {
        await AuthenticateAsync();

        var response = await _client.PostAsJsonAsync("/api/finances/transactions/templates", new
        {
            Name = "Monthly Rent",
            AccountId = _accountId,
            Amount = 1000m,
            Currency = "USD",
            ExchangeRate = 1.0m,
            Type = 1,
            Installments = 12,
            IsInterestFree = true,
            Applicability = 1,
            Frequency = 3
        });
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var result = await response.Content.ReadFromJsonAsync<IdResponse>();
        result!.Id.Should().NotBeEmpty();
    }

    [Fact]
    public async Task CreateRecurringTransaction_Manual_ShouldReturn201()
    {
        await AuthenticateAsync();

        var response = await _client.PostAsJsonAsync("/api/finances/transactions/templates", new
        {
            Name = "Manual Bill",
            AccountId = _accountId,
            Amount = 50m,
            Currency = "USD",
            ExchangeRate = 1.0m,
            Type = 1,
            Installments = 1,
            IsInterestFree = true,
            Applicability = 2,
            Frequency = 3
        });
        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task GetRecurringTransactions_ShouldReturnList()
    {
        await AuthenticateAsync();

        var response = await _client.GetAsync("/api/finances/transactions/templates");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetRecurringTransactionById_ShouldReturnTemplate()
    {
        await AuthenticateAsync();

        var create = await _client.PostAsJsonAsync("/api/finances/transactions/templates", new
        {
            Name = "Find Me",
            AccountId = _accountId,
            Amount = 200m,
            Currency = "USD",
            ExchangeRate = 1.0m,
            Type = 1,
            Installments = 6,
            IsInterestFree = true,
            Applicability = 1,
            Frequency = 3,
            EndDate = DateTime.UtcNow.AddMonths(6)
        });
        var created = await create.Content.ReadFromJsonAsync<IdResponse>();

        var response = await _client.GetAsync($"/api/finances/transactions/templates/{created!.Id}");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetRecurringTransactionById_NotFound_ShouldReturn400()
    {
        await AuthenticateAsync();

        var response = await _client.GetAsync($"/api/finances/transactions/templates/{Guid.NewGuid()}");
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task UpdateRecurringTransaction_ShouldReturn204()
    {
        await AuthenticateAsync();

        var create = await _client.PostAsJsonAsync("/api/finances/transactions/templates", new
        {
            Name = "Old Template",
            AccountId = _accountId,
            Amount = 100m,
            Currency = "USD",
            ExchangeRate = 1.0m,
            Type = 1,
            Installments = 1,
            IsInterestFree = true,
            Applicability = 2,
            Frequency = 3
        });
        var created = await create.Content.ReadFromJsonAsync<IdResponse>();

        var update = await _client.PutAsJsonAsync($"/api/finances/transactions/templates/{created!.Id}", new
        {
            Id = created.Id,
            Name = "Updated Template",
            AccountId = _accountId,
            Amount = 150m,
            Currency = "USD",
            ExchangeRate = 1.0m,
            Type = 1,
            Installments = 2,
            IsInterestFree = true,
            Applicability = 2,
            Frequency = 3
        });
        update.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task DeleteRecurringTransaction_ShouldReturn204()
    {
        await AuthenticateAsync();

        var create = await _client.PostAsJsonAsync("/api/finances/transactions/templates", new
        {
            Name = "TempToDelete",
            AccountId = _accountId,
            Amount = 50m,
            Currency = "USD",
            ExchangeRate = 1.0m,
            Type = 1,
            Installments = 1,
            IsInterestFree = true,
            Applicability = 2,
            Frequency = 3
        });
        var created = await create.Content.ReadFromJsonAsync<IdResponse>();

        var delete = await _client.DeleteAsync($"/api/finances/transactions/templates/{created!.Id}");
        delete.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task DeleteRecurringTransaction_NotFound_ShouldReturn400()
    {
        await AuthenticateAsync();

        var response = await _client.DeleteAsync($"/api/finances/transactions/templates/{Guid.NewGuid()}");
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task ApplyRecurringTransaction_ShouldReturn200()
    {
        await AuthenticateAsync();

        var create = await _client.PostAsJsonAsync("/api/finances/transactions/templates", new
        {
            Name = "Apply Me",
            AccountId = _accountId,
            Amount = 100m,
            Currency = "USD",
            ExchangeRate = 1.0m,
            Type = 1,
            Installments = 1,
            IsInterestFree = true,
            Applicability = 2,
            Frequency = 3
        });
        var created = await create.Content.ReadFromJsonAsync<IdResponse>();

        var apply = await _client.PostAsync($"/api/finances/transactions/templates/{created!.Id}/apply", null);
        apply.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ApplyAllDueRecurringTransactions_ShouldReturn200()
    {
        await AuthenticateAsync();

        var response = await _client.PostAsync("/api/finances/transactions/templates/apply-all-due", null);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetDueRecurringTransactions_ShouldReturnList()
    {
        await AuthenticateAsync();

        var response = await _client.GetAsync("/api/finances/transactions/templates/due");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    private record LoginResult(string Token);
}
