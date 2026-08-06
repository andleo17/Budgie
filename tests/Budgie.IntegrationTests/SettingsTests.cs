using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Budgie.Modules.Users.Application.Settings.UpdateSettings;
using Budgie.Modules.Users.Domain;
using Budgie.Modules.Users.Infrastructure.Database;
using Budgie.Shared.Enums;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Budgie.IntegrationTests;

public class SettingsTests : IClassFixture<BudgieWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly BudgieWebApplicationFactory _factory;

    public SettingsTests(BudgieWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
        factory.EnsureDatabaseCreated();
    }

    [Fact]
    public async Task App_ShouldBlockRequests_WhenNotConfigured()
    {
        // 1. Register
        var email = $"test_{Guid.NewGuid()}@example.com";
        var regResponse = await _client.PostAsJsonAsync("/api/users/register", new { Email = email, Password = "Password123!", Name = "Test User" });
        regResponse.EnsureSuccessStatusCode();

        // 2. Login
        var loginResponse = await _client.PostAsJsonAsync("/api/users/login", new { Email = email, Password = "Password123!" });
        loginResponse.EnsureSuccessStatusCode();
        var loginResult = await loginResponse.Content.ReadFromJsonAsync<LoginResult>();
        
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", loginResult!.Token);

        // 3. Try to access dashboard (should be blocked)
        var dashboardResponse = await _client.GetAsync("/api/finances/reports/dashboard");
        dashboardResponse.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        // 4. Configure settings using the setup endpoint
        var settingsResponse = await _client.PostAsJsonAsync("/api/users/settings/setup", new UpdateSettingsCommand("USD", CreditConsumptionMode.Total));
        settingsResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // 5. Try to access dashboard again (should be allowed)
        var dashboardResponse2 = await _client.GetAsync("/api/finances/reports/dashboard");
        dashboardResponse2.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Session_WhenUserDeleted_ShouldReturnUnauthorized()
    {
        // 1. Register + Login
        var email = $"test_{Guid.NewGuid()}@example.com";
        var regResponse = await _client.PostAsJsonAsync("/api/users/register", new { Email = email, Password = "Password123!", Name = "Test User" });
        regResponse.EnsureSuccessStatusCode();

        var loginResponse = await _client.PostAsJsonAsync("/api/users/login", new { Email = email, Password = "Password123!" });
        loginResponse.EnsureSuccessStatusCode();
        var loginResult = await loginResponse.Content.ReadFromJsonAsync<LoginResult>();

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", loginResult!.Token);

        // 2. Delete the user (simulates a wiped database after the token was issued)
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<UsersDbContext>();
            var user = await db.Users.FirstAsync(u => u.Email == email);
            db.Users.Remove(user);
            await db.SaveChangesAsync();
        }

        // 3. Every protected route must reject the stale session
        var settingsResponse = await _client.GetAsync("/api/users/settings");
        settingsResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        var dashboardResponse = await _client.GetAsync("/api/finances/reports/dashboard");
        dashboardResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    private record LoginResult(string Token);
}
