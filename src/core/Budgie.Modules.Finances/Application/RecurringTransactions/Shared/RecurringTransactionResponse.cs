using Budgie.Shared.Enums;

namespace Budgie.Modules.Finances.Application.RecurringTransactions.Shared;

public record RecurringTransactionParticipantDto(
    Guid PersonId,
    decimal ContributedAmount,
    decimal RemainingAmount = 0,
    bool IsTrackingOnly = false,
    Guid? DestinationAccountId = null);

public record RecurringTransactionResponse(
    Guid Id,
    string Name,
    Guid AccountId,
    Guid? CategoryId,
    decimal Amount,
    string Currency,
    decimal ExchangeRate,
    TransactionType Type,
    string? Notes,
    int Installments,
    bool IsInterestFree,
    RecurringApplicability Applicability,
    RecurringFrequency? Frequency,
    int? CustomIntervalDays,
    RecurringEndType? EndType,
    int? EndAfterCount,
    DateTime? EndDate,
    DateTime? StartDate,
    DateTime? NextDueDate,
    int TimesApplied,
    DateTime? LastAppliedDate,
    bool IsActive,
    List<RecurringTransactionParticipantDto> Participants);

public record DueRecurringResponse(
    Guid Id,
    string Name,
    Guid AccountId,
    decimal Amount,
    string Currency,
    TransactionType Type);