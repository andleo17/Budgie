using Budgie.Modules.Relationships.Presentation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Budgie.Modules.Relationships;

public static class RelationshipsModule
{
    public static IServiceCollection AddRelationshipsModule(this IServiceCollection services)
    {
        return services;
    }

    public static IEndpointRouteBuilder MapRelationshipsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/relationships").RequireAuthorization();
        CreatePersonEndpoint.MapEndpoint(group);
        PersonsEndpoints.MapEndpoints(group);
        CreateTagEndpoint.MapEndpoint(group);
        TagsEndpoints.MapEndpoints(group);
        return app;
    }
}
