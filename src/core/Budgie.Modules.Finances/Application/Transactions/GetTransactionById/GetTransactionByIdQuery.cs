using ErrorOr;
using MediatR;

namespace Budgie.Modules.Finances.Application.Transactions.GetTransactionById;

public record GetTransactionByIdQuery(Guid Id) : IRequest<ErrorOr<TransactionResponse>>;
