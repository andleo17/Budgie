using Budgie.Modules.Finances.Application.Transactions.BulkRepay;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Budgie.Modules.Finances.Presentation.Transactions;

public static class BulkRepayEndpoint
{
    public static void MapEndpoint(RouteGroupBuilder group)
    {
        group.MapPost("/transactions/bulk-repay", async (BulkRepayCommand cmd, IMediator mediator) =>
        {
            var result = await mediator.Send(cmd);
            return result.Match(
                id => Results.Created($"/api/finances/transactions/{id}", new { id }),
                err => Results.BadRequest(err.ToDictionary(e => e.Code, e => new[] { e.Description })));
        });
    }
}
