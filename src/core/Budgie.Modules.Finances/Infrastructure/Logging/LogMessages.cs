using Budgie.Shared.Infrastructure;
namespace Budgie.Modules.Finances.Infrastructure.Logging;

public static partial class LogMessages
{
    [LoggerMessage(EventId = 1, Level = LogLevel.Information, Message = "Account created: {AccountId} ({AccountName})")]
    public static partial void AccountCreated(ILogger logger, Guid accountId, string accountName);

    [LoggerMessage(EventId = 2, Level = LogLevel.Information, Message = "Account {AccountId} updated")]
    public static partial void AccountUpdated(ILogger logger, Guid accountId);

    [LoggerMessage(EventId = 3, Level = LogLevel.Information, Message = "Account {AccountId} deleted")]
    public static partial void AccountDeleted(ILogger logger, Guid accountId);

    [LoggerMessage(EventId = 4, Level = LogLevel.Information, Message = "Transaction {TransactionId} created ({Type}, {Amount} {Currency})")]
    public static partial void TransactionCreated(ILogger logger, Guid transactionId, string type, decimal amount, string currency);

    [LoggerMessage(EventId = 5, Level = LogLevel.Information, Message = "Transaction {TransactionId} updated")]
    public static partial void TransactionUpdated(ILogger logger, Guid transactionId);

    [LoggerMessage(EventId = 6, Level = LogLevel.Information, Message = "Transaction {TransactionId} deleted")]
    public static partial void TransactionDeleted(ILogger logger, Guid transactionId);

    [LoggerMessage(EventId = 7, Level = LogLevel.Information, Message = "Transfer {FromAccountId} → {ToAccountId}: {Amount} {Currency}")]
    public static partial void TransferCompleted(ILogger logger, Guid fromAccountId, Guid toAccountId, decimal amount, string currency);

    [LoggerMessage(EventId = 8, Level = LogLevel.Information, Message = "Loan repayment {TransactionId} ({Amount} {Currency})")]
    public static partial void LoanRepaid(ILogger logger, Guid transactionId, decimal amount, string currency);

    [LoggerMessage(EventId = 9, Level = LogLevel.Information, Message = "Category created: {CategoryId} ({CategoryName})")]
    public static partial void CategoryCreated(ILogger logger, Guid categoryId, string categoryName);

    [LoggerMessage(EventId = 10, Level = LogLevel.Information, Message = "Category {CategoryId} updated")]
    public static partial void CategoryUpdated(ILogger logger, Guid categoryId);

    [LoggerMessage(EventId = 11, Level = LogLevel.Information, Message = "Category {CategoryId} deleted")]
    public static partial void CategoryDeleted(ILogger logger, Guid categoryId);

    [LoggerMessage(EventId = 12, Level = LogLevel.Information, Message = "Goal created: {GoalId}")]
    public static partial void GoalCreated(ILogger logger, Guid goalId);

    [LoggerMessage(EventId = 13, Level = LogLevel.Information, Message = "Goal {GoalId} updated")]
    public static partial void GoalUpdated(ILogger logger, Guid goalId);

    [LoggerMessage(EventId = 14, Level = LogLevel.Information, Message = "Goal {GoalId} deleted")]
    public static partial void GoalDeleted(ILogger logger, Guid goalId);

    [LoggerMessage(EventId = 15, Level = LogLevel.Information, Message = "Seeded system category: {Name} ({Id})")]
    public static partial void SystemCategorySeeded(ILogger logger, string name, Guid id);

    [LoggerMessage(EventId = 16, Level = LogLevel.Warning, Message = "Category '{CategoryName}' reached {Spent:C} of a {MonthlyBudget:C} monthly budget (Warning threshold).")]
    public static partial void BudgetWarning(ILogger logger, string categoryName, decimal spent, decimal monthlyBudget);

    [LoggerMessage(EventId = 17, Level = LogLevel.Warning, Message = "Category '{CategoryName}' exceeded its {MonthlyBudget:C} monthly budget with {Spent:C} spent.")]
    public static partial void BudgetExceeded(ILogger logger, string categoryName, decimal spent, decimal monthlyBudget);

    [LoggerMessage(EventId = 18, Level = LogLevel.Information, Message = "Money Manager preview started for user {UserId}, file {FileName}")]
    public static partial void MoneyManagerPreviewStarted(ILogger logger, Guid userId, string fileName);

    [LoggerMessage(EventId = 19, Level = LogLevel.Information, Message = "Money Manager preview completed for user {UserId}, file {FileName}, hash {Hash}")]
    public static partial void MoneyManagerPreviewCompleted(ILogger logger, Guid userId, string fileName, string hash);

    [LoggerMessage(EventId = 20, Level = LogLevel.Information, Message = "Money Manager commit started for user {UserId}, file {FileName}, overwrite {Overwrite}")]
    public static partial void MoneyManagerCommitStarted(ILogger logger, Guid userId, string fileName, bool overwrite);

    [LoggerMessage(EventId = 21, Level = LogLevel.Information, Message = "Money Manager commit completed for user {UserId}, file {FileName}, hash {Hash}")]
    public static partial void MoneyManagerCommitCompleted(ILogger logger, Guid userId, string fileName, string hash);

    [LoggerMessage(EventId = 22, Level = LogLevel.Error, Message = "Money Manager commit failed for user {UserId}, file {FileName}")]
    public static partial void MoneyManagerCommitFailed(ILogger logger, Exception exception, Guid userId, string fileName);

    [LoggerMessage(EventId = 23, Level = LogLevel.Warning, Message = "Money Manager import validation failed for user {UserId}, file {FileName}: {ErrorCode}")]
    public static partial void MoneyManagerValidationFailed(ILogger logger, Guid userId, string fileName, string errorCode);

    [LoggerMessage(EventId = 24, Level = LogLevel.Information, Message = "Money Manager parse completed: {AssetGroups} groups, {Assets} assets, {Categories} categories, {Transactions} transactions")]
    public static partial void MoneyManagerParseCompleted(ILogger logger, int assetGroups, int assets, int categories, int transactions);
}
