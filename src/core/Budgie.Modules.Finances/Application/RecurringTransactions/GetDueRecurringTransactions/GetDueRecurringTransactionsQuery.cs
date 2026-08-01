using Budgie.Modules.Finances.Application.RecurringTransactions.Shared;
using ErrorOr;
using MediatR;

namespace Budgie.Modules.Finances.Application.RecurringTransactions.GetDueRecurringTransactions;

public record GetDueRecurringTransactionsQuery() : IRequest<ErrorOr<List<DueRecurringResponse>>>;