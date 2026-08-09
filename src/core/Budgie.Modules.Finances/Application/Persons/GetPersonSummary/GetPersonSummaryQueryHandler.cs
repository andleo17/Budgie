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
            .ToListAsync(cancellationToken);

        var currency = participants
            .Select(tp => tp.Transaction.Account.Balance.Currency)
            .FirstOrDefault() ?? "USD";

        var totalContributions = participants.Sum(tp => tp.ContributedAmount.Amount);
        var totalRepaid = participants.Where(tp => tp.RemainingAmount.Amount <= 0 && !tp.IsTrackingOnly).Sum(tp => tp.ContributedAmount.Amount);

        return new PersonSummaryResponse(
            participants.Count,
            totalContributions,
            totalRepaid,
            totalContributions - totalRepaid,
            currency);
    }
}
