namespace Budgie.Modules.EmailImport.Infrastructure.Logging;

/// <summary>
/// Compile-time source-generated log messages for the EmailImport module
/// (convention: <c>AGENTS.md</c> — handlers never call <c>ILogger.Log*</c> directly).
/// The module owns EventIds 1..17. This file defines EventIds 1–7 (the plan §13 names
/// that had no owner yet: <see cref="MessageSkippedNoRule"/>, <see cref="MessageParseFailed"/>,
/// <see cref="MessageAutoCommitted"/>, <see cref="MessageStaged"/>, <see cref="CommitFailed"/>,
/// <see cref="ProviderAuthFailed"/>, <see cref="ProviderUnavailable"/>), EventId 9
/// (<see cref="KeyDerivedFromJwtSecret"/>), EventIds 11–14 (slice 8, provider client)
/// and EventIds 15–17 (slice 11, sync lifecycle: <see cref="SyncStarted"/> and
/// <see cref="SyncCompleted"/> are emitted by <c>EmailSyncService</c> so both the worker
/// tick and the SyncNow endpoint are logged; <see cref="SyncTickFailed"/> is emitted by
/// <c>EmailSyncWorker</c>).
/// NOTE on the id map: the plan assigned 1/2 to SyncStarted/SyncCompleted, but slice 11
/// already shipped them as 15/16 — names are never duplicated and existing ids are never
/// re-used, so the seven remaining plan names take the free ids 1–7 in plan order.
/// EventIds 8 and 10 stay unassigned. Do NOT reuse an already-assigned EventId.
/// </summary>
internal static partial class LogMessages
{
    /// <summary>
    /// Debug for a fetched message no rule matched: counted in
    /// <see cref="Application.Inbox.EmailSyncResult.Skipped"/> and never persisted (spec §13 —
    /// persisting would require nullable amount/date for rows that are not transactions).
    /// Carries only identifiers, never the email body.
    /// </summary>
    [LoggerMessage(EventId = 1, Level = LogLevel.Debug, Message = "EmailImport: message {MessageId} matched no import rule ({SkipReason}); skipped (LastSkippedCount).")]
    public static partial void MessageSkippedNoRule(ILogger logger, string messageId, string skipReason);

    /// <summary>
    /// Debug for a message whose parse pipeline threw. No staging row can exist for it
    /// (the domain requires a positive amount), so the failure surfaces through
    /// <see cref="Application.Inbox.EmailSyncResult.Failed"/> and the sync moves on.
    /// </summary>
    [LoggerMessage(EventId = 2, Level = LogLevel.Debug, Message = "EmailImport: parsing message {MessageId} threw {ExceptionType}; counted as failed and skipped.")]
    public static partial void MessageParseFailed(ILogger logger, string messageId, string exceptionType);

    /// <summary>
    /// Info when a message was auto-committed into Finances through
    /// <c>CreateTransactionCommand</c> (confidence ≥ threshold and rule.AutoCommit).
    /// </summary>
    [LoggerMessage(EventId = 3, Level = LogLevel.Information, Message = "EmailImport: message {MessageId} auto-committed as transaction {TransactionId}.")]
    public static partial void MessageAutoCommitted(ILogger logger, string messageId, Guid transactionId);

    /// <summary>
    /// Info when a message was staged as a Pending row for the review inbox.
    /// </summary>
    [LoggerMessage(EventId = 4, Level = LogLevel.Information, Message = "EmailImport: message {MessageId} staged as pending {PendingId} for review.")]
    public static partial void MessageStaged(ILogger logger, string messageId, Guid pendingId);

    /// <summary>
    /// Warning when <c>CreateTransactionCommand</c> answered with an Error (validation,
    /// missing account, ControlStartDate…) or threw for a non-unique-violation reason: the
    /// staging row is marked Failed carrying the description, so the user can correct it.
    /// </summary>
    [LoggerMessage(EventId = 5, Level = LogLevel.Warning, Message = "EmailImport: commit failed for message {MessageId}: {Reason}.")]
    public static partial void CommitFailed(ILogger logger, string messageId, string reason);

    /// <summary>
    /// Warning when the provider rejected the mailbox credential (401/403): the connection
    /// is disabled with LastError set and the user must reconnect. Other users' connections
    /// are unaffected (spec §9).
    /// </summary>
    [LoggerMessage(EventId = 6, Level = LogLevel.Warning, Message = "EmailImport: provider rejected the credential for mailbox {Email}; connection disabled. Reconnect the mailbox to resume syncing.")]
    public static partial void ProviderAuthFailed(ILogger logger, string email);

