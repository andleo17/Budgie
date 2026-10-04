using System.Globalization;
using Budgie.Modules.EmailImport.Domain.Staging;
using Budgie.Shared.Abstractions;
using Budgie.Shared.Enums;
using ErrorOr;
using Mediator;

namespace Budgie.Modules.EmailImport.Application.Inbox.GetPending;

/// <inheritdoc cref="GetPendingQuery"/>
/// <remarks>
/// The review row needs the original email (<c>RawBody</c>, spec §8.1 "Ver correo
/// original") and failed rows must be listable (<c>FailureReason</c>): the user has to be
/// able to see why an import failed and correct it. Pending rows page server-side;
/// Failed rows are fetched and paged in memory — they are bounded in practice (a parse
/// or commit error per message) and the repository offers no paged capability for them.
/// </remarks>
internal sealed class GetPendingQueryHandler(
    IPendingEmailTransactionRepository pendingRepository,
    ICurrentUserProvider currentUserProvider)
    : IRequestHandler<GetPendingQuery, ErrorOr<PagedResult<PendingEmailTransactionResponse>>>
{
    /// <summary>
    /// Hard cap on page size. Every row ships its <c>RawBody</c> (the whole email body),
    /// so an unbounded PageSize could stream entire mailboxes to the browser in one
    /// response.
    /// </summary>
    private const int MaxPageSize = 200;

    public async ValueTask<ErrorOr<PagedResult<PendingEmailTransactionResponse>>> Handle(
        GetPendingQuery request, CancellationToken ct)
    {
        // Query-string enum binding accepts ANY integer (Enum.TryParse happily parses
        // undefined values, so "?status=99" binds to (PendingEmailStatus)99), so an unknown
        // status must be rejected here: debt plan §3.3 pins GET /inbox?status=99 → 400.
        if (request.Status is { } statusValue && !Enum.IsDefined(statusValue))
            return Error.Validation(
                "EmailImport.InvalidStatus",
                CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "en"
                    ? $"'{(int)statusValue}' is not a valid inbox status."
                    : $"'{(int)statusValue}' no es un estado válido de la bandeja de revisión.");

        var userId = currentUserProvider.GetUserId();
        var page = Math.Max(1, request.Page);
        var pageSize = Math.Clamp(request.PageSize, 1, MaxPageSize);
        var status = request.Status ?? PendingEmailStatus.Pending;

        var result = status switch
        {
            PendingEmailStatus.Pending => await pendingRepository.GetPendingAsync(userId, page, pageSize, ct),
            PendingEmailStatus.Failed => Paginate(await pendingRepository.GetFailedAsync(userId, ct), page, pageSize),

            // Committed and dismissed rows are not review material: committed transactions
            // live in Finances and dismissed rows are closed. The repository exposes no
            // capability for them on purpose — an empty page is the honest answer.
            _ => Empty<PendingEmailTransaction>(page, pageSize)
        };

        return new PagedResult<PendingEmailTransactionResponse>(
            result.Items.Select(ToResponse).ToList(),
            result.Total,
            result.Page,
            result.PageSize);
    }

    /// <summary>In-memory page over an already-fetched list (failed rows: bounded in practice).</summary>
    private static PagedResult<T> Paginate<T>(IReadOnlyList<T> source, int page, int pageSize) =>
        new(source.Skip((page - 1) * pageSize).Take(pageSize).ToList(), source.Count, page, pageSize);

    private static PagedResult<T> Empty<T>(int page, int pageSize) => new([], 0, page, pageSize);

    private static PendingEmailTransactionResponse ToResponse(PendingEmailTransaction row) => new(
        row.Id,
        row.ConnectionId,
        row.RuleId,
        row.Subject,
        row.Merchant,
        row.Notes,
        row.Amount.Amount,
        row.Currency.Code,
        row.ExchangeRate,
        row.Date,
        row.TransactionType,
        row.Confidence,
        row.FieldConfidenceJson,
        row.RawBody,
        row.FailureReason,
        row.CreatedAt);
}
