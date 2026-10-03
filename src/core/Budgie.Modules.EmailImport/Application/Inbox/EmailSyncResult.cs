namespace Budgie.Modules.EmailImport.Application.Inbox;

/// <summary>
/// Outcome of one sync pass (spec §2.4). Pure data: the worker and the SyncNow endpoint
/// decide what to do with it — the sync service itself never writes it anywhere.
/// </summary>
/// <param name="ConnectionsProcessed">Connections the pass attempted, including the ones that failed.</param>
/// <param name="MessagesFetched">Messages the providers returned (before dedupe).</param>
/// <param name="AutoCommitted">
/// Rows committed to Finances during this pass. A row committed by the second idempotency
/// barrier (<c>SqlState 23505</c>: it was already committed) also counts here — the end
/// state of the row is <see cref="Domain.Staging.PendingEmailStatus.Committed"/>.
/// </param>
/// <param name="Staged">Rows inserted and left <see cref="Domain.Staging.PendingEmailStatus.Pending"/> for the review inbox.</param>
/// <param name="Skipped">Messages no rule matched. They are never persisted (spec §13 decision).</param>
/// <param name="Failed">Rows marked <see cref="Domain.Staging.PendingEmailStatus.Failed"/> (validation or commit errors).</param>
/// <param name="ConnectionErrors">
/// Human-readable per-connection failures (provider errors, unresolvable providers,
/// unexpected exceptions). One broken connection never stops the others (spec §9).
/// </param>
public sealed record EmailSyncResult(
    int ConnectionsProcessed,
    int MessagesFetched,
    int AutoCommitted,
    int Staged,
    int Skipped,
    int Failed,
    IReadOnlyList<string> ConnectionErrors);
