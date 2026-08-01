using Budgie.Modules.Relationships.Presentation.Persons;
using Budgie.Modules.Relationships.Presentation.Tags;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Budgie.Modules.Relationships;

public static class RelationshipsModule
{
    public static IServiceCollection AddRelationshipsModule(this IServiceCollection services)
    {
        services.AddMediatR(config =>
        {
            config.RegisterServicesFromAssembly(typeof(RelationshipsModule).Assembly);
        });

        return services;
    }

    public static IEndpointRouteBuilder MapRelationshipsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/relationships").RequireAuthorization();
        CreatePersonEndpoint.MapEndpoint(group);
        PersonsEndpoints.MapEndpoints(group);
        PersonPhotoEndpoints.MapEndpoints(group);
        CreateTagEndpoint.MapEndpoint(group);
        TagsEndpoints.MapEndpoints(group);
        return app;
    }
}
