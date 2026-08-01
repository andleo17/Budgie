using ErrorOr;
using MediatR;

namespace Budgie.Modules.Finances.Application.RecurringTransactions.ApplyAllDueRecurringTransactions;

public record ApplyAllDueRecurringTransactionsCommand() : IRequest<ErrorOr<List<Guid>>>;