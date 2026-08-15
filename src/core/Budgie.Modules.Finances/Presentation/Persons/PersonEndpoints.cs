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
        })
        .Produces<PersonSummaryResponse>(StatusCodes.Status200OK);
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
        })
        .Produces<List<DebtResponse>>(StatusCodes.Status200OK);

        group.MapPost("/debts/settle", async (Budgie.Modules.Finances.Application.Debts.SettleDebt.SettleDebtCommand command, IMediator mediator) =>
        {
            var result = await mediator.Send(command);
            return result.Match(
                id => Results.Ok(new { Id = id }),
                err => Results.BadRequest(err));
        })
        .Produces<Budgie.Shared.Dtos.CreatedResponse>(StatusCodes.Status200OK);
    }
}
