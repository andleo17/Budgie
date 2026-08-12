using Budgie.Modules.Finances.Application.RecurringTransactions.Shared;
using Budgie.Shared.Abstractions;
using ErrorOr;
using Mediator;

namespace Budgie.Modules.Finances.Application.RecurringTransactions.GetDueRecurringTransactions;

internal sealed class GetDueRecurringTransactionsQueryHandler(
    IRecurringTransactionRepository recurringTransactionRepository,
    ICurrentUserProvider currentUserProvider)
    : IRequestHandler<GetDueRecurringTransactionsQuery, ErrorOr<List<DueRecurringResponse>>>
{
    public async ValueTask<ErrorOr<List<DueRecurringResponse>>> Handle(GetDueRecurringTransactionsQuery request, CancellationToken ct)
    {
        var userId = currentUserProvider.GetUserId();
        return await recurringTransactionRepository.GetDueResponsesAsync(userId, ct);
    }
}
