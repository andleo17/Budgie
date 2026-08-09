using ErrorOr;
using Mediator;

namespace Budgie.Modules.Finances.Application.Reports.GetDashboardSummary;

public record GetDashboardSummaryQuery(string MainCurrency) : IRequest<ErrorOr<DashboardSummary>>;
