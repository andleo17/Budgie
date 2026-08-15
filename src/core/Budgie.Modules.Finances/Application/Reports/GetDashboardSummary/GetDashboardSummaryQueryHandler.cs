using Budgie.Modules.Finances.Application;
using Budgie.Shared.Abstractions;
using ErrorOr;
using Mediator;

namespace Budgie.Modules.Finances.Application.Reports.GetDashboardSummary;

public record GetDashboardSummaryQuery : IRequest<ErrorOr<DashboardSummary>>;

public record DashboardSummary(decimal TotalBalance, decimal MonthlyIncome,
    decimal MonthlyExpenses, decimal NetFlow, string Currency);

internal sealed class GetDashboardSummaryQueryHandler(
    IReportRepository reportRepository,
    ICurrentUserProvider currentUserProvider,
    IUserSettingsProvider settingsProvider)
    : IRequestHandler<GetDashboardSummaryQuery, ErrorOr<DashboardSummary>>
{
    public async ValueTask<ErrorOr<DashboardSummary>> Handle(GetDashboardSummaryQuery request, CancellationToken ct)
    {
        var userId = currentUserProvider.GetUserId();
        var mainCurrency = await settingsProvider.GetMainCurrencyAsync();
        var summary = await reportRepository.GetDashboardSummaryAsync(userId, mainCurrency, ct);
        return summary;
    }
}
