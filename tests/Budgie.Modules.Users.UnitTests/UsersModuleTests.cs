using Budgie.Shared.Abstractions;
using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Budgie.Modules.Users.UnitTests;

public class UsersModuleTests
{
    [Fact]
    public void AddUsersModule_ShouldReturnServiceCollection()
    {
        var services = new ServiceCollection();

        var result = services.AddUsersModule();

        result.Should().BeSameAs(services);
    }

    [Fact]
    public void MapUsersEndpoints_ShouldReturnEndpointBuilder()
    {
        var app = WebApplication.Create();

        var result = app.MapUsersEndpoints();

        result.Should().Be(app);
    }
}