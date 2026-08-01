using ErrorOr;
using MediatR;

namespace Budgie.Modules.Finances.Application.Transactions.RepayLoan;

public record RepayLoanCommand(
    Guid OriginalTransactionId,
    decimal Amount,
    string Currency,
    decimal ExchangeRate,
    DateTime Date,
    string? Notes) : IRequest<ErrorOr<Guid>>;
