using Budgie.Modules.Finances.Application.Transactions.DeleteTransaction;
using Budgie.Modules.Finances.Application.Transactions.GetTransactions;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Budgie.Modules.Finances.Presentation.Transactions;

internal static class TransactionsEndpoints
{
    public static void MapEndpoints(RouteGroupBuilder group)
    {
        group.MapGet("/transactions", async (Guid? personId, string? accountIds,
            string? startDate, string? endDate, int? page, int? pageSize, IMediator mediator) =>
        {
            var accountIdList = accountIds?.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(Guid.Parse).ToList();
            var result = await mediator.Send(new GetTransactionsQuery(
                AccountIds: accountIdList as IReadOnlyList<Guid>,
                StartDate: startDate is not null ? DateTime.Parse(startDate) : null,
                EndDate: endDate is not null ? DateTime.Parse(endDate) : null,
                PersonId: personId, Page: page, PageSize: pageSize));
            return result.Match(Results.Ok, err => Results.BadRequest(err));
        });
        group.MapDelete("/transactions/{id:guid}", async (Guid id, IMediator mediator) =>
        {
            var result = await mediator.Send(new DeleteTransactionCommand(id));
            return result.Match(_ => Results.NoContent(), err => Results.BadRequest(err));
        });
    }
}
