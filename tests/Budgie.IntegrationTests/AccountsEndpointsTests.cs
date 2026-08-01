using System.Net;
using System.Net.Http.Json;
using Budgie.Modules.Finances.Application.Accounts;
using Budgie.Modules.Finances.Application.Accounts.CreateAccount;
using Budgie.Shared.Enums;
using FluentAssertions;
using Xunit;

namespace Budgie.IntegrationTests;

public class AccountsEndpointsTests : IClassFixture<BudgieWebApplicationFactory>
{
    private readonly HttpClient _client;
    private string _token = string.Empty;

    public AccountsEndpointsTests(BudgieWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
        factory.EnsureDatabaseCreated();
    }

    private async Task AuthenticateAsync()
    {
        if (!string.IsNullOrEmpty(_token)) return;

        var email = $"accounts_{Guid.NewGuid()}@example.com";
        await _client.PostAsJsonAsync("/api/users/register", new { Email = email, Password = "Password123!", Name = "Test" });
        var loginResponse = await _client.PostAsJsonAsync("/api/users/login", new { Email = email, Password = "Password123!" });
        var loginResult = await loginResponse.Content.ReadFromJsonAsync<LoginResult>();
        _token = loginResult!.Token;
        _client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _token);

        var setupResponse = await _client.PostAsJsonAsync("/api/users/settings/setup", new { MainCurrency = "USD", DefaultCreditConsumptionMode = 1 });
        setupResponse.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task CreateAccount_ShouldReturn201()
    {
        await AuthenticateAsync();

        var response = await _client.PostAsJsonAsync("/api/finances/accounts",
            new CreateAccountRequest("Checking", "USD", 1000m, 1, null));

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var result = await response.Content.ReadFromJsonAsync<IdResponse>();
        result.Should().NotBeNull();
        result!.Id.Should().NotBeEmpty();
    }

    [Fact]
    public async Task CreateAccount_WithCreditCard_ShouldReturn201()
    {
        await AuthenticateAsync();

        var response = await _client.PostAsJsonAsync("/api/finances/accounts",
            new { Name = "Visa", Currency = "USD", InitialBalance = 0m, Type = 3, CreditLimit = 5000m });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task GetAccounts_WithoutAuth_ShouldReturn401()
    {
        var client = new BudgieWebApplicationFactory().CreateClient();

        var response = await client.GetAsync("/api/finances/accounts");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetAccounts_ShouldReturnList()
    {
        await AuthenticateAsync();

        await _client.PostAsJsonAsync("/api/finances/accounts",
            new CreateAccountRequest("Acc1", "USD", 100m, 1, null));
        await _client.PostAsJsonAsync("/api/finances/accounts",
            new CreateAccountRequest("Acc2", "EUR", 200m, 1, null));

        var response = await _client.GetAsync("/api/finances/accounts");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var accounts = await response.Content.ReadFromJsonAsync<List<AccountResponse>>();
        accounts.Should().NotBeNull();
        accounts!.Count.Should().BeGreaterThanOrEqualTo(2);
    }

    [Fact]
    public async Task GetAccountById_ShouldReturnAccount()
    {
        await AuthenticateAsync();

        var createResponse = await _client.PostAsJsonAsync("/api/finances/accounts",
            new CreateAccountRequest("MyAccount", "USD", 500m, 1, null));
        var created = await createResponse.Content.ReadFromJsonAsync<IdResponse>();

        var response = await _client.GetAsync($"/api/finances/accounts/{created!.Id}");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var account = await response.Content.ReadFromJsonAsync<AccountResponse>();
        account!.Name.Should().Be("MyAccount");
        account.Balance.Should().Be(500m);
    }

    [Fact]
    public async Task GetAccountById_NotFound_ShouldReturn404()
    {
        await AuthenticateAsync();

        var response = await _client.GetAsync($"/api/finances/accounts/{Guid.NewGuid()}");
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task UpdateAccount_ShouldReturn204()
    {
        await AuthenticateAsync();

        var createResponse = await _client.PostAsJsonAsync("/api/finances/accounts",
            new CreateAccountRequest("OldName", "USD", 100m, 1, null));
        var created = await createResponse.Content.ReadFromJsonAsync<IdResponse>();

        var updateResponse = await _client.PutAsJsonAsync($"/api/finances/accounts/{created!.Id}",
            new { Name = "NewName", CreditLimit = (decimal?)null, CreditConsumptionMode = (int?)null });
        updateResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var getResponse = await _client.GetAsync($"/api/finances/accounts/{created.Id}");
        var account = await getResponse.Content.ReadFromJsonAsync<AccountResponse>();
        account!.Name.Should().Be("NewName");
    }

    [Fact]
    public async Task UpdateAccount_NotFound_ShouldReturn400()
    {
        await AuthenticateAsync();

        var response = await _client.PutAsJsonAsync($"/api/finances/accounts/{Guid.NewGuid()}",
            new { Name = "Ghost", CreditLimit = (decimal?)null, CreditConsumptionMode = (int?)null });
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task DeleteAccount_ShouldReturn204()
    {
        await AuthenticateAsync();

        var createResponse = await _client.PostAsJsonAsync("/api/finances/accounts",
            new CreateAccountRequest("ToDelete", "USD", 0m, 1, null));
        var created = await createResponse.Content.ReadFromJsonAsync<IdResponse>();

        var deleteResponse = await _client.DeleteAsync($"/api/finances/accounts/{created!.Id}");
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task DeleteAccount_NotFound_ShouldReturn400()
    {
        await AuthenticateAsync();

        var response = await _client.DeleteAsync($"/api/finances/accounts/{Guid.NewGuid()}");
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    private record LoginResult(string Token);
}
