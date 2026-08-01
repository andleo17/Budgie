using System.Net;
using System.Net.Http.Json;
using Budgie.Modules.Finances.Application.Accounts;
using Budgie.Modules.Finances.Application.Accounts.CreateAccount;
using FluentAssertions;
using Xunit;

namespace Budgie.IntegrationTests;

public class GoalsEndpointsTests : IClassFixture<BudgieWebApplicationFactory>
{
    private readonly HttpClient _client;
    private string _token = string.Empty;
    private Guid _accountId;

    public GoalsEndpointsTests(BudgieWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
        factory.EnsureDatabaseCreated();
    }

    private async Task AuthenticateAsync()
    {
        if (!string.IsNullOrEmpty(_token)) return;

        var email = $"goals_{Guid.NewGuid()}@example.com";
        await _client.PostAsJsonAsync("/api/users/register", new { Email = email, Password = "Password123!", Name = "Test" });
        var login = await _client.PostAsJsonAsync("/api/users/login", new { Email = email, Password = "Password123!" });
        var result = await login.Content.ReadFromJsonAsync<LoginResult>();
        _token = result!.Token;
        _client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _token);
        await _client.PostAsJsonAsync("/api/users/settings/setup", new { MainCurrency = "USD", DefaultCreditConsumptionMode = 1 });

        var acc = await _client.PostAsJsonAsync("/api/finances/accounts",
            new CreateAccountRequest("GoalAcc", "USD", 5000m, 1, null));
        var accResult = await acc.Content.ReadFromJsonAsync<IdResponse>();
        _accountId = accResult!.Id;
    }

    [Fact]
    public async Task CreateGoal_ShouldReturn201()
    {
        await AuthenticateAsync();

        var response = await _client.PostAsJsonAsync("/api/finances/goals", new
        {
            AccountId = _accountId,
            TargetAmount = 10000m,
            TargetDate = DateTime.UtcNow.AddMonths(12)
        });
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var result = await response.Content.ReadFromJsonAsync<IdResponse>();
        result!.Id.Should().NotBeEmpty();
    }

    [Fact]
    public async Task CreateGoal_WithoutTargetDate_ShouldReturn201()
    {
        await AuthenticateAsync();

        var response = await _client.PostAsJsonAsync("/api/finances/goals", new
        {
            AccountId = _accountId,
            TargetAmount = 5000m
        });
        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task GetGoals_ShouldReturnList()
    {
        await AuthenticateAsync();

        var response = await _client.GetAsync("/api/finances/goals");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task UpdateGoal_ShouldReturn204()
    {
        await AuthenticateAsync();

        var create = await _client.PostAsJsonAsync("/api/finances/goals", new
        {
            AccountId = _accountId,
            TargetAmount = 8000m,
            TargetDate = DateTime.UtcNow.AddMonths(6)
        });
        var created = await create.Content.ReadFromJsonAsync<IdResponse>();

        var update = await _client.PutAsJsonAsync($"/api/finances/goals/{created!.Id}", new
        {
            TargetAmount = 12000m,
            TargetDate = DateTime.UtcNow.AddMonths(12)
        });
        update.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task UpdateGoal_NotFound_ShouldReturn400()
    {
        await AuthenticateAsync();

        var response = await _client.PutAsJsonAsync($"/api/finances/goals/{Guid.NewGuid()}",
            new { TargetAmount = 100m, TargetDate = (DateTime?)null });
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task DeleteGoal_ShouldReturn204()
    {
        await AuthenticateAsync();

        var create = await _client.PostAsJsonAsync("/api/finances/goals", new
        {
            AccountId = _accountId,
            TargetAmount = 3000m
        });
        var created = await create.Content.ReadFromJsonAsync<IdResponse>();

        var delete = await _client.DeleteAsync($"/api/finances/goals/{created!.Id}");
        delete.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task DeleteGoal_NotFound_ShouldReturn400()
    {
        await AuthenticateAsync();

        var response = await _client.DeleteAsync($"/api/finances/goals/{Guid.NewGuid()}");
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    private record LoginResult(string Token);
}
