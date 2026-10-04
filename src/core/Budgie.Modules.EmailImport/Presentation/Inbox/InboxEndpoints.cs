using Budgie.Modules.EmailImport.Application.Inbox.ApproveBatch;
using Budgie.Modules.EmailImport.Application.Inbox.ApprovePending;
using Budgie.Modules.EmailImport.Application.Inbox.DismissPending;
using Budgie.Modules.EmailImport.Application.Inbox.GetPending;
using Budgie.Shared.Enums;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Budgie.Modules.EmailImport.Presentation.Inbox;

/// <summary>
/// Review inbox (spec §8.1): list staged rows, approve one (with optional corrections),
/// approve a batch of high-confidence rows and dismiss rows. The on-demand sync lives in
/// <see cref="SyncNowEndpoint"/>.
/// </summary>
internal static class InboxEndpoints
{
    public static void MapEndpoints(RouteGroupBuilder group)
    {
        // GET /inbox ?status=&page=&pageSize= — Pending rows by default; Failed rows are
        // available so the UI can show why an import failed. Committed/Dismissed yield an
        // empty page (they are not part of the review inbox).
        group.MapGet("/inbox", async (
            int? page, int? pageSize, PendingEmailStatus? status, IMediator mediator) =>
        {
            var result = await mediator.Send(new GetPendingQuery(
                page ?? 1, pageSize ?? 50, status));
            return result.Match(
                value => Results.Ok(value),
                errors => Results.BadRequest(errors.ToDictionary(e => e.Code, e => new[] { e.Description })));
        });

        // Approving dispatches CreateTransactionCommand through Mediator with the same
        // ExternalUid key the worker uses, so a row the worker already committed can never
        // create a second transaction. All corrections are optional: approve-without-touching
        // uses the staged values as-is.
        group.MapPost("/inbox/{id:guid}/approve", async (
            Guid id, ApprovePendingRequest req, IMediator mediator) =>
        {
            var result = await mediator.Send(req.ToCommand(id));
            return result.Match(
                transactionId => Results.Ok(new { id = transactionId }),
                errors => Results.BadRequest(errors.ToDictionary(e => e.Code, e => new[] { e.Description })));
        });

        group.MapPost("/inbox/approve-batch", async (ApproveBatchRequest req, IMediator mediator) =>
        {
            var result = await mediator.Send(req.ToCommand());
            return result.Match(
                value => Results.Ok(value),
                errors => Results.BadRequest(errors.ToDictionary(e => e.Code, e => new[] { e.Description })));
        });

        // Dismiss keeps the row (traced, auditable) so its MessageId keeps blocking
        // re-import of the same email — it is a state change, never a delete.
        group.MapPost("/inbox/{id:guid}/dismiss", async (Guid id, IMediator mediator) =>
        {
            var result = await mediator.Send(new DismissPendingCommand(id));
            return result.Match(
                _ => Results.NoContent(),
                errors => Results.BadRequest(errors.ToDictionary(e => e.Code, e => new[] { e.Description })));
        });
    }
}

/// <summary>
/// Body of <c>POST /api/email-import/inbox/{id}/approve</c>. Every field except the row id
/// (route) is optional: approving without corrections stages nothing extra and commits the
/// values exactly as parsed; each non-null field overrides the staged value.
/// </summary>
public record ApprovePendingRequest(
    decimal? Amount,
    DateTime? Date,
    string? Merchant,
    string? Notes,
    Guid? AccountId,
    Guid? CategoryId,
    TransactionType? Type)
{
    internal ApprovePendingCommand ToCommand(Guid id) => new(
        id, Amount, Date, Merchant, Notes, AccountId, CategoryId, Type);
}

/// <summary>
/// Body of <c>POST /api/email-import/inbox/approve-batch</c>: approve every Pending row
/// whose confidence is at least <see cref="MinConfidence"/>, highest confidence first,
/// up to <see cref="Take"/> rows.
/// </summary>
public record ApproveBatchRequest(
    decimal MinConfidence,
    int Take = 100)
{
    internal ApproveBatchCommand ToCommand() => new(MinConfidence, Take);
}
