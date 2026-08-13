using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Xunit;

namespace Budgie.IntegrationTests;

public class RegistrationPolicyTests : IClassFixture<AutoRegistrationFactory>
{
    private readonly HttpClient _client;

    public RegistrationPolicyTests(AutoRegistrationFactory factory)
    {
        _client = factory.CreateClient();
        factory.EnsureDatabaseCreated();
    }

    [Fact]
    public async Task AutoMode_AllowsOnlyFirstUserToRegister()
    {
        var first = await _client.PostAsJsonAsync("/api/users/register",
            new { Email = $"first_{Guid.NewGuid()}@example.com", Password = "Password123!", Name = "First" });
        first.StatusCode.Should().Be(HttpStatusCode.Created);

        var second = await _client.PostAsJsonAsync("/api/users/register",
            new { Email = $"second_{Guid.NewGuid()}@example.com", Password = "Password123!", Name = "Second" });
        second.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var status = await _client.GetFromJsonAsync<RegistrationStatusResponse>("/api/users/registration-status");
        status!.Enabled.Should().BeFalse();
        status.HasUsers.Should().BeTrue();
    }

    private record RegistrationStatusResponse(bool Enabled, bool HasUsers);
}

public class AutoRegistrationFactory : BudgieWebApplicationFactory
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.UseSetting("Registration:Mode", "Auto");
    }
}
