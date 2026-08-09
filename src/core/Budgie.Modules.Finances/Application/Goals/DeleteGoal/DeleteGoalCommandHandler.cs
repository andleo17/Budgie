using Budgie.Modules.Finances.Infrastructure.Database;
using Budgie.Modules.Finances.Infrastructure.Logging;
using ErrorOr;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace Budgie.Modules.Finances.Application.Goals.DeleteGoal;

internal sealed class DeleteGoalCommandHandler(
    FinancesDbContext dbContext,
    ILogger<DeleteGoalCommandHandler> logger)
    : IRequestHandler<DeleteGoalCommand, ErrorOr<Success>>
{
    public async ValueTask<ErrorOr<Success>> Handle(DeleteGoalCommand request, CancellationToken cancellationToken)
    {
        var goal = await dbContext.Goals.FirstOrDefaultAsync(g => g.Id == request.Id, cancellationToken);

        if (goal is null)
        {
            return Error.NotFound("Goal.NotFound", "Goal not found.");
        }

        dbContext.Goals.Remove(goal);
        await dbContext.SaveChangesAsync(cancellationToken);

        LogMessages.GoalDeleted(logger, request.Id);

        return Result.Success;
    }
}
