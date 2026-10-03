namespace Budgie.Modules.EmailImport.Application.Inbox;

/// <summary>
/// The whole email synchronisation logic (spec §2.4, §5.1). Deliberately free of timers,
/// clocks-as-dependencies and HTTP concerns: the <c>EmailSyncWorker</c> only opens a scope
/// per tick and calls <see cref="SyncAllAsync"/>; the SyncNow endpoint calls
/// <see cref="SyncUserAsync"/>. Integration tests invoke this interface directly.
/// </summary>
public interface IEmailSyncService
{
    /// <summary>
    /// Syncs every connection with sync enabled across all users — the worker tick.
    /// Each connection is processed under its own owner (see
    /// <see cref="Abstractions.ICurrentUserProviderAccessor"/>).
    /// </summary>
    Task<EmailSyncResult> SyncAllAsync(CancellationToken ct);

    /// <summary>
    /// Syncs a single user's connections (SyncNow endpoint). Disabled connections are
    /// filtered out — the caller cannot sync a mailbox the user turned off.
    /// </summary>
    Task<EmailSyncResult> SyncUserAsync(Guid userId, CancellationToken ct);
}
