using Budgie.Modules.Finances.Infrastructure.Database;
using Budgie.Shared.Domain.ValueObjects;
using Budgie.Shared.Enums;
using ErrorOr;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace Budgie.Modules.Finances.Application.Debts.GetPersonDebts;

internal sealed class GetPersonDebtsQueryHandler(FinancesDbContext dbContext)
    : IRequestHandler<GetPersonDebtsQuery, ErrorOr<List<DebtResponse>>>
{
    public async ValueTask<ErrorOr<List<DebtResponse>>> Handle(GetPersonDebtsQuery request, CancellationToken cancellationToken)
    {
        var debts = await dbContext.TransactionParticipants
            .Where(tp => tp.PersonId == request.PersonId && tp.RemainingAmount.Amount > 0 && !tp.IsTrackingOnly && tp.Transaction.Type == TransactionType.Loan)
            .Include(tp => tp.Transaction)
            .ToListAsync(cancellationToken);

        return debts.Select(tp => new DebtResponse(
            tp.Id,
            tp.TransactionId,
            tp.PersonId,
            tp.ContributedAmount.Amount,
            tp.RemainingAmount.Amount,
            tp.Transaction.Date,
            tp.Transaction.Notes,
            tp.Transaction.Amount.Currency)).ToList();
    }
}