    /// <summary>
    /// Warning when the provider failed without an auth problem (429, 5xx, network): the
    /// cursor is NOT advanced and the next tick retries (spec §9).
    /// </summary>
    [LoggerMessage(EventId = 7, Level = LogLevel.Warning, Message = "EmailImport: provider unavailable for mailbox {Email} ({Error}); cursor not advanced, will retry next tick.")]
    public static partial void ProviderUnavailable(ILogger logger, string email, string error);

    /// <summary>
    /// Warns that the at-rest encryption key fell back to being derived from
    /// <c>Jwt:Secret</c> because <c>EmailImport:EncryptionKey</c> is not configured.
    /// Never contains any secret material.
    /// </summary>
    [LoggerMessage(EventId = 9, Level = LogLevel.Warning, Message = "EmailImport:EncryptionKey is not configured; the credential encryption key was derived from Jwt:Secret. Configure a dedicated EmailImport:EncryptionKey for at-rest encryption.")]
    public static partial void KeyDerivedFromJwtSecret(ILogger logger);

    /// <summary>
    /// Debug detail for a provider call that failed with a non-success HTTP status.
    /// Operation name and status code only — never tokens, credentials or bodies.
    /// </summary>
    [LoggerMessage(EventId = 11, Level = LogLevel.Debug, Message = "EmailImport: provider call {Operation} failed with HTTP status {StatusCode}.")]
    public static partial void ProviderCallFailed(ILogger logger, string operation, int statusCode);

    /// <summary>
    /// Debug detail for a provider call that failed with a network error or timeout.
    /// Never contains any secret material.
    /// </summary>
    [LoggerMessage(EventId = 12, Level = LogLevel.Debug, Message = "EmailImport: provider call {Operation} failed: network error or timeout.")]
    public static partial void ProviderNetworkError(ILogger logger, string operation);

    /// <summary>
    /// Info after the provider history cursor expired and the sync fell back to the
    /// 7-day recovery window. Already-seen messages are discarded by the staging unique
    /// constraint, so the fallback is safe by design.
    /// </summary>
    [LoggerMessage(EventId = 13, Level = LogLevel.Information, Message = "EmailImport: the provider history cursor expired; falling back to the 7-day recovery window. Already-seen messages are discarded by the staging unique constraint.")]
    public static partial void HistoryCursorExpired(ILogger logger);

    /// <summary>
    /// Debug after a preemptive access-token refresh (the stored token was expired or
    /// about to expire). Never logs the token itself.
    /// </summary>
    [LoggerMessage(EventId = 14, Level = LogLevel.Debug, Message = "EmailImport: the stored access token is expired or about to expire; refreshing it before continuing.")]
    public static partial void AccessTokenRefreshed(ILogger logger);

    /// <summary>
    /// Info at the start of a sync pass, with the number of connections it will attempt.
    /// Emitted by <c>EmailSyncService</c> so worker ticks and SyncNow calls are both covered.
    /// </summary>
    [LoggerMessage(EventId = 15, Level = LogLevel.Information, Message = "EmailImport: sync pass started with {ConnectionCount} connection(s).")]
    public static partial void SyncStarted(ILogger logger, int connectionCount);

    /// <summary>
    /// Info at the end of a sync pass with the full tally. <c>ConnectionErrors</c> is a
    /// single joined string ("none" when empty) so one log line carries the whole outcome.
    /// Emitted by <c>EmailSyncService</c>.
    /// </summary>
    [LoggerMessage(EventId = 16, Level = LogLevel.Information, Message = "EmailImport: sync pass completed. Connections: {ConnectionsProcessed}; messages fetched: {MessagesFetched}; auto-committed: {AutoCommitted}; staged: {Staged}; skipped: {Skipped}; failed: {Failed}; connection errors: {ConnectionErrors}.")]
    public static partial void SyncCompleted(
        ILogger logger,
        int connectionsProcessed,
        int messagesFetched,
        int autoCommitted,
        int staged,
        int skipped,
        int failed,
        string connectionErrors);

    /// <summary>
    /// Error when a worker tick fails unexpectedly (exception escaping
    /// <c>EmailSyncService</c>, scope creation failure, …). The timer survives: the next
    /// tick is the retry. Emitted by <c>EmailSyncWorker</c>.
    /// </summary>
    [LoggerMessage(EventId = 17, Level = LogLevel.Error, Message = "EmailImport: sync tick failed unexpectedly.")]
    public static partial void SyncTickFailed(ILogger logger, Exception exception);
}
