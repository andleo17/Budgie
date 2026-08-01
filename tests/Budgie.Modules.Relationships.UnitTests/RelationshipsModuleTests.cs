using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Budgie.Modules.Relationships.UnitTests;

public class RelationshipsModuleTests
{
    [Fact]
    public void AddRelationshipsModule_ShouldReturnServiceCollection()
    {
        var services = new ServiceCollection();

        var result = services.AddRelationshipsModule();

        result.Should().BeSameAs(services);
    }

    [Fact]
    public void MapRelationshipsEndpoints_ShouldReturnEndpointBuilder()
    {
        var app = WebApplication.Create();

        var result = app.MapRelationshipsEndpoints();

        result.Should().Be(app);
    }
}