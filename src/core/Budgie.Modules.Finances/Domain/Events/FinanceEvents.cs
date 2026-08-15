using Budgie.Modules.Finances.Application.Transactions.CreateTransaction;
using Budgie.Shared.Domain.Events;
using Budgie.Shared.Enums;

namespace Budgie.Modules.Finances.Domain.Events;

public record AccountCreatedEvent(
    Guid AccountId,
    Guid UserId,
    string Name,
    decimal InitialBalance,
    decimal? CreditLimit,
    string Currency,
    AccountType Type,
    DateTime ControlStartDate) : IDomainEvent;

public record TransactionCreatedEvent(
    Guid TransactionId,
    Guid UserId,
    Guid AccountId,
    Guid? CategoryId,
    decimal Amount,
    string Currency,
    decimal ExchangeRate,
    DateTime Date,
    TransactionType Type,
    List<TransactionParticipantDto>? Participants,
    int Installments,
    Guid? ParentTransactionId,
    bool IsInitialBalance) : IDomainEvent;

public record DebtSettledEvent(
    Guid SettleTransactionId,
    Guid UserId,
    Guid DestinationAccountId,
    Guid PersonId,
    decimal Amount,
    string Currency,
    DateTime Date) : IDomainEvent;
