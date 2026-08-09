using Budgie.Modules.Finances.Application.Debts.GetPersonDebts;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Budgie.Modules.Finances.Presentation.Debts;

internal static class DebtsEndpoint
{
    public static void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("persons/{personId:guid}/debts", async (Guid personId, ISender sender) =>
        {
            var result = await sender.Send(new GetPersonDebtsQuery(personId));

            return result.Match(
                debts => Results.Ok(debts),
                errors => Results.BadRequest(errors));
        })
        .Produces<List<DebtResponse>>(200)
        .WithName("GetPersonDebts")
        .WithTags("Persons");
    }
}
