using ErrorOr;
using Mediator;

namespace Budgie.Modules.Finances.Application.RecurringTransactions.ApplyAllDueRecurringTransactions;

public record ApplyAllDueRecurringTransactionsCommand() : IRequest<ErrorOr<List<Guid>>>;