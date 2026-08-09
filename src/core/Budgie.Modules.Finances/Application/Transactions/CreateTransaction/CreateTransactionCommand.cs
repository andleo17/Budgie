using Budgie.Shared.Enums;
using ErrorOr;
using Mediator;

namespace Budgie.Modules.Finances.Application.Transactions.CreateTransaction;

public record CreateTransactionCommand(
    Guid AccountId,
    Guid? CategoryId,
    decimal Amount,
    string Currency,
    decimal ExchangeRate,
    DateTime Date,
    string? Notes,
    TransactionType Type,
    List<TransactionParticipantDto>? Participants = null,
    int Installments = 1,
    Guid? ParentTransactionId = null,
    bool IsInitialBalance = false,
    bool IsInterestFree = true) : IRequest<ErrorOr<Guid>>;

public record TransactionParticipantDto(Guid PersonId, decimal ContributedAmount, decimal RemainingAmount = 0, bool IsTrackingOnly = false, Guid? DestinationAccountId = null);