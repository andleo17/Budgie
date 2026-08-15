using Budgie.Modules.Finances.Application;
using Budgie.Shared.Abstractions;
using ErrorOr;
using Mediator;

namespace Budgie.Modules.Finances.Application.Reports.GetTransactionsSummary;

public record GetTransactionsSummaryQuery(
    DateTime? StartDate = null,
    DateTime? EndDate = null,
    IReadOnlyList<Guid>? AccountIds = null) : IRequest<ErrorOr<GetTransactionsSummaryResponse>>;

public record GetTransactionsSummaryResponse(
    List<CurrencySummary> Currencies,
    string MainCurrency,
    decimal ApproximateIncome,
    decimal ApproximateExpenses);

public record CurrencySummary(decimal Income, decimal Expenses, decimal Balance, string Currency);

internal sealed class GetTransactionsSummaryQueryHandler(
    IReportRepository reportRepository,
    ICurrentUserProvider currentUserProvider,
    IUserSettingsProvider settingsProvider)
    : IRequestHandler<GetTransactionsSummaryQuery, ErrorOr<GetTransactionsSummaryResponse>>
{
    public async ValueTask<ErrorOr<GetTransactionsSummaryResponse>> Handle(GetTransactionsSummaryQuery request, CancellationToken ct)
    {
        var userId = currentUserProvider.GetUserId();
        var now = DateTime.UtcNow;
        var startDate = request.StartDate ?? now.AddDays(-30);
        var endDate = (request.EndDate ?? now).Date.AddDays(1);
        var mainCurrency = await settingsProvider.GetMainCurrencyAsync();

        var summary = await reportRepository.GetTransactionsSummaryAsync(
            userId, startDate, endDate, request.AccountIds, mainCurrency, ct);

        return summary;
    }
}
