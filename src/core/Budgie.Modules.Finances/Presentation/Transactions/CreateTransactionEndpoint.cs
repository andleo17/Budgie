using Budgie.Modules.Finances.Application.Transactions.CreateTransaction;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Budgie.Modules.Finances.Presentation.Transactions;

public static class CreateTransactionEndpoint
{
    public static void MapEndpoint(RouteGroupBuilder group)
    {
        group.MapPost("/transactions", async (CreateTransactionCommand cmd, IMediator mediator) =>
        {
            var result = await mediator.Send(cmd);
            return result.Match(
                id => Results.Created($"/api/finances/transactions/{id}", new { id }),
                err => Results.BadRequest(err.ToDictionary(e => e.Code, e => new[] { e.Description })));
        });
    }
}
