using ErrorOr;
using MediatR;

namespace Budgie.Modules.Finances.Application.RecurringTransactions.ApplyRecurringTransaction;

public record ApplyRecurringTransactionCommand(Guid Id) : IRequest<ErrorOr<Guid>>;