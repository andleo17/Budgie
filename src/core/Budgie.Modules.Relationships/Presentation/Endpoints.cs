using Budgie.Modules.Relationships.Application.Persons.CreatePerson;
using Budgie.Modules.Relationships.Application.Persons.DeletePerson;
using Budgie.Modules.Relationships.Application.Persons.GetPersons;
using Budgie.Modules.Relationships.Application.Persons.UpdatePerson;
using Budgie.Modules.Relationships.Application.Tags;
using Budgie.Modules.Relationships.Application.Tags.DeleteTag;
using Budgie.Modules.Relationships.Application.Tags.UpdateTag;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Budgie.Modules.Relationships.Presentation;

public static class CreatePersonEndpoint
{
    public static void MapEndpoint(RouteGroupBuilder group)
    {
        group.MapPost("/persons", async (CreatePersonCommand cmd, IMediator mediator) =>
        {
            var result = await mediator.Send(cmd);
            return result.Match(
                id => Results.Created($"/api/relationships/persons/{id}", new { id }),
                errors => Results.BadRequest(errors.ToDictionary(e => e.Code, e => new[] { e.Description })));
        });
    }
}

internal static class PersonsEndpoints
{
    public static void MapEndpoints(RouteGroupBuilder group)
    {
        group.MapGet("/persons", async (IMediator mediator) =>
        {
            var result = await mediator.Send(new GetPersonsQuery());
            return result.Match(Results.Ok, errors => Results.BadRequest(errors));
        });

        group.MapPut("/persons/{id:guid}", async (Guid id, UpdatePersonCommand cmd, IMediator mediator) =>
        {
            var result = await mediator.Send(cmd with { Id = id });
            return result.Match(_ => Results.NoContent(), errors => Results.BadRequest(errors));
        });

        group.MapDelete("/persons/{id:guid}", async (Guid id, IMediator mediator) =>
        {
            var result = await mediator.Send(new DeletePersonCommand(id));
            return result.Match(_ => Results.NoContent(), errors => Results.BadRequest(errors));
        });
    }
}

public static class CreateTagEndpoint
{
    public static void MapEndpoint(RouteGroupBuilder group)
    {
        group.MapPost("/tags", async (CreateTagCommand cmd, IMediator mediator) =>
        {
            var result = await mediator.Send(cmd);
            return result.Match(
                id => Results.Created($"/api/relationships/tags/{id}", new { id }),
                errors => Results.BadRequest(errors.ToDictionary(e => e.Code, e => new[] { e.Description })));
        });
    }
}

internal static class TagsEndpoints
{
    public static void MapEndpoints(RouteGroupBuilder group)
    {
        group.MapGet("/tags", async (IMediator mediator) =>
        {
            var result = await mediator.Send(new GetTagsQuery());
            return result.Match(Results.Ok, errors => Results.BadRequest(errors));
        });

        group.MapPut("/tags/{id:guid}", async (Guid id, UpdateTagRequest req, IMediator mediator) =>
        {
            var result = await mediator.Send(new UpdateTagCommand(id, req.Name, req.Color));
            return result.Match(_ => Results.NoContent(), errors => Results.BadRequest(errors));
        });

        group.MapDelete("/tags/{id:guid}", async (Guid id, IMediator mediator) =>
        {
            var result = await mediator.Send(new DeleteTagCommand(id));
            return result.Match(_ => Results.NoContent(), errors => Results.BadRequest(errors));
        });
    }
}

public record UpdateTagRequest(string Name, string? Color = null);
