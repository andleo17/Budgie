using Budgie.Modules.Finances.Application.Reports.GetCategoryBudgets;
using Budgie.Modules.Finances.Application.Reports.GetCategorySpending;
using Budgie.Modules.Finances.Application.Reports.GetDashboardSummary;
using Budgie.Modules.Finances.Application.Reports.GetGoalsStatus;
using Budgie.Modules.Finances.Application.Reports.GetTransactionsSummary;

namespace Budgie.Modules.Finances.Application;

public interface IReportRepository
{
    Task<DashboardSummary> GetDashboardSummaryAsync(Guid userId, string mainCurrency, CancellationToken ct);
    Task<List<CategorySpending>> GetCategorySpendingAsync(Guid userId, DateTime startDate, DateTime endDate, CancellationToken ct);
    Task<List<CategoryBudget>> GetCategoryBudgetsAsync(Guid userId, DateTime startDate, DateTime endDate, CancellationToken ct);
    Task<List<GoalStatus>> GetGoalsStatusAsync(Guid userId, CancellationToken ct);
    Task<GetTransactionsSummaryResponse> GetTransactionsSummaryAsync(Guid userId, DateTime startDate, DateTime endDate, IReadOnlyList<Guid>? accountIds, string mainCurrency, CancellationToken ct);
}
