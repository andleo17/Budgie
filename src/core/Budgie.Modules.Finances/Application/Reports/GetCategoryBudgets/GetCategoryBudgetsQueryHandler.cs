using Budgie.Modules.Finances.Application;
using Budgie.Shared.Abstractions;
using ErrorOr;
using Mediator;

namespace Budgie.Modules.Finances.Application.Reports.GetCategoryBudgets;

public record GetCategoryBudgetsQuery(DateTime? Month = null) : IRequest<ErrorOr<List<CategoryBudget>>>;

public enum BudgetThresholdStatus
{
    Normal = 0,
    Warning = 1,
    Exceeded = 2
}

public record CategoryBudget(
    Guid CategoryId,
    string CategoryName,
    string? Color,
    string? Icon,
    decimal MonthlyBudget,
    decimal Spent,
    decimal Remaining,
    double Percentage,
    BudgetThresholdStatus Status);

internal sealed class GetCategoryBudgetsQueryHandler(
    IReportRepository reportRepository,
    ICurrentUserProvider currentUserProvider)
    : IRequestHandler<GetCategoryBudgetsQuery, ErrorOr<List<CategoryBudget>>>
{
    public async ValueTask<ErrorOr<List<CategoryBudget>>> Handle(GetCategoryBudgetsQuery request, CancellationToken ct)
    {
        var userId = currentUserProvider.GetUserId();
        var month = request.Month?.Date ?? DateTime.UtcNow.Date;
        var start = new DateTime(month.Year, month.Month, 1, 0, 0, 0, DateTimeKind.Unspecified);
        var end = start.AddMonths(1);

        var budgets = await reportRepository.GetCategoryBudgetsAsync(userId, start, end, ct);
        return budgets.Select(b =>
        {
            var percentage = b.MonthlyBudget > 0 ? (double)(b.Spent / b.MonthlyBudget * 100) : 0;
            var status = b.MonthlyBudget > 0
                ? percentage >= 100
                    ? BudgetThresholdStatus.Exceeded
                    : percentage >= 80
                        ? BudgetThresholdStatus.Warning
                        : BudgetThresholdStatus.Normal
                : BudgetThresholdStatus.Normal;
            return b with
            {
                Percentage = Math.Round(percentage, 1),
                Remaining = b.MonthlyBudget - b.Spent,
                Status = status
            };
        }).ToList();
    }
}
