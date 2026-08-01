using Budgie.Modules.Finances.Application.Transactions;
using Budgie.Modules.Finances.Application.Transactions.CreateTransaction;
using Budgie.Modules.Finances.Application.Transactions.GetTransactions;
using Budgie.Modules.Finances.Application.Transactions.GetTransactionsSummary;
using Budgie.Modules.Finances.Application.Transactions.GetTransactionById;
using Budgie.Modules.Finances.Application.Transactions.DeleteTransaction;
using Budgie.Modules.Finances.Application.Transactions.UpdateTransaction;
using Budgie.Shared.Enums;
using ErrorOr;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
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

    public static void MapEndpoints(IEndpointRouteBuilder app)
    {
        app.MapGet("transactions", async (
            [FromQuery] string? accountIds,
            [FromQuery] string? categoryIds,
            [FromQuery] string? transactionTypes,
            [FromQuery] DateTime? startDate,
            [FromQuery] DateTime? endDate,
            [FromQuery] string? search,
            [FromQuery] bool includeRunningBalance = false,
            [FromQuery] int? page = null,
            [FromQuery] int? pageSize = null,
            [FromQuery] Guid? personId = null,
            [FromQuery] string? tagIds = null,
            ISender sender = default!) =>
        {
            var query = new GetTransactionsQuery(
                AccountIds: ParseGuids(accountIds),
                CategoryIds: ParseGuids(categoryIds),
                TransactionTypes: ParseTypes(transactionTypes),
                StartDate: startDate,
                EndDate: endDate,
                Search: search,
                IncludeRunningBalance: includeRunningBalance,
                Page: page,
                PageSize: pageSize,
                PersonId: personId,
                TagIds: ParseGuids(tagIds));
            var result = await sender.Send(query);
            return result.Match(transactions => Results.Ok(transactions), errors => Results.BadRequest(errors));
        })
        .Produces<PagedResponse<TransactionResponse>>(200)
        .WithName("GetTransactions")
        .WithTags("Transactions");

        app.MapGet("transactions/summary", async (
            [FromQuery] DateTime? startDate,
            [FromQuery] DateTime? endDate,
            [FromQuery] string? accountIds,
            ISender sender) =>
        {
            var now = DateTime.UtcNow;
            var query = new GetTransactionsSummaryQuery(
                startDate ?? now.AddDays(-30),
                endDate ?? now,
                ParseGuids(accountIds));
            var result = await sender.Send(query);
            return result.Match(summary => Results.Ok(summary), errors => Results.BadRequest(errors));
        })
        .Produces<GetTransactionsSummaryResponse>(200)
        .WithName("GetTransactionsSummary")
        .WithTags("Transactions");

        app.MapGet("transactions/{id:guid}", async (Guid id, ISender sender) =>
        {
            var query = new GetTransactionByIdQuery(id);
            var result = await sender.Send(query);
            return result.Match(
                transaction => Results.Ok(transaction),
                errors => errors[0].Type == ErrorType.NotFound
                    ? Results.NotFound()
                    : Results.BadRequest(errors));
        })
        .Produces<TransactionResponse>(200)
        .WithName("GetTransactionById")
        .WithTags("Transactions");

        app.MapPut("transactions/{id:guid}", async (Guid id, [FromBody] UpdateTransactionRequest request, ISender sender) =>
        {
            var command = new UpdateTransactionCommand(
                id,
                request.CategoryId,
                request.Amount,
                request.Currency,
                request.ExchangeRate,
                request.Date,
                request.Notes,
                request.Type,
                request.Installments,
                request.Participants,
                request.ParentTransactionId);

            var result = await sender.Send(command);
            return result.Match(_ => Results.NoContent(), errors => Results.BadRequest(errors));
        })
        .Produces(204)
        .WithName("UpdateTransaction")
        .WithTags("Transactions");

        app.MapDelete("transactions/{id:guid}", async (Guid id, ISender sender) =>
        {
            var command = new DeleteTransactionCommand(id);
            var result = await sender.Send(command);
            return result.Match(_ => Results.NoContent(), errors => Results.BadRequest(errors));
        })
        .Produces(204)
        .WithName("DeleteTransaction")
        .WithTags("Transactions");
    }
}

public record UpdateTransactionRequest(
    Guid? CategoryId,
    decimal Amount,
    string Currency,
    decimal ExchangeRate,
    DateTime Date,
    string? Notes,
    TransactionType Type,
    int Installments,
    List<TransactionParticipantDto>? Participants = null,
    Guid? ParentTransactionId = null);
