using System.Net;
using System.Net.Http.Json;
using Budgie.Modules.Users.Application.Settings;
using Budgie.Modules.Users.Application.Settings.UpdateSettings;
using Budgie.Shared.Enums;
using FluentAssertions;
using Xunit;

namespace Budgie.IntegrationTests;

public class UsersEndpointsTests : IClassFixture<BudgieWebApplicationFactory>
{
    private readonly HttpClient _client;

    public UsersEndpointsTests(BudgieWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
        factory.EnsureDatabaseCreated();
    }

    [Fact]
    public async Task Register_ShouldReturn201()
    {
        var email = $"register_{Guid.NewGuid()}@example.com";
        var response = await _client.PostAsJsonAsync("/api/users/register",
            new { Email = email, Password = "Password123!", Name = "New User" });
        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task Register_DuplicateEmail_ShouldReturn400()
    {
        var email = $"dupe_{Guid.NewGuid()}@example.com";
        await _client.PostAsJsonAsync("/api/users/register",
            new { Email = email, Password = "Password123!", Name = "First" });
        var response = await _client.PostAsJsonAsync("/api/users/register",
            new { Email = email, Password = "Password123!", Name = "Second" });
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Login_ShouldReturnToken()
    {
        var email = $"login_{Guid.NewGuid()}@example.com";
        await _client.PostAsJsonAsync("/api/users/register",
            new { Email = email, Password = "Password123!", Name = "LoginUser" });

        var response = await _client.PostAsJsonAsync("/api/users/login",
            new { Email = email, Password = "Password123!" });
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<LoginResponse>();
        result.Should().NotBeNull();
        result!.Token.Should().NotBeNullOrEmpty();
        result.RefreshToken.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task Login_WrongPassword_ShouldReturn401()
    {
        var email = $"wrong_{Guid.NewGuid()}@example.com";
        await _client.PostAsJsonAsync("/api/users/register",
            new { Email = email, Password = "Password123!", Name = "User" });

        var response = await _client.PostAsJsonAsync("/api/users/login",
            new { Email = email, Password = "WrongPassword!" });
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Refresh_ShouldReturnNewToken()
    {
        var email = $"refresh_{Guid.NewGuid()}@example.com";
        await _client.PostAsJsonAsync("/api/users/register",
            new { Email = email, Password = "Password123!", Name = "RefreshUser" });
        var login = await _client.PostAsJsonAsync("/api/users/login",
            new { Email = email, Password = "Password123!" });
        var loginResult = await login.Content.ReadFromJsonAsync<LoginResponse>();

        var response = await _client.PostAsJsonAsync("/api/users/refresh",
            new { RefreshToken = loginResult!.RefreshToken });
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<LoginResponse>();
        result!.Token.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task Refresh_InvalidToken_ShouldReturn401()
    {
        var response = await _client.PostAsJsonAsync("/api/users/refresh",
            new { RefreshToken = "invalid-refresh-token" });
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetProfile_ShouldReturnUserProfile()
    {
        var email = $"profile_{Guid.NewGuid()}@example.com";
        await _client.PostAsJsonAsync("/api/users/register",
            new { Email = email, Password = "Password123!", Name = "ProfileUser" });
        var login = await _client.PostAsJsonAsync("/api/users/login",
            new { Email = email, Password = "Password123!" });
        var loginResult = await login.Content.ReadFromJsonAsync<LoginResponse>();
        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", loginResult!.Token);

        await _client.PostAsJsonAsync("/api/users/settings/setup",
            new { MainCurrency = "USD", DefaultCreditConsumptionMode = 1 });

        var response = await _client.GetAsync("/api/users/profile");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task UpdateProfile_ShouldReturn204()
    {
        var email = $"update_{Guid.NewGuid()}@example.com";
        await _client.PostAsJsonAsync("/api/users/register",
            new { Email = email, Password = "Password123!", Name = "OldName" });
        var login = await _client.PostAsJsonAsync("/api/users/login",
            new { Email = email, Password = "Password123!" });
        var loginResult = await login.Content.ReadFromJsonAsync<LoginResponse>();
        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", loginResult!.Token);

        await _client.PostAsJsonAsync("/api/users/settings/setup",
            new { MainCurrency = "USD", DefaultCreditConsumptionMode = 1 });

        var response = await _client.PutAsJsonAsync("/api/users/profile", new { Name = "NewName" });
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task GetProfile_WithoutAuth_ShouldReturn401()
    {
        var response = await _client.GetAsync("/api/users/profile");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Setup_ShouldReturn204()
    {
        var email = $"setup_{Guid.NewGuid()}@example.com";
        await _client.PostAsJsonAsync("/api/users/register",
            new { Email = email, Password = "Password123!", Name = "SetupUser" });
        var login = await _client.PostAsJsonAsync("/api/users/login",
            new { Email = email, Password = "Password123!" });
        var loginResult = await login.Content.ReadFromJsonAsync<LoginResponse>();
        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", loginResult!.Token);

        var response = await _client.PostAsJsonAsync("/api/users/settings/setup",
            new { MainCurrency = "USD", DefaultCreditConsumptionMode = 1 });

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task GetSettings_ShouldReturnSettings()
    {
        var email = $"getset_{Guid.NewGuid()}@example.com";
        await _client.PostAsJsonAsync("/api/users/register",
            new { Email = email, Password = "Password123!", Name = "SettingsUser" });
        var login = await _client.PostAsJsonAsync("/api/users/login",
            new { Email = email, Password = "Password123!" });
        var loginResult = await login.Content.ReadFromJsonAsync<LoginResponse>();
        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", loginResult!.Token);

        await _client.PostAsJsonAsync("/api/users/settings/setup",
            new { MainCurrency = "USD", DefaultCreditConsumptionMode = 1 });

        var response = await _client.GetAsync("/api/users/settings");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var settings = await response.Content.ReadFromJsonAsync<UserSettingsResponse>();
        settings.Should().NotBeNull();
        settings!.MainCurrency.Should().Be("USD");
    }

    [Fact]
    public async Task UpdateSettings_ShouldReturn204()
    {
        var email = $"putset_{Guid.NewGuid()}@example.com";
        await _client.PostAsJsonAsync("/api/users/register",
            new { Email = email, Password = "Password123!", Name = "User" });
        var login = await _client.PostAsJsonAsync("/api/users/login",
            new { Email = email, Password = "Password123!" });
        var loginResult = await login.Content.ReadFromJsonAsync<LoginResponse>();
        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", loginResult!.Token);

        await _client.PostAsJsonAsync("/api/users/settings/setup",
            new { MainCurrency = "USD", DefaultCreditConsumptionMode = 1 });

        var response = await _client.PutAsJsonAsync("/api/users/settings",
            new UpdateSettingsCommand("EUR", CreditConsumptionMode.Total));
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var getResponse = await _client.GetAsync("/api/users/settings");
        var settings = await getResponse.Content.ReadFromJsonAsync<UserSettingsResponse>();
        settings!.MainCurrency.Should().Be("EUR");
    }

    private record LoginResponse(string Token, string RefreshToken);
}
