using Budgie.Modules.Finances.Infrastructure.Database;
using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Budgie.Modules.Finances.Application.Goals.GetGoals;

internal sealed class GetGoalsQueryHandler(FinancesDbContext dbContext)
    : IRequestHandler<GetGoalsQuery, ErrorOr<List<GoalResponse>>>
{
    public async Task<ErrorOr<List<GoalResponse>>> Handle(GetGoalsQuery request, CancellationToken cancellationToken)
    {
        var goalsData = await dbContext.Goals
            .AsNoTracking()
            .Join(dbContext.Accounts.AsNoTracking(),
                g => g.AccountId,
                a => a.Id,
                (g, a) => new { Goal = g, Account = a })
            .ToListAsync(cancellationToken);

        var goals = goalsData.Select(x =>
        {
            var targetAmount = x.Goal.TargetAmount.Amount > 0 ? x.Goal.TargetAmount.Amount : 1m;
            return new GoalResponse(
                x.Goal.Id,
                x.Account.Id,
                x.Account.Name,
                x.Account.Balance.Currency,
                x.Account.Balance.Amount,
                x.Goal.TargetAmount.Amount,
                x.Goal.TargetDate,
                (double)(x.Account.Balance.Amount / targetAmount * 100));
        }).ToList();

        return goals;
    }
}
