using Budgie.Modules.Relationships.Application;
using Budgie.Modules.Relationships.Infrastructure.Database;
using Budgie.Modules.Relationships.Presentation;
using Budgie.Modules.Relationships.Presentation.Persons;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Budgie.Modules.Relationships;

public static class RelationshipsModule
{
    public static IServiceCollection AddRelationshipsModule(this IServiceCollection services)
    {
        services.AddScoped<IPersonRepository, PersonRepository>();
        services.AddScoped<ITagRepository, TagRepository>();
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
