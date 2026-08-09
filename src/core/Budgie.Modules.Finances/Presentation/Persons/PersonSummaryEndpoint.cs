using Budgie.Modules.Finances.Application.Persons.GetPersonSummary;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Budgie.Modules.Finances.Presentation.Persons;

internal static class PersonSummaryEndpoint
{
    public static void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("persons/{personId:guid}/summary", async (Guid personId, ISender sender) =>
        {
            var result = await sender.Send(new GetPersonSummaryQuery(personId));

            return result.Match(
                summary => Results.Ok(summary),
                errors => Results.BadRequest(errors));
        })
        .Produces<PersonSummaryResponse>(200)
        .WithName("GetPersonSummary")
        .WithTags("Persons");
    }
}
