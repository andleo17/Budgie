using System.Net.Http.Json;
using Budgie.Modules.Finances.Application.Accounts;
using Budgie.Modules.Finances.Presentation.Accounts;
using FluentAssertions;
using Xunit;

namespace Budgie.IntegrationTests;

public class FinanceFlowTests : IClassFixture<BudgieWebApplicationFactory>
{
    private readonly HttpClient _client;

    public FinanceFlowTests(BudgieWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
        factory.EnsureDatabaseCreated();
    }

    [Fact]
    public async Task FullFlow_ShouldWorkCorrectly()
    {
        // 1. Register
        var registerRequest = new RegisterRequest("testuser@example.com", "Password123!", "Test User");
        var registerResponse = await _client.PostAsJsonAsync("/api/users/register", registerRequest);
        registerResponse.EnsureSuccessStatusCode();

        // 2. Login
        var loginRequest = new LoginRequest("testuser@example.com", "Password123!");
        var loginResponse = await _client.PostAsJsonAsync("/api/users/login", loginRequest);
        loginResponse.EnsureSuccessStatusCode();
        var loginResult = await loginResponse.Content.ReadFromJsonAsync<AuthResponse>();
        loginResult.Should().NotBeNull();
        loginResult!.Token.Should().NotBeNullOrEmpty();

        // Set Auth Header
        _client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", loginResult.Token);

        // 3. Setup user configuration
        await _client.PostAsJsonAsync("/api/users/settings/setup", new { MainCurrency = "USD", DefaultCreditConsumptionMode = 1 });

        // 4. Create Account
        var createAccountRequest = new CreateAccountRequest("Savings", "USD", 1000m, 1, null);
        var createResponse = await _client.PostAsJsonAsync("/api/finances/accounts", createAccountRequest);
        createResponse.EnsureSuccessStatusCode();

        // 4. Get Accounts
        var getAccountsResponse = await _client.GetAsync("/api/finances/accounts");
        getAccountsResponse.EnsureSuccessStatusCode();
        var accounts = await getAccountsResponse.Content.ReadFromJsonAsync<List<AccountResponse>>();
        
        accounts.Should().NotBeNull();
        accounts.Should().ContainSingle(a => a.Name == "Savings");
    }
}

// Simple records to match API requests/responses in tests
public record RegisterRequest(string Email, string Password, string Name);
public record LoginRequest(string Email, string Password);
public record AuthResponse(string Token, string Email, string Name);
public record CreateAccountRequest(string Name, string Currency, decimal InitialBalance, int? Type, decimal? CreditLimit);
