using ErrorOr;
using Mediator;

namespace Budgie.Modules.Finances.Application.Transactions.GetTransactionById;

public record GetTransactionByIdQuery(Guid Id) : IRequest<ErrorOr<TransactionResponse>>;
