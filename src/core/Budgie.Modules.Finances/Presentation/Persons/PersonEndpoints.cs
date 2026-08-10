using Budgie.Modules.Finances.Application.Debts.GetPersonDebts;
using Budgie.Modules.Finances.Application.Persons.GetPersonSummary;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Budgie.Modules.Finances.Presentation.Persons;

public static class PersonSummaryEndpoint
{
    public static void MapEndpoint(RouteGroupBuilder group)
    {
        group.MapGet("/persons/{personId:guid}/summary", async (Guid personId, IMediator mediator) =>
        {
            var result = await mediator.Send(new GetPersonSummaryQuery(personId));
            return result.Match(Results.Ok, err => Results.BadRequest(err));
        });
    }
}

public static class DebtsEndpoint
{
    public static void MapEndpoint(RouteGroupBuilder group)
    {
        group.MapGet("/persons/{personId:guid}/debts", async (Guid personId, IMediator mediator) =>
        {
            var result = await mediator.Send(new GetPersonDebtsQuery(personId));
            return result.Match(Results.Ok, err => Results.BadRequest(err));
        });
    }
}
