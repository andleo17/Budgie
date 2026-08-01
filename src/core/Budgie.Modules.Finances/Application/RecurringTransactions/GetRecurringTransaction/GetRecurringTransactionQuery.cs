using Budgie.Modules.Finances.Application.RecurringTransactions.Shared;
using ErrorOr;
using MediatR;

namespace Budgie.Modules.Finances.Application.RecurringTransactions.GetRecurringTransaction;

public record GetRecurringTransactionQuery(Guid Id) : IRequest<ErrorOr<RecurringTransactionResponse>>;