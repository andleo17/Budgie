using System.Globalization;
using System.Text.Json;
using Budgie.Modules.EmailImport.Application.Abstractions;
using Budgie.Modules.EmailImport.Application.Inbox.Abstractions;
using Budgie.Modules.EmailImport.Application.Parsing;
using Budgie.Modules.EmailImport.Domain.Connections;
using Budgie.Modules.EmailImport.Domain.Parsing;
using Budgie.Modules.EmailImport.Domain.Staging;
using Budgie.Modules.EmailImport.Infrastructure.Logging;
using Budgie.Modules.Finances.Application;
using Budgie.Modules.Finances.Application.Transactions.CreateTransaction;
using Budgie.Shared.Abstractions;
using Budgie.Shared.Enums;
using ErrorOr;
using Mediator;
using Npgsql;

namespace Budgie.Modules.EmailImport.Application.Inbox;

/// <summary>
/// Synchronises email mailboxes into Budgie: fetch → stage → (optionally) auto-commit.
/// This class holds ALL the sync logic of the module (spec §5.1); the worker is only a
/// timer. Every message goes through the double idempotency barrier of spec §3.5.
/// </summary>
/// <remarks>
/// <para>
/// <b>The two invariants this file exists to protect</b> (spec §3.5, §5.1):
/// </para>
/// <list type="number">
/// <item>
/// The sync cursor is persisted <b>only after the whole batch has been processed</b> —
/// never inside the message loop. If the process dies mid-batch, the next tick re-reads
/// the same messages and <c>UNIQUE("UserId","MessageId")</c> discards them. With the
/// cursor saved first, every unprocessed message would be lost forever.
/// </item>
/// <item>
/// <c>NpgsqlException</c> with <c>SqlState 23505</c> from the auto-commit dispatch means
/// "the transaction was ALREADY committed" (the unique index on
/// <c>Transactions."ExternalUid"</c>), not an error: the staging row is marked
/// <c>Committed</c> and the loop continues.
/// </item>
/// </list>
/// <para>
/// <b>No explicit SQL transaction wraps these steps, on purpose.</b> The
/// <c>ExternalUid</c> barrier IS the crash-safety mechanism: staging insert and
/// transaction insert are two independent writes. A transaction around them would only
/// hide bugs — a partial batch would look atomic while silently skipping messages.
/// </para>
/// <para>
/// Precisely what the barrier guarantees: <b>the money is never duplicated</b>
/// (at-least-once delivery + a unique index = exactly-once transactions). It does
/// <b>not</b> reconcile the staging row itself. If the process dies between the two
/// writes, the row is still <c>Pending</c> while its transaction already exists; the
/// next tick sees <c>TryAddAsync == false</c> and moves on, so the row needs the user
/// to approve it once — that approval hits the same barrier (23505) and marks it
/// <c>Committed</c>. That is the only real crash window, and it is rare.
/// </para>
/// </remarks>
internal sealed class EmailSyncService(
    IEmailConnectionRepository connectionsRepository,
    IEmailImportRuleRepository rulesRepository,
    IPendingEmailTransactionRepository pendingRepository,
    IEmailProviderResolver providerResolver,
    IEmailParser parser,
    ITransactionRepository transactionsRepository,
    IMediator mediator,
    ICurrentUserProviderAccessor currentUserAccessor,
    ILanguageProvider languageProvider,
    ILogger<EmailSyncService> logger) : IEmailSyncService
{
    /// <inheritdoc />
    public async Task<EmailSyncResult> SyncAllAsync(CancellationToken ct)
    {
        // The one deliberate exception to tenancy (documented on
        // IEmailConnectionRepository.GetEnabledAsync): the worker has no HttpContext, so
        // every enabled connection is loaded and each one is later processed under its
        // own owner through ICurrentUserProviderAccessor.Push.
        var connections = await connectionsRepository.GetEnabledAsync(ct);
        return await SyncAsync(connections, ct);
    }

    /// <inheritdoc />
    public async Task<EmailSyncResult> SyncUserAsync(Guid userId, CancellationToken ct)
    {
        var connections = (await connectionsRepository.GetAllAsync(userId, ct))
            .Where(connection => connection.SyncEnabled)
            .ToList();
        return await SyncAsync(connections, ct);
    }

    private async Task<EmailSyncResult> SyncAsync(List<EmailConnection> connections, CancellationToken ct)
    {
        LogMessages.SyncStarted(logger, connections.Count);

        var connectionErrors = new List<string>();
        var processed = 0;
        var fetched = 0;
        var autoCommitted = 0;
        var staged = 0;
        var skipped = 0;
        var failed = 0;

        foreach (var connection in connections)
        {
            processed++;
            try
            {
                var outcome = await SyncConnectionAsync(connection, ct);
                fetched += outcome.Fetched;
                autoCommitted += outcome.AutoCommitted;
                staged += outcome.Staged;
                skipped += outcome.Skipped;
                failed += outcome.Failed;
                if (outcome.Error is not null)
                    connectionErrors.Add(outcome.Error);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // Last-resort isolation (spec §9): one user with a broken loop must never
                // take down the sync of the others. The connection is deliberately NOT
                // persisted here — an unexpected failure must not move its cursor.
                connectionErrors.Add($"{connection.Email}: {ex.Message}");
            }
        }

        var result = new EmailSyncResult(
            processed, fetched, autoCommitted, staged, skipped, failed, connectionErrors);

        LogMessages.SyncCompleted(
            logger,
            result.ConnectionsProcessed,
            result.MessagesFetched,
            result.AutoCommitted,
            result.Staged,
            result.Skipped,
            result.Failed,
            result.ConnectionErrors.Count == 0
                ? "none"
                : string.Join(" | ", result.ConnectionErrors));

        return result;
    }

    private async Task<ConnectionOutcome> SyncConnectionAsync(EmailConnection connection, CancellationToken ct)
    {
        // The WHOLE connection runs under its owner: CreateTransactionCommand resolves its
        // UserId through ICurrentUserProvider, which outside an HTTP request is this pushed
        // scope. Same path as the manual review route (spec §5.2) — one implementation,
        // two entry points.
        using var userScope = currentUserAccessor.Push(connection.UserId);

        // Throws InvalidOperationException when the provider is not registered; the outer
        // catch records it and the next connection is processed (spec §5.1 step a).
        var provider = providerResolver.Resolve(connection.Provider);

        // Once per connection, never per message.
        var rules = await rulesRepository.GetByUserAsync(connection.UserId, ct);

        // Snapshot taken BEFORE listing: this is what gets restored when the batch cannot
        // be listed completely. The cursor may only move forward in the database after
        // every message has been processed (invariant #1).
        var previousCursor = connection.SyncCursor;

        IReadOnlyList<EmailMessage> messages;
        try
        {
            messages = await ListNewWithCursorRecoveryAsync(provider, connection, ct);
        }
        catch (EmailProviderException ex)
        {
            return await HandleProviderFailureAsync(connection, ex, previousCursor, ct);
        }

        var fetched = messages.Count;
        var autoCommitted = 0;
        var staged = 0;
        var skipped = 0;
        var failed = 0;

        foreach (var message in messages)
        {
            ct.ThrowIfCancellationRequested();

            ParseOutcome? outcome;
            try
            {
                outcome = await parser.ParseAsync(
                    message, rules, languageProvider.LanguageCode, ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // A broken parse never kills the sync (spec §9). NOTE: no staging row can
                // exist for this message — PendingEmailTransaction.Create requires a
                // positive extracted amount (Money.FromPositive) and the DDL enforces
                // "Amount" NOT NULL; a message that failed to parse has no amount to
                // store. Same conscious decision the plan makes for no-rule emails: the
                // failure surfaces through EmailSyncResult.Failed and the sync moves on.
                failed++;
                LogMessages.MessageParseFailed(logger, message.MessageId, ex.GetType().Name);
                continue;
            }

            if (outcome is null || !outcome.Matched)
            {
                // No rule matched (or a producer returned nothing at all): not persisted
                // (spec §13 decision — persisting would require nullable amount/date for
                // rows that are not transactions).
                if (outcome is null)
                    failed++;
                else
                {
                    skipped++;
                    LogMessages.MessageSkippedNoRule(logger, message.MessageId, outcome.SkipReason ?? "unknown");
                }

                continue;
            }

            var extracted = outcome.Value!;
            var rule = outcome.MatchedRule;

            PendingEmailTransaction pending;
            try
            {
                pending = PendingEmailTransaction.Create(
                    userId: connection.UserId,
                    connectionId: connection.Id,
                    ruleId: rule?.Id,
                    messageId: message.MessageId,
                    threadId: message.ThreadId,
                    subject: message.Subject,
                    merchant: extracted.Merchant,
                    notes: null,
                    amount: extracted.Amount,
                    currency: extracted.Currency,
                    exchangeRate: rule?.ExchangeRate ?? 1m,
                    date: extracted.Date,
                    transactionType: extracted.TransactionType,
                    confidence: extracted.Confidence,
                    fieldConfidenceJson: JsonSerializer.Serialize(extracted.FieldConfidence),
                    rawBody: message.Body,
                    createdAt: DateTime.UtcNow);
            }
            catch (Exception) when (!ct.IsCancellationRequested)
            {
                // The adapter returned fields the domain rejects (invalid currency,
                // non-positive amount, out-of-range confidence): same treatment as a
                // parse failure — counted, never fatal.
                failed++;
                continue;
            }

            // ── First idempotency barrier (spec §3.5): the staging INSERT itself. It is
            // the FIRST write of this message's pipeline — before any commit attempt —
            // implemented as ON CONFLICT ("UserId","MessageId") DO NOTHING plus the
            // affected-row count. false = the row already exists (duplicate, typically a
            // re-read after a crash): the message is discarded and
            // CreateTransactionCommand is NEVER dispatched for it.
            var inserted = await pendingRepository.TryAddAsync(pending, ct);
            if (!inserted)
                continue;

            if (rule?.AutoCommit != true ||
                extracted.Confidence < connection.AutoCommitMinConfidence)
            {
                // Below the auto-commit threshold, or the rule does not auto-commit:
                // the row stays Pending in the review inbox (spec §5.1 step d).
                staged++;
                LogMessages.MessageStaged(logger, message.MessageId, pending.Id);
                continue;
            }

            // ── Auto-commit (spec §5.2): dispatch through Mediator, never write Finances
            // directly — that is what inherits ControlStartDate validation, installment
            // math, participants and the TransactionCreatedEvent that moves the balance.
            var externalUid =
                $"email:{connection.Provider.ToString().ToLowerInvariant()}:{message.MessageId}";

            ErrorOr<Guid> result;
            try
            {
                result = await mediator.Send(
                    new CreateTransactionCommand(
                        AccountId: rule!.AccountId,
                        CategoryId: rule.CategoryId,
                        Amount: extracted.Amount,
                        Currency: extracted.Currency,
                        ExchangeRate: rule.ExchangeRate ?? 1m,
                        Date: extracted.Date,
                        Notes: null,
                        Type: extracted.TransactionType,
                        ExternalUid: externalUid),
                    ct);
            }
            catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.UniqueViolation)
            {
                // SECOND idempotency barrier (spec §3.5): SqlState 23505 on the unique
                // index IX_Transactions_UserId_ExternalUid means the transaction was
                // ALREADY committed — most likely the process died between this dispatch
                // and the staging update of a previous pass. That is a success, not an
                // error: link the row to the existing transaction and continue.
                var existingId = await transactionsRepository.GetIdByExternalUidAsync(
                    connection.UserId, externalUid, ct);

                if (existingId is null)
                {
                    // Defensive race: the transaction existed at insert time but is gone
                    // now (deleted before this lookup). Surface it as Failed so the review
                    // inbox can decide — never invent a link.
                    await pendingRepository.MarkFailedAsync(
                        connection.UserId,
                        pending.Id,
                        BuildLostTransactionMessage(),
                        DateTime.UtcNow,
                        ct);
                    failed++;
                }
                else
                {
                    await pendingRepository.MarkCommittedAsync(
                        connection.UserId,
                        pending.Id,
                        existingId.Value,
                        DateTime.UtcNow,
                        ct);
                    autoCommitted++;
                }

                continue;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // The dispatch itself blew up (not an ErrorOr result): mark the row and
                // keep the batch going.
                await pendingRepository.MarkFailedAsync(
                    connection.UserId, pending.Id, ex.Message, DateTime.UtcNow, ct);
                LogMessages.CommitFailed(logger, message.MessageId, ex.Message);
                failed++;
                continue;
            }

            if (result.IsError)
            {
                // Validation error (Transaction.BeforeControlStartDate, missing account…):
                // the row becomes Failed carrying the very description the UI shows, so
                // the user can correct it (spec §5.2, §9).
                await pendingRepository.MarkFailedAsync(
                    connection.UserId,
                    pending.Id,
                    result.Errors[0].Description,
                    DateTime.UtcNow,
                    ct);
                LogMessages.CommitFailed(logger, message.MessageId, result.Errors[0].Description);
                failed++;
                continue;
            }

            await pendingRepository.MarkCommittedAsync(
                connection.UserId, pending.Id, result.Value, DateTime.UtcNow, ct);
            LogMessages.MessageAutoCommitted(logger, message.MessageId, result.Value);
            autoCommitted++;
        }

        // ═══ INVARIANT #1 — the cursor is persisted ONLY here, after every message of
        // the batch has been processed. If the process dies anywhere above, the next tick
        // re-reads the same messages and UNIQUE("UserId","MessageId") discards them;
        // saving the cursor earlier would silently skip every unprocessed message.
        // The provider may have already established the new cursor in memory while
        // listing (see IEmailProviderClient.ListNewAsync); adopting it here — and
        // persisting it through UpdateAsync — is the single point where it becomes real.
        var newCursor = connection.SyncCursor;
        connection.AdvanceCursor(newCursor);
        connection.RecordSuccess(DateTime.UtcNow, skipped);
        await connectionsRepository.UpdateAsync(connection, ct);

        return new ConnectionOutcome(fetched, autoCommitted, staged, skipped, failed, null);
    }

    /// <summary>
    /// Lists new messages, recovering once from a <see cref="EmailProviderError.CursorExpired"/>
    /// (spec §5.1 step b): reset the cursor and retry with the provider's recovery window.
    /// The staging <c>UNIQUE</c> discards everything already seen, so re-reading is safe.
    /// </summary>
    private async Task<IReadOnlyList<EmailMessage>> ListNewWithCursorRecoveryAsync(
        IEmailProviderClient provider, EmailConnection connection, CancellationToken ct)
    {
        try
        {
            return await provider.ListNewAsync(connection, ct);
        }
        catch (EmailProviderException ex) when (ex.Error == EmailProviderError.CursorExpired)
        {
            LogMessages.HistoryCursorExpired(logger);
            connection.AdvanceCursor(null);
            var messages = await provider.ListNewAsync(connection, ct);

            // Re-anchor the cursor at the provider's current position. Without this the
            // connection would fall back to its recovery window on EVERY tick — a window
            // listing never advances a cursor — re-reading a week of mail forever.
            // Ordering note: the retry above runs with a null cursor (that is what makes
            // it a recovery-window read); the re-anchor comes after it on purpose.
            // Best-effort: if the provider cannot report its current position the cursor
            // stays null, which is safe (dedupe) but wasteful — and the failure has
            // already been logged by the provider client itself.
            try
            {
                var currentCursor = await provider.GetCurrentCursorAsync(connection, ct);
                if (currentCursor is not null)
                    connection.AdvanceCursor(currentCursor);
            }
            catch (EmailProviderException)
            {
                // Best-effort re-anchor; see comment above.
            }

            return messages;
        }
    }

    /// <summary>
    /// Maps a typed provider failure to connection state (spec §9). In every branch the
    /// cursor is left where the database already has it: a failed listing is not progress.
    /// </summary>
    private async Task<ConnectionOutcome> HandleProviderFailureAsync(
        EmailConnection connection,
        EmailProviderException ex,
        string? previousCursor,
        CancellationToken ct)
    {
        switch (ex.Error)
        {
            case EmailProviderError.Unauthorized:
                // Credential rejected or revoked: disable the connection and tell the user
                // to reconnect. Other users' connections are unaffected (spec §9).
                connection.Disable();
                connection.RecordError(BuildUnauthorizedMessage());
                LogMessages.ProviderAuthFailed(logger, connection.Email);
                await connectionsRepository.UpdateAsync(connection, ct);
                return ConnectionOutcome.WithError(BuildUnauthorizedMessage());

            case EmailProviderError.CursorExpired:
                // The provider surfaced CursorExpired even after the single in-service
                // recovery retry (defensive — providers normally swallow it): the
                // persisted cursor is dead. Drop it so the next tick starts from the
                // recovery window instead of looping on a dead cursor forever.
                connection.AdvanceCursor(null);
                connection.RecordError(ex.Message);
                await connectionsRepository.UpdateAsync(connection, ct);
                return ConnectionOutcome.WithError($"{connection.Email}: {ex.Message}");

            default:
                // RateLimited / Transient / NotConfigured: back off and retry on the next
                // tick. The cursor must NOT move — persisting the provider's in-memory
                // cursor here would skip every message of a partially listed batch.
                connection.AdvanceCursor(previousCursor);
                connection.RecordError(ex.Message);
                LogMessages.ProviderUnavailable(logger, connection.Email, ex.Error.ToString());
                await connectionsRepository.UpdateAsync(connection, ct);
                return ConnectionOutcome.WithError($"{connection.Email}: {ex.Message}");
        }
    }

    private static string BuildUnauthorizedMessage() =>
        CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "en"
            ? "The provider rejected the mailbox credential (revoked or expired). Reconnect the mailbox to resume syncing."
            : "El proveedor rechazó la credencial del buzón (revocada o caducada). Reconecta el buzón para reanudar la sincronización.";

    private static string BuildLostTransactionMessage() =>
        CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "en"
            ? "The transaction already existed (imported earlier) but could not be found afterwards; review this row manually."
            : "La transacción ya existía (importada antes) pero no se encontró después; revisa esta fila manualmente.";

    /// <summary>Per-connection tally rolled up into <see cref="EmailSyncResult"/>.</summary>
    private sealed record ConnectionOutcome(
        int Fetched,
        int AutoCommitted,
        int Staged,
        int Skipped,
        int Failed,
        string? Error)
    {
        public static ConnectionOutcome WithError(string error) => new(0, 0, 0, 0, 0, error);
    }
}
