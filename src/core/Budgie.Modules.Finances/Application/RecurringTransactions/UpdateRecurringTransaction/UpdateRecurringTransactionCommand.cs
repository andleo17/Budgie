using Budgie.Modules.Finances.Application.RecurringTransactions.Shared;
using Budgie.Shared.Enums;
using ErrorOr;
using MediatR;

namespace Budgie.Modules.Finances.Application.RecurringTransactions.UpdateRecurringTransaction;

public record UpdateRecurringTransactionCommand(
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
    List<RecurringTransactionParticipantDto>? Participants = null) : IRequest<ErrorOr<Success>>;