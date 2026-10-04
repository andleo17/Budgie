using Budgie.Shared.Enums;
using ErrorOr;
using Mediator;

namespace Budgie.Modules.EmailImport.Application.Inbox.GetPending;

/// <summary>
/// Page of the review inbox (spec §8.1). Defaults to <see cref="PendingEmailStatus.Pending"/>
/// — the actionable rows — with <see cref="PendingEmailStatus.Failed"/> available so the UI
/// can show why an import failed and let the user correct it.
/// </summary>
/// <param name="Page">1-based page number. Values below 1 are clamped by the handler.</param>
/// <param name="PageSize">Rows per page. Hard-capped at 200 by the handler.</param>
/// <param name="Status">
/// Status to list; <c>null</c> means Pending. Committed and Dismissed rows are not part of
/// the review inbox (committed transactions live in Finances; dismissed rows are closed)
/// and yield an empty page. A value outside the enum — binding accepts any integer — is
/// rejected with <c>EmailImport.InvalidStatus</c> (debt plan §3.3).
/// </param>
public record GetPendingQuery(
    int Page = 1,
    int PageSize = 50,
    PendingEmailStatus? Status = PendingEmailStatus.Pending)
    : IRequest<ErrorOr<PagedResult<PendingEmailTransactionResponse>>>;

/// <summary>
/// One staging row as the review UI renders it. <c>RawBody</c> is included on purpose: the
/// review row expands "Ver correo original" with it (spec §8.1), and <c>FailureReason</c>
/// lets the user see why a failed import failed so it can be corrected.
/// </summary>
public record PendingEmailTransactionResponse(
    Guid Id,
    Guid ConnectionId,
    Guid? RuleId,
    string? Subject,
    string? Merchant,
    string? Notes,
    decimal Amount,
    string Currency,
    decimal ExchangeRate,
    DateTime Date,
    TransactionType TransactionType,
    decimal Confidence,
    string? FieldConfidenceJson,
    string? RawBody,
    string? FailureReason,
    DateTime CreatedAt);
