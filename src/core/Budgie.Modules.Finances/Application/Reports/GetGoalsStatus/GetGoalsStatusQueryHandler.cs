using Budgie.Modules.Finances.Infrastructure.Database;
using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Budgie.Modules.Finances.Application.Reports.GetGoalsStatus;

internal sealed class GetGoalsStatusQueryHandler(FinancesDbContext dbContext)
    : IRequestHandler<GetGoalsStatusQuery, ErrorOr<List<GoalStatus>>>
{
    public async Task<ErrorOr<List<GoalStatus>>> Handle(GetGoalsStatusQuery request, CancellationToken cancellationToken)
    {
        var goalsData = await dbContext.Goals
            .AsNoTracking()
            .Where(g => g.TargetAmount.Amount > 0)
            .Join(dbContext.Accounts.AsNoTracking(),
                g => g.AccountId,
                a => a.Id,
                (g, a) => new { Goal = g, Account = a })
            .ToListAsync(cancellationToken);

        var goals = goalsData.Select(x => new GoalStatus(
            x.Account.Id,
            x.Account.Name,
            x.Account.Balance.Amount,
            x.Goal.TargetAmount.Amount,
            (double)(x.Account.Balance.Amount / x.Goal.TargetAmount.Amount * 100),
            x.Goal.TargetDate)).ToList();

        return goals;
    }
}
