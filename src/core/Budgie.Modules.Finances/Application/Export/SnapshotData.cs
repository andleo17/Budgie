using ErrorOr;
using Mediator;

namespace Budgie.Modules.Finances.Application.Export;

public record ExportSnapshotQuery : IRequest<ErrorOr<SnapshotData>>;

public record RestoreSnapshotCommand(SnapshotData Snapshot) : IRequest<ErrorOr<Success>>;

public record SnapshotData(
    string Version,
    DateTime ExportedAt,
    List<SnapshotAccount> Accounts,
    List<SnapshotCategory> Categories,
    List<SnapshotTransaction> Transactions,
    List<SnapshotParticipant> Participants,
    List<SnapshotRepayment> Repayments,
    List<SnapshotRecurringTransaction> RecurringTransactions,
    List<SnapshotGoal> Goals);

public record SnapshotAccount(
    Guid Id, string Name, decimal Balance, string Currency, int Type,
    decimal? CreditLimit, string? CreditLimitCurrency, int CreditConsumptionMode,
    decimal? AnnualInterestRate, bool BillingCycleDayInclusive,
    int? BillingCycleDay, int? PaymentDueDay, int InterestCalculationMethod,
    DateTime ControlStartDate, DateTime CreatedAt);

public record SnapshotCategory(
    Guid Id, string Name, string? Color, string? Icon, int Type,
    bool IsSystem, Guid? ParentId, decimal? MonthlyBudget);

public record SnapshotTransaction(
    Guid Id, Guid AccountId, Guid? CategoryId, decimal Amount, string Currency,
    decimal ExchangeRate, DateTime Date, string? Notes, int Type, int Installments,
    Guid? LinkedTransactionId, Guid? ParentTransactionId, bool IsInitialBalance);

public record SnapshotParticipant(
    Guid TransactionId, Guid PersonId, decimal ContributedAmount, string ContributedCurrency,
    decimal RemainingAmount, string RemainingCurrency, bool IsTrackingOnly, Guid? DestinationAccountId);

public record SnapshotRepayment(
    Guid RepaymentTransactionId, Guid RepaidParticipantId, decimal Amount);

public record SnapshotRecurringTransaction(
    Guid Id, Guid AccountId, Guid? CategoryId, string Name, decimal Amount, string Currency,
    decimal ExchangeRate, string? Notes, int Type, int Installments, bool IsInterestFree,
    int Frequency, int? CustomIntervalDays, DateTime? StartDate, DateTime? EndDate,
    int? EndAfterCount, int? EndType, int Applicability, DateTime? NextDueDate,
    DateTime? LastAppliedDate, int TimesApplied, bool IsActive);

public record SnapshotGoal(
    Guid Id, Guid AccountId, decimal TargetAmount, string TargetCurrency,
    DateTime? TargetDate, DateTime CreatedAt);
