using Budgie.Modules.Finances.Application.RecurringTransactions.Shared;
using ErrorOr;
using Mediator;

namespace Budgie.Modules.Finances.Application.RecurringTransactions.GetDueRecurringTransactions;

public record GetDueRecurringTransactionsQuery() : IRequest<ErrorOr<List<DueRecurringResponse>>>;