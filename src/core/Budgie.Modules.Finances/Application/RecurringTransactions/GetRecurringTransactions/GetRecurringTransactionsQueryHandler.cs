using Budgie.Modules.Finances.Application.RecurringTransactions.Shared;
using Budgie.Modules.Finances.Infrastructure.Database;
using Budgie.Shared.Abstractions;
using Budgie.Shared.Domain.ValueObjects;
using ErrorOr;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace Budgie.Modules.Finances.Application.RecurringTransactions.GetRecurringTransactions;

internal sealed class GetRecurringTransactionsQueryHandler(
    FinancesDbContext dbContext,
    ICurrentUserProvider currentUserProvider)
    : IRequestHandler<GetRecurringTransactionsQuery, ErrorOr<List<RecurringTransactionResponse>>>
{
    public async ValueTask<ErrorOr<List<RecurringTransactionResponse>>> Handle(GetRecurringTransactionsQuery request, CancellationToken cancellationToken)
    {
        var userId = currentUserProvider.GetUserId();

        var query = dbContext.RecurringTransactions
            .Include(r => r.Participants)
            .Where(r => r.UserId == userId);

        if (request.Applicability.HasValue)
            query = query.Where(r => r.Applicability == request.Applicability.Value);

        if (request.IsActive.HasValue)
            query = query.Where(r => r.IsActive == request.IsActive.Value);

        var templates = await query
            .OrderByDescending(r => r.LastAppliedDate ?? r.StartDate ?? DateTime.MinValue)
            .ToListAsync(cancellationToken);

        return templates.Select(MapResponse).ToList();
    }

    private static RecurringTransactionResponse MapResponse(Domain.RecurringTransactions.RecurringTransaction r)
    {
        return new RecurringTransactionResponse(
            r.Id, r.Name, r.AccountId, r.CategoryId, r.Amount.Amount, r.Amount.Currency,
            r.ExchangeRate, r.Type, r.Notes, r.Installments, r.IsInterestFree,
            r.Applicability, r.Frequency, r.CustomIntervalDays, r.EndType,
            r.EndAfterCount, r.EndDate, r.StartDate, r.NextDueDate,
            r.TimesApplied, r.LastAppliedDate, r.IsActive,
            r.Participants.Select(p => new RecurringTransactionParticipantDto(
                p.PersonId, p.ContributedAmount.Amount, p.RemainingAmount.Amount,
                p.IsTrackingOnly, p.DestinationAccountId)).ToList());
    }
}