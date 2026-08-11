using System.Net;
using System.Net.Http.Json;
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
    public async Task Register_Login_Refresh_ShouldWork()
    {
        var email = $"user_{Guid.NewGuid()}@example.com";

        var register = await _client.PostAsJsonAsync("/api/users/register",
            new { Email = email, Password = "Password123!", Name = "Test User" });
        register.StatusCode.Should().Be(HttpStatusCode.Created);

        var login = await _client.PostAsJsonAsync("/api/users/login",
            new { Email = email, Password = "Password123!" });
        login.StatusCode.Should().Be(HttpStatusCode.OK);
        var loginResult = await login.Content.ReadFromJsonAsync<AuthResponse>();
        loginResult!.Token.Should().NotBeNullOrEmpty();
        loginResult.RefreshToken.Should().NotBeNullOrEmpty();

        var refresh = await _client.PostAsJsonAsync("/api/users/refresh",
            new { RefreshToken = loginResult.RefreshToken });
        refresh.StatusCode.Should().Be(HttpStatusCode.OK);
        var refreshResult = await refresh.Content.ReadFromJsonAsync<AuthResponse>();
        refreshResult!.Token.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task Login_WithInvalidCredentials_ShouldReturn401()
    {
        var response = await _client.PostAsJsonAsync("/api/users/login",
            new { Email = "nonexistent@example.com", Password = "WrongPassword123!" });
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Register_DuplicateEmail_ShouldReturn400()
    {
        var email = $"dup_{Guid.NewGuid()}@example.com";
        var body = new { Email = email, Password = "Password123!", Name = "Dup" };

        var first = await _client.PostAsJsonAsync("/api/users/register", body);
        first.StatusCode.Should().Be(HttpStatusCode.Created);

        var second = await _client.PostAsJsonAsync("/api/users/register", body);
        second.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetProfile_ShouldReturnUser()
    {
        var token = await AuthHelper.RegisterAndLoginAsync(_client);

        var response = await _client.GetAsync("/api/users/profile");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var profile = await response.Content.ReadFromJsonAsync<ProfileResponse>();
        profile!.Name.Should().NotBeNullOrEmpty();
    }

    private record AuthResponse(string Token, string RefreshToken);
    private record ProfileResponse(Guid Id, string Email, string Name);
}
