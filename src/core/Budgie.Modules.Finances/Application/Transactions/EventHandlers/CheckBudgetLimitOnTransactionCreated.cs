using Budgie.Modules.Finances.Application;
using Budgie.Modules.Finances.Domain.Events;
using Budgie.Modules.Finances.Infrastructure.Logging;
using Budgie.Shared.Enums;
using Mediator;
using Microsoft.Extensions.Logging;

namespace Budgie.Modules.Finances.Application.Transactions.EventHandlers;

internal sealed class CheckBudgetLimitOnTransactionCreated(
    IReportRepository reportRepository,
    ILogger<CheckBudgetLimitOnTransactionCreated> logger)
    : INotificationHandler<TransactionCreatedEvent>
{
    public async ValueTask Handle(TransactionCreatedEvent notification, CancellationToken ct)
    {
        if (notification.Type != TransactionType.Expense || notification.CategoryId is null)
            return;

        var start = new DateTime(notification.Date.Year, notification.Date.Month, 1, 0, 0, 0, DateTimeKind.Unspecified);
        var end = start.AddMonths(1);

        var budgets = await reportRepository.GetCategoryBudgetsAsync(notification.UserId, start, end, ct);
        var budget = budgets.FirstOrDefault(b => b.CategoryId == notification.CategoryId.Value);

        if (budget is null || budget.MonthlyBudget <= 0)
            return;

        if (budget.Percentage >= 100)
            LogMessages.BudgetExceeded(logger, budget.CategoryName, budget.Spent, budget.MonthlyBudget);
        else if (budget.Percentage >= 80)
            LogMessages.BudgetWarning(logger, budget.CategoryName, budget.Spent, budget.MonthlyBudget);
    }
}
