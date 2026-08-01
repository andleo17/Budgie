namespace Budgie.Modules.Finances.Application.Reports;

public record DashboardSummary(
    decimal TotalBalance,
    decimal MonthlyIncome,
    decimal MonthlyExpenses,
    decimal NetFlow,
    string Currency);

public record CategorySpending(
    Guid CategoryId,
    string CategoryName,
    string? Color,
    decimal TotalAmount,
    double Percentage);

public record LoanSummary(
    Guid PersonId,
    string PersonName,
    decimal TotalOwed,
    int PendingLoansCount);

public record GoalStatus(
    Guid AccountId,
    string AccountName,
    decimal CurrentBalance,
    decimal TargetAmount,
    double ProgressPercentage,
    DateTime? TargetDate);
