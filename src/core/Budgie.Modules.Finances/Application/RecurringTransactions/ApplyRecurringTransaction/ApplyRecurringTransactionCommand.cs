using ErrorOr;
using Mediator;

namespace Budgie.Modules.Finances.Application.RecurringTransactions.ApplyRecurringTransaction;

public record ApplyRecurringTransactionCommand(Guid Id) : IRequest<ErrorOr<Guid>>;