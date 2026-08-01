using Budgie.Modules.Finances.Domain.Goals;
using Budgie.Modules.Finances.Infrastructure.Database;
using Budgie.Modules.Finances.Infrastructure.Logging;
using Budgie.Shared.Abstractions;
using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Budgie.Modules.Finances.Application.Goals.CreateGoal;

internal sealed class CreateGoalCommandHandler(
    FinancesDbContext dbContext,
    ICurrentUserProvider currentUserProvider,
    ILogger<CreateGoalCommandHandler> logger)
    : IRequestHandler<CreateGoalCommand, ErrorOr<Guid>>
{
    public async Task<ErrorOr<Guid>> Handle(CreateGoalCommand request, CancellationToken cancellationToken)
    {
        var account = await dbContext.Accounts.FirstOrDefaultAsync(a => a.Id == request.AccountId, cancellationToken);

        if (account is null)
        {
            return Error.NotFound("Account.NotFound", "Account not found.");
        }

        var userId = currentUserProvider.GetUserId();

        var goal = Goal.Create(userId, request.AccountId, request.TargetAmount, account.Balance.Currency, request.TargetDate);

        dbContext.Goals.Add(goal);
        await dbContext.SaveChangesAsync(cancellationToken);

        LogMessages.GoalCreated(logger, goal.Id);

        return goal.Id;
    }
}
