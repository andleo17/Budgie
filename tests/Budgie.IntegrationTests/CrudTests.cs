using System.Net.Http.Json;
using Budgie.Modules.Finances.Application.Accounts;
using Budgie.Modules.Finances.Application.Accounts.CreateAccount;
using Budgie.Modules.Finances.Application.Categories;
using Budgie.Modules.Finances.Application.Transactions;
using Budgie.Modules.Finances.Application.Transactions.GetTransactions;
using Budgie.Modules.Relationships.Application.Persons;
using Budgie.Shared.Enums;
using FluentAssertions;
using Xunit;

namespace Budgie.IntegrationTests;

public class CrudTests : IClassFixture<BudgieWebApplicationFactory>
{
    private readonly HttpClient _client;
    private string _token = string.Empty;

    public CrudTests(BudgieWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
        factory.EnsureDatabaseCreated();
    }

    private async Task AuthenticateAsync()
    {
        if (!string.IsNullOrEmpty(_token)) return;

        var email = $"user_{Guid.NewGuid()}@example.com";
        var registerRequest = new RegisterRequest(email, "Password123!", "Test User");
        await _client.PostAsJsonAsync("/api/users/register", registerRequest);

        var loginRequest = new LoginRequest(email, "Password123!");
        var loginResponse = await _client.PostAsJsonAsync("/api/users/login", loginRequest);
        var authResult = await loginResponse.Content.ReadFromJsonAsync<AuthResponse>();
        _token = authResult!.Token;
        _client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _token);

        // Setup user configuration
        await _client.PostAsJsonAsync("/api/users/settings/setup", new { MainCurrency = "USD", DefaultCreditConsumptionMode = 1 });
    }

    [Fact]
    public async Task Account_Crud_ShouldWork()
    {
        await AuthenticateAsync();

        // Create
        var createRequest = new CreateAccountRequest("Savings", "USD", 1000m, 1, null);
        var createResponse = await _client.PostAsJsonAsync("/api/finances/accounts", createRequest);
        createResponse.EnsureSuccessStatusCode();
        var createResult = await createResponse.Content.ReadFromJsonAsync<IdResponse>();

        // Get By Id
        var getResponse = await _client.GetAsync($"/api/finances/accounts/{createResult!.Id}");
        getResponse.EnsureSuccessStatusCode();
        var account = await getResponse.Content.ReadFromJsonAsync<AccountResponse>();
        account!.Name.Should().Be("Savings");

        // Update
        var updateRequest = new UpdateAccountRequest("Updated Savings", null, null);
        var updateResponse = await _client.PutAsJsonAsync($"/api/finances/accounts/{createResult.Id}", updateRequest);
        updateResponse.EnsureSuccessStatusCode();

        // Verify Update
        account = await (await _client.GetAsync($"/api/finances/accounts/{createResult.Id}")).Content.ReadFromJsonAsync<AccountResponse>();
        account!.Name.Should().Be("Updated Savings");

        // Delete
        var deleteResponse = await _client.DeleteAsync($"/api/finances/accounts/{createResult.Id}");
        deleteResponse.EnsureSuccessStatusCode();

        // Verify Delete
        var getAfterDeleteResponse = await _client.GetAsync($"/api/finances/accounts/{createResult.Id}");
        getAfterDeleteResponse.StatusCode.Should().Be(System.Net.HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Category_Crud_ShouldWork()
    {
        await AuthenticateAsync();

        // Create
        var createRequest = new { Name = "Food", Color = "#FF0000", Icon = "pizza" };
        var createResponse = await _client.PostAsJsonAsync("/api/finances/categories", createRequest);
        createResponse.EnsureSuccessStatusCode();
        var createResult = await createResponse.Content.ReadFromJsonAsync<IdResponse>();

        // List
        var listResponse = await _client.GetAsync("/api/finances/categories");
        listResponse.EnsureSuccessStatusCode();
        var categories = await listResponse.Content.ReadFromJsonAsync<List<CategoryResponse>>();
        categories.Should().Contain(c => c.Id == createResult!.Id && c.Name == "Food");

        // Update
        var updateRequest = new { Name = "Dining", Color = "#00FF00", Icon = "burger" };
        var updateResponse = await _client.PutAsJsonAsync($"/api/finances/categories/{createResult!.Id}", updateRequest);
        updateResponse.EnsureSuccessStatusCode();

        // Delete
        var deleteResponse = await _client.DeleteAsync($"/api/finances/categories/{createResult.Id}");
        deleteResponse.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task Transaction_Crud_ShouldWork()
    {
        await AuthenticateAsync();

        // Setup Account
        var accountId = (await (await _client.PostAsJsonAsync("/api/finances/accounts", new CreateAccountRequest("TransAcc", "USD", 500m, 1, null))).Content.ReadFromJsonAsync<IdResponse>())!.Id;

        // Create
        var createRequest = new 
        { 
            AccountId = accountId, 
            Amount = 50m, 
            Currency = "USD", 
            ExchangeRate = 1.0m, 
            Date = DateTime.UtcNow, 
            Type = 1, // Expense
            Notes = "Lunch" 
        };
        var createResponse = await _client.PostAsJsonAsync("/api/finances/transactions", createRequest);
        createResponse.EnsureSuccessStatusCode();
        var createResult = await createResponse.Content.ReadFromJsonAsync<IdResponse>();

        // List
        var listResponse = await _client.GetAsync("/api/finances/transactions");
        listResponse.EnsureSuccessStatusCode();
        var paged = await listResponse.Content.ReadFromJsonAsync<PagedResponse<TransactionResponse>>();
        paged!.Items.Should().Contain(t => t.Id == createResult!.Id);

        // Update
        var updateRequest = new 
        { 
            Amount = 60m, 
            Currency = "USD", 
            ExchangeRate = 1.0m, 
            Date = DateTime.UtcNow, 
            Notes = "Dinner",
            Type = 1,
            Installments = 1
        };
        var updateResponse = await _client.PutAsJsonAsync($"/api/finances/transactions/{createResult!.Id}", updateRequest);
        updateResponse.EnsureSuccessStatusCode();

        // Delete
        var deleteResponse = await _client.DeleteAsync($"/api/finances/transactions/{createResult.Id}");
        deleteResponse.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task Person_Crud_ShouldWork()
    {
        await AuthenticateAsync();

        // Create
        var createRequest = new { Name = "John Doe" };
        var createResponse = await _client.PostAsJsonAsync("/api/relationships/persons", createRequest);
        createResponse.EnsureSuccessStatusCode();
        var createResult = await createResponse.Content.ReadFromJsonAsync<IdResponse>();

        // List
        var listResponse = await _client.GetAsync("/api/relationships/persons");
        listResponse.EnsureSuccessStatusCode();
        var persons = await listResponse.Content.ReadFromJsonAsync<List<PersonResponse>>();
        persons.Should().Contain(p => p.Id == createResult!.Id && p.Name == "John Doe");

        // Update
        var updateRequest = new { Name = "John Smith" };
        var updateResponse = await _client.PutAsJsonAsync($"/api/relationships/persons/{createResult!.Id}", updateRequest);
        updateResponse.EnsureSuccessStatusCode();

        // Delete
        var deleteResponse = await _client.DeleteAsync($"/api/relationships/persons/{createResult.Id}");
        deleteResponse.EnsureSuccessStatusCode();
    }
}

// Helper Records for Tests
public record IdResponse(Guid Id);
public record UpdateAccountRequest(string Name, decimal? CreditLimit, int? CreditConsumptionMode);
