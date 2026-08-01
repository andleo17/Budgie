using ErrorOr;
using MediatR;

namespace Budgie.Modules.Finances.Application.Reports.GetDashboardSummary;

public record GetDashboardSummaryQuery(string MainCurrency) : IRequest<ErrorOr<DashboardSummary>>;
