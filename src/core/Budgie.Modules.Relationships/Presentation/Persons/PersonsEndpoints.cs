using Budgie.Modules.Relationships.Application.Persons;
using Budgie.Modules.Relationships.Application.Persons.GetPersons;
using Budgie.Modules.Relationships.Application.Persons.GetPersonById;
using Budgie.Modules.Relationships.Application.Persons.DeletePerson;
using Budgie.Modules.Relationships.Application.Persons.UpdatePerson;
using ErrorOr;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Budgie.Modules.Relationships.Presentation.Persons;

internal static class PersonsEndpoints
{
    public static void MapEndpoints(IEndpointRouteBuilder app)
    {
        app.MapGet("persons", async (ISender sender) =>
        {
            var query = new GetPersonsQuery();
            var result = await sender.Send(query);
            return result.Match(persons => Results.Ok(persons), errors => Results.BadRequest(errors));
        })
        .Produces<List<PersonResponse>>(200)
        .WithName("GetPersons")
        .WithTags("Persons");

        app.MapGet("persons/{id:guid}", async (Guid id, ISender sender) =>
        {
            var query = new GetPersonByIdQuery(id);
            var result = await sender.Send(query);
            return result.Match(
                person => Results.Ok(person),
                errors => errors[0].Type == ErrorType.NotFound
                    ? Results.NotFound()
                    : Results.BadRequest(errors));
        })
        .Produces<PersonResponse>(200)
        .WithName("GetPersonById")
        .WithTags("Persons");

        app.MapPut("persons/{id:guid}", async (Guid id, [FromBody] UpdatePersonRequest request, ISender sender) =>
        {
            var command = new UpdatePersonCommand(id, request.Name, request.TagNames);
            var result = await sender.Send(command);
            return result.Match(_ => Results.NoContent(), errors => Results.BadRequest(errors));
        })
        .Produces(204)
        .WithName("UpdatePerson")
        .WithTags("Persons");

        app.MapDelete("persons/{id:guid}", async (Guid id, ISender sender) =>
        {
            var command = new DeletePersonCommand(id);
            var result = await sender.Send(command);
            return result.Match(_ => Results.NoContent(), errors => Results.BadRequest(errors));
        })
        .Produces(204)
        .WithName("DeletePerson")
        .WithTags("Persons");
    }
}

public record UpdatePersonRequest(string Name, List<string>? TagNames = null);
