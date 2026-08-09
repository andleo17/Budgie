using Budgie.Modules.Finances.Application.RecurringTransactions.Shared;
using Budgie.Modules.Finances.Infrastructure.Database;
using Budgie.Shared.Abstractions;
using Budgie.Shared.Domain.ValueObjects;
using ErrorOr;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace Budgie.Modules.Finances.Application.RecurringTransactions.GetDueRecurringTransactions;

internal sealed class GetDueRecurringTransactionsQueryHandler(
    FinancesDbContext dbContext,
    ICurrentUserProvider currentUserProvider)
    : IRequestHandler<GetDueRecurringTransactionsQuery, ErrorOr<List<DueRecurringResponse>>>
{
    public async ValueTask<ErrorOr<List<DueRecurringResponse>>> Handle(GetDueRecurringTransactionsQuery request, CancellationToken cancellationToken)
    {
        var userId = currentUserProvider.GetUserId();
        var now = DateTime.UtcNow;

        var due = await dbContext.RecurringTransactions
            .Where(r => r.UserId == userId
                && r.IsActive
                && r.NextDueDate != null
                && r.NextDueDate <= now)
            .OrderBy(r => r.NextDueDate)
            .Select(r => new DueRecurringResponse(
                r.Id, r.Name, r.AccountId, r.Amount.Amount, r.Amount.Currency, r.Type))
            .ToListAsync(cancellationToken);

        return due;
    }
}