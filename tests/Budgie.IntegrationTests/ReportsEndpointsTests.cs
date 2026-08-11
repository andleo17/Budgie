using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Xunit;

namespace Budgie.IntegrationTests;

public class ReportsEndpointsTests : IClassFixture<BudgieWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly BudgieWebApplicationFactory _factory;

    public ReportsEndpointsTests(BudgieWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
        factory.EnsureDatabaseCreated();
        AuthHelper.RegisterAndLoginAsync(_client, currency: "PEN").GetAwaiter().GetResult();
        AuthHelper.CreateAccountAsync(_client, "DashAcc", "PEN", 1000m).GetAwaiter().GetResult();
    }

    [Fact]
    public async Task GetDashboardSummary_ShouldReturnMainCurrency()
    {
        var response = await _client.GetAsync("/api/finances/reports/dashboard");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var summary = await response.Content.ReadFromJsonAsync<DashboardSummary>();
        summary!.Currency.Should().Be("PEN");
        summary.TotalBalance.Should().Be(1000m);
    }

    [Fact]
    public async Task GetDashboardSummary_Unauthenticated_ShouldReturn401()
    {
        var unauthenticated = _factory.CreateClient();
        var response = await unauthenticated.GetAsync("/api/finances/reports/dashboard");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    private record DashboardSummary(decimal TotalBalance, decimal MonthlyIncome,
        decimal MonthlyExpenses, decimal NetFlow, string Currency);
}
