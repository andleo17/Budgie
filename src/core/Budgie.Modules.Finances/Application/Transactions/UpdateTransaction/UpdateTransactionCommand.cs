using Budgie.Modules.Finances.Application.Transactions.CreateTransaction;
using Budgie.Shared.Enums;
using ErrorOr;
using Mediator;

namespace Budgie.Modules.Finances.Application.Transactions.UpdateTransaction;

public record UpdateTransactionCommand(
    Guid Id,
    Guid? CategoryId,
    decimal Amount,
    string Currency,
    decimal ExchangeRate,
    DateTime Date,
    string? Notes,
    TransactionType Type,
    int Installments,
    List<TransactionParticipantDto>? Participants = null,
    Guid? ParentTransactionId = null) : IRequest<ErrorOr<Success>>;
