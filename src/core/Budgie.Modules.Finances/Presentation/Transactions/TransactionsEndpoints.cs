using Budgie.Modules.Finances.Application.Transactions.DeleteTransaction;
using Budgie.Modules.Finances.Application.Transactions.GetTransactions;
using Budgie.Shared.Enums;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Budgie.Modules.Finances.Presentation.Transactions;

internal static class TransactionsEndpoints
{
    private static IReadOnlyList<Guid>? ParseGuids(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var guids = value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(s => Guid.TryParse(s, out var g) ? g : (Guid?)null)
            .Where(g => g.HasValue)
            .Select(g => g!.Value)
            .ToList();
        return guids.Count > 0 ? guids : null;
    }

    private static IReadOnlyList<TransactionType>? ParseTypes(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var types = value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(s => int.TryParse(s, out var v) ? (TransactionType?)v : null)
            .Where(t => t.HasValue)
            .Select(t => t!.Value)
            .ToList();
        return types.Count > 0 ? types : null;
    }

    public static void MapEndpoints(RouteGroupBuilder group)
    {
        group.MapGet("/transactions", async (IMediator mediator, string? accountIds, string? categoryIds,
            string? transactionTypes, string? startDate, string? endDate, string? search,
            bool includeRunningBalance = false, Guid? personId = null, string? tagIds = null,
            int? page = null, int? pageSize = null) =>
        {
            var result = await mediator.Send(new GetTransactionsQuery(
                AccountIds: ParseGuids(accountIds),
                CategoryIds: ParseGuids(categoryIds),
                TransactionTypes: ParseTypes(transactionTypes),
                StartDate: startDate is not null ? DateTime.Parse(startDate) : null,
                EndDate: endDate is not null ? DateTime.Parse(endDate) : null,
                Search: search,
                PersonId: personId,
                TagIds: ParseGuids(tagIds),
                IncludeRunningBalance: includeRunningBalance,
                Page: page, PageSize: pageSize));
            return result.Match(Results.Ok, err => Results.BadRequest(err));
        });
        group.MapDelete("/transactions/{id:guid}", async (Guid id, IMediator mediator) =>
        {
            var result = await mediator.Send(new DeleteTransactionCommand(id));
            return result.Match(_ => Results.NoContent(), err => Results.BadRequest(err));
        });
    }
}
