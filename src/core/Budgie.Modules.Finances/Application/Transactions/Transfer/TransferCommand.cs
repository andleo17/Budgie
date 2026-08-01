using ErrorOr;
using MediatR;

namespace Budgie.Modules.Finances.Application.Transactions.Transfer;

public record TransferCommand(
    Guid FromAccountId,
    Guid ToAccountId,
    decimal Amount,
    decimal Fee,
    string Currency,
    decimal ExchangeRate,
    DateTime Date,
    string? Notes) : IRequest<ErrorOr<Guid>>;
