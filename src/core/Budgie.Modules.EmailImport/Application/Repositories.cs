using Budgie.Modules.EmailImport.Domain.Connections;
using Budgie.Modules.EmailImport.Domain.Rules;
using Budgie.Modules.EmailImport.Domain.Staging;
using Budgie.Shared.Enums;

namespace Budgie.Modules.EmailImport.Application;

/// <summary>
/// One page of items plus the total count of the filtered set, used by the review inbox.
/// Declared here because no equivalent exists in <c>Budgie.Shared/Dtos</c>.
/// </summary>
public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Total, int Page, int PageSize);

/// <summary>
/// Persistence for mailbox connections. Multi-tenancy is part of every signature: each method
/// takes <c>userId</c> as its first parameter and every query filters
/// <c>WHERE "UserId" = @UserId</c>.
/// </summary>
public interface IEmailConnectionRepository
{
    Task<EmailConnection?> GetByIdAsync(Guid userId, Guid id, CancellationToken ct);
    Task<EmailConnection?> GetByProviderAsync(Guid userId, EmailProvider provider, CancellationToken ct);
    Task<bool> ExistsByProviderAsync(Guid userId, EmailProvider provider, CancellationToken ct);
    Task<List<EmailConnection>> GetAllAsync(Guid userId, CancellationToken ct);

    /// <summary>
    /// Returns every connection with sync enabled, across all users.
    /// Deliberate exception to the tenancy rule: it is consumed by the <c>EmailSyncWorker</c>
    /// (a <c>BackgroundService</c>), which runs outside any HTTP request and therefore has no
    /// <c>HttpContext</c> to read a <c>UserId</c> from. The rows are already filtered by
    /// <c>SyncEnabled = TRUE</c>; each row carries its own <c>UserId</c> and every staging row
    /// persisted from it is written under that user.
    /// </summary>
    Task<List<EmailConnection>> GetEnabledAsync(CancellationToken ct);

    Task AddAsync(EmailConnection connection, CancellationToken ct);
    Task UpdateAsync(EmailConnection connection, CancellationToken ct);
    Task DeleteAsync(Guid userId, Guid id, CancellationToken ct);
}

/// <summary>
/// Persistence for user-defined parsing rules. Multi-tenancy: every method takes
/// <c>userId</c> first and every query filters <c>WHERE "UserId" = @UserId</c>.
/// </summary>
public interface IEmailImportRuleRepository
{
    Task<EmailImportRule?> GetByIdAsync(Guid userId, Guid id, CancellationToken ct);
    Task<List<EmailImportRule>> GetByUserAsync(Guid userId, CancellationToken ct);
    Task AddAsync(EmailImportRule rule, CancellationToken ct);
    Task UpdateAsync(EmailImportRule rule, CancellationToken ct);
    Task DeleteAsync(Guid userId, Guid id, CancellationToken ct);
}

/// <summary>
/// Persistence for the staging inbox. Idempotency relies on <c>TryAddAsync</c>, which inserts
/// with <c>ON CONFLICT ("UserId","MessageId") DO NOTHING</c> — the first of the two barriers
/// described in spec §3.5 (the second being the unique index on <c>Transactions."ExternalUid"</c>).
/// </summary>
public interface IPendingEmailTransactionRepository
{
    /// <summary>
    /// Inserts a staged row unless <c>UNIQUE("UserId","MessageId")</c> already covers it.
    /// Returns <c>false</c> when the message was already staged (duplicate), <c>true</c> when the
    /// row was inserted. Implemented with <c>ON CONFLICT ... DO NOTHING</c> plus the affected row
    /// count, never by catching a constraint violation: this is the hot path executed for every
    /// message of every sync.
    /// </summary>
    Task<bool> TryAddAsync(PendingEmailTransaction pending, CancellationToken ct);

    Task<PendingEmailTransaction?> GetByIdAsync(Guid userId, Guid id, CancellationToken ct);

    /// <summary>
    /// <paramref name="reviewedAt"/> is supplied by the caller because the domain entity never
    /// reads the clock (same reason the columns are <c>TIMESTAMP WITHOUT TIME ZONE</c>).
    /// </summary>
    Task MarkCommittedAsync(Guid userId, Guid id, Guid transactionId, DateTime reviewedAt, CancellationToken ct);

    /// <inheritdoc cref="MarkCommittedAsync"/>
    Task MarkDismissedAsync(Guid userId, Guid id, DateTime reviewedAt, CancellationToken ct);

    /// <inheritdoc cref="MarkCommittedAsync"/>
    Task MarkFailedAsync(Guid userId, Guid id, string reason, DateTime reviewedAt, CancellationToken ct);

    /// <summary>
    /// Persists user corrections from the review inbox. <see cref="PendingEmailTransaction.Status"/>
    /// is written from the entity state and stays <see cref="PendingEmailStatus.Pending"/> after a
    /// review — committing is the handler's job, never a side effect of reviewing.
    /// </summary>
    Task UpdateReviewedAsync(PendingEmailTransaction pending, CancellationToken ct);

    /// <summary>Pending rows only, newest first, with LIMIT/OFFSET and a COUNT(*) for the total.</summary>
    Task<PagedResult<PendingEmailTransaction>> GetPendingAsync(Guid userId, int page, int pageSize, CancellationToken ct);

    /// <summary>Pending rows with <c>"Confidence" &gt;= minConfidence</c>, highest confidence first.</summary>
    Task<List<PendingEmailTransaction>> GetHighConfidenceAsync(Guid userId, decimal minConfidence, int take, CancellationToken ct);

    /// <summary>
    /// Failed rows (parse or commit errors), newest first. Implementation note
    /// (debt plan §6.6): materialises every failed row and pages in memory — a deliberate,
    /// reviewed trade-off (the Failed filter is user-bounded, not an unbounded log); the
    /// accepted risk is response size if that assumption ever breaks.
    /// </summary>
    Task<List<PendingEmailTransaction>> GetFailedAsync(Guid userId, CancellationToken ct);

    Task<int> CountPendingAsync(Guid userId, CancellationToken ct);
}
