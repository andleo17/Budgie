namespace Budgie.Modules.Finances.Application.Goals;

public record GoalResponse(
    Guid Id,
    Guid AccountId,
    string AccountName,
    string Currency,
    decimal CurrentBalance,
    decimal TargetAmount,
    DateTime? TargetDate,
    double ProgressPercentage);
