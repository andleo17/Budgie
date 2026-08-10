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
}
