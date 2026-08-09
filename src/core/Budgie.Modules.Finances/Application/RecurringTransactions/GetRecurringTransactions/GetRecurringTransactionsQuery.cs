using Budgie.Modules.Finances.Application.RecurringTransactions.Shared;
using Budgie.Shared.Enums;
using ErrorOr;
using Mediator;

namespace Budgie.Modules.Finances.Application.RecurringTransactions.GetRecurringTransactions;

public record GetRecurringTransactionsQuery(
    RecurringApplicability? Applicability = null,
    bool? IsActive = null) : IRequest<ErrorOr<List<RecurringTransactionResponse>>>;