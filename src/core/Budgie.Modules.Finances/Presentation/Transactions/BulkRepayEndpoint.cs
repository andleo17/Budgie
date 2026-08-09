using Budgie.Modules.Finances.Application.Transactions.BulkRepay;
using Budgie.Shared.Dtos;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Budgie.Modules.Finances.Presentation.Transactions;

public static class BulkRepayEndpoint
{
    public static void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("transactions/bulk-repay", async (BulkRepayCommand command, IMediator mediator) =>
        {
            var result = await mediator.Send(command);

            return result.Match<IResult>(
                transactionId => TypedResults.Created($"/api/finances/transactions/{transactionId}", new { Id = transactionId }),
                errors => TypedResults.BadRequest(errors.ToDictionary(e => e.Code, e => new[] { e.Description }))
            );
        })
        .Produces<CreatedResponse>(StatusCodes.Status201Created)
        .WithTags("Transactions");
    }
}
