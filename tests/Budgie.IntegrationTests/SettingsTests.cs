using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Xunit;

namespace Budgie.IntegrationTests;

public class SettingsTests : IClassFixture<BudgieWebApplicationFactory>
{
    private readonly HttpClient _client;

    public SettingsTests(BudgieWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
        factory.EnsureDatabaseCreated();
    }

    [Fact]
    public async Task Setup_ThenGetSettings_ShouldReturnConfiguredValues()
    {
        var email = $"settings_{Guid.NewGuid()}@example.com";
        await _client.PostAsJsonAsync("/api/users/register", new { Email = email, Password = "Password123!", Name = "Test" });
        var login = await _client.PostAsJsonAsync("/api/users/login", new { Email = email, Password = "Password123!" });
        var result = await login.Content.ReadFromJsonAsync<LoginResult>();
        _client.DefaultRequestHeaders.Authorization = new("Bearer", result!.Token);

        var setup = await _client.PostAsJsonAsync("/api/users/settings/setup",
            new { MainCurrency = "PEN", DefaultCreditConsumptionMode = 1 });
        setup.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var get = await _client.GetAsync("/api/users/settings");
        get.StatusCode.Should().Be(HttpStatusCode.OK);
        var settings = await get.Content.ReadFromJsonAsync<SettingsDto>();
        settings!.MainCurrency.Should().Be("PEN");
        settings.IsConfigured.Should().BeTrue();
    }

    [Fact]
    public async Task GetSettings_WithoutSetup_ShouldReturnDefaults()
    {
        var email = $"nosetup_{Guid.NewGuid()}@example.com";
        await _client.PostAsJsonAsync("/api/users/register", new { Email = email, Password = "Password123!", Name = "Test" });
        var login = await _client.PostAsJsonAsync("/api/users/login", new { Email = email, Password = "Password123!" });
        var result = await login.Content.ReadFromJsonAsync<LoginResult>();
        _client.DefaultRequestHeaders.Authorization = new("Bearer", result!.Token);

        var get = await _client.GetAsync("/api/users/settings");
        get.StatusCode.Should().Be(HttpStatusCode.OK);
        var settings = await get.Content.ReadFromJsonAsync<SettingsDto>();
        settings!.MainCurrency.Should().Be("USD");
        settings.IsConfigured.Should().BeFalse();
    }

    private record LoginResult(string Token, string RefreshToken);
    private record SettingsDto(string MainCurrency, bool IsConfigured);
}
