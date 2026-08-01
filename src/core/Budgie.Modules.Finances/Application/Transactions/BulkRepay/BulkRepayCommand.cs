using ErrorOr;
using MediatR;

namespace Budgie.Modules.Finances.Application.Transactions.BulkRepay;

public record DebtRepayment(Guid ParticipantId, decimal Amount, Guid TransactionId);

public record BulkRepayCommand(
    Guid AccountId,
    decimal Amount,
    string Currency,
    decimal ExchangeRate,
    DateTime Date,
    string? Notes,
    List<DebtRepayment> Debts) : IRequest<ErrorOr<Guid>>;
