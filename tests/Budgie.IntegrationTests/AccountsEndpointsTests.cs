using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Xunit;

namespace Budgie.IntegrationTests;

public class AccountsEndpointsTests : IClassFixture<BudgieWebApplicationFactory>
{
    private readonly HttpClient _client;

    public AccountsEndpointsTests(BudgieWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
        factory.EnsureDatabaseCreated();
        AuthHelper.RegisterAndLoginAsync(_client).GetAwaiter().GetResult();
    }

    [Fact]
    public async Task CreateAccount_ShouldReturn201()
    {
        var response = await _client.PostAsJsonAsync("/api/finances/accounts",
            new { Name = "Main", Currency = "USD", InitialBalance = 500m, Type = 1 });
        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task GetAccounts_ShouldReturnList()
    {
        await _client.PostAsJsonAsync("/api/finances/accounts",
            new { Name = "Main", Currency = "USD", InitialBalance = 500m, Type = 1 });

        var response = await _client.GetAsync("/api/finances/accounts");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var accounts = await response.Content.ReadFromJsonAsync<List<AccountDto>>();
        accounts!.Should().Contain(a => a.Name == "Main");
    }

    [Fact]
    public async Task GetAccountById_ShouldReturnAccount()
    {
        var create = await _client.PostAsJsonAsync("/api/finances/accounts",
            new { Name = "Detail", Currency = "USD", InitialBalance = 100m, Type = 1 });
        var created = await create.Content.ReadFromJsonAsync<IdResponse>();

        var response = await _client.GetAsync($"/api/finances/accounts/{created!.Id}");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var account = await response.Content.ReadFromJsonAsync<AccountDto>();
        account!.Name.Should().Be("Detail");
    }

    [Fact]
    public async Task GetAccountById_NotFound_ShouldReturn400()
    {
        var response = await _client.GetAsync($"/api/finances/accounts/{Guid.NewGuid()}");
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    private record IdResponse(Guid Id);
    private record AccountDto(Guid Id, string Name, decimal Balance, string Currency);
}
