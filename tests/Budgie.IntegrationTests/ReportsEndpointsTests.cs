using System.Net;
using System.Net.Http.Json;
using Budgie.Modules.Finances.Application.Accounts.CreateAccount;
using Budgie.Modules.Finances.Application.Reports;
using FluentAssertions;
using Xunit;

namespace Budgie.IntegrationTests;

public class ReportsEndpointsTests : IClassFixture<BudgieWebApplicationFactory>
{
    private readonly HttpClient _client;
    private string _token = string.Empty;
    private Guid _accountId;

    public ReportsEndpointsTests(BudgieWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
        factory.EnsureDatabaseCreated();
    }

    private async Task AuthenticateAsync()
    {
        if (!string.IsNullOrEmpty(_token)) return;

        var email = $"reports_{Guid.NewGuid()}@example.com";
        await _client.PostAsJsonAsync("/api/users/register", new { Email = email, Password = "Password123!", Name = "Test" });
        var login = await _client.PostAsJsonAsync("/api/users/login", new { Email = email, Password = "Password123!" });
        var result = await login.Content.ReadFromJsonAsync<LoginResult>();
        _token = result!.Token;
        _client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _token);
        await _client.PostAsJsonAsync("/api/users/settings/setup", new { MainCurrency = "USD", DefaultCreditConsumptionMode = 1 });

        var acc = await _client.PostAsJsonAsync("/api/finances/accounts",
            new CreateAccountRequest("ReportAcc", "USD", 1000m, 1, null));
        var accResult = await acc.Content.ReadFromJsonAsync<IdResponse>();
        _accountId = accResult!.Id;
    }

    [Fact]
    public async Task GetDashboard_ShouldReturnDashboardSummary()
    {
        await AuthenticateAsync();

        var response = await _client.GetAsync("/api/finances/reports/dashboard");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var dashboard = await response.Content.ReadFromJsonAsync<DashboardSummary>();
        dashboard.Should().NotBeNull();
    }

    [Fact]
    public async Task GetDashboard_WithCurrency_ShouldWork()
    {
        await AuthenticateAsync();

        var response = await _client.GetAsync("/api/finances/reports/dashboard?currency=EUR");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetSpendingByCategory_ShouldReturnList()
    {
        await AuthenticateAsync();

        var response = await _client.GetAsync("/api/finances/reports/spending-by-category");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var spending = await response.Content.ReadFromJsonAsync<List<CategorySpending>>();
        spending.Should().NotBeNull();
    }

    [Fact]
    public async Task GetSpendingByCategory_WithDateRange_ShouldWork()
    {
        await AuthenticateAsync();

        var response = await _client.GetAsync(
            $"/api/finances/reports/spending-by-category?start={DateTime.UtcNow.AddMonths(-1):O}&end={DateTime.UtcNow:O}");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetGoalsReport_ShouldReturnList()
    {
        await AuthenticateAsync();

        var response = await _client.GetAsync("/api/finances/reports/goals");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var goals = await response.Content.ReadFromJsonAsync<List<GoalStatus>>();
        goals.Should().NotBeNull();
    }

    private record LoginResult(string Token);
}
