using Budgie.Modules.Finances.Infrastructure.Database;
using Budgie.Shared.Domain.ValueObjects;
using ErrorOr;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace Budgie.Modules.Finances.Application.Persons.GetPersonSummary;

internal sealed class GetPersonSummaryQueryHandler(FinancesDbContext dbContext)
    : IRequestHandler<GetPersonSummaryQuery, ErrorOr<PersonSummaryResponse>>
{
    public async ValueTask<ErrorOr<PersonSummaryResponse>> Handle(GetPersonSummaryQuery request, CancellationToken cancellationToken)
    {
        var participants = await dbContext.TransactionParticipants
            .Where(tp => tp.PersonId == request.PersonId)
            .Include(tp => tp.Transaction)
                .ThenInclude(t => t.Account)
            .Include(tp => tp.Transaction)
                .ThenInclude(t => t.ChildTransactions)
                    .ThenInclude(c => c.Participants)
            .ToListAsync(cancellationToken);

        // Skip the parent side of parent/child pairs: when a transaction has a child that
        // references the same person (e.g. expense on credit -> child LOAN, cash participant
        // -> child INCOME), the child already represents that economic event, so counting
        // the parent participant too would double the person's totals.
        var effectiveParticipants = participants
            .Where(tp => !tp.Transaction.ChildTransactions.Any(c => c.Participants.Any(cp => cp.PersonId == tp.PersonId)))
            .ToList();

        var currency = effectiveParticipants
            .Select(tp => tp.Transaction.Account.Balance.Currency)
            .FirstOrDefault() ?? "USD";

        var totalContributions = effectiveParticipants.Sum(tp => tp.ContributedAmount.Amount);
        var totalRepaid = effectiveParticipants.Where(tp => tp.RemainingAmount.Amount <= 0 && !tp.IsTrackingOnly).Sum(tp => tp.ContributedAmount.Amount);

        return new PersonSummaryResponse(
            effectiveParticipants.Count,
            totalContributions,
            totalRepaid,
            totalContributions - totalRepaid,
            currency);
    }
}
