using ErrorOr;
using MediatR;

namespace Budgie.Modules.Finances.Application.Transactions.DeleteTransaction;

public record DeleteTransactionCommand(Guid Id) : IRequest<ErrorOr<Success>>;
