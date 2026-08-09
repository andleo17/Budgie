using Budgie.Modules.Finances.Application.RecurringTransactions.Shared;
using ErrorOr;
using Mediator;

namespace Budgie.Modules.Finances.Application.RecurringTransactions.GetRecurringTransaction;

public record GetRecurringTransactionQuery(Guid Id) : IRequest<ErrorOr<RecurringTransactionResponse>>;