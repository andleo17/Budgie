using ErrorOr;
using MediatR;

namespace Budgie.Modules.Finances.Application.RecurringTransactions.DeleteRecurringTransaction;

public record DeleteRecurringTransactionCommand(Guid Id) : IRequest<ErrorOr<Success>>;