using Budgie.Modules.Finances.Infrastructure.Database;
using Budgie.Modules.Finances.Infrastructure.Logging;
using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Budgie.Modules.Finances.Application.Goals.UpdateGoal;

internal sealed class UpdateGoalCommandHandler(
    FinancesDbContext dbContext,
    ILogger<UpdateGoalCommandHandler> logger)
    : IRequestHandler<UpdateGoalCommand, ErrorOr<Success>>
{
    public async Task<ErrorOr<Success>> Handle(UpdateGoalCommand request, CancellationToken cancellationToken)
    {
        var goal = await dbContext.Goals.FirstOrDefaultAsync(g => g.Id == request.Id, cancellationToken);

        if (goal is null)
        {
            return Error.NotFound("Goal.NotFound", "Goal not found.");
        }

        var account = await dbContext.Accounts.FirstOrDefaultAsync(a => a.Id == goal.AccountId, cancellationToken);

        if (account is null)
        {
            return Error.NotFound("Account.NotFound", "Account not found.");
        }

        goal.Update(request.TargetAmount, account.Balance.Currency, request.TargetDate);
        await dbContext.SaveChangesAsync(cancellationToken);

        LogMessages.GoalUpdated(logger, request.Id);

        return Result.Success;
    }
}
