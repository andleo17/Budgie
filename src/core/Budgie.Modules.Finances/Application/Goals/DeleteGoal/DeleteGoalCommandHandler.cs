using Budgie.Modules.Finances.Application;
using Budgie.Modules.Finances.Infrastructure.Logging;
using Budgie.Shared.Abstractions;
using ErrorOr;
using Mediator;
using Microsoft.Extensions.Logging;

namespace Budgie.Modules.Finances.Application.Goals.DeleteGoal;

public record DeleteGoalCommand(Guid Id) : IRequest<ErrorOr<Success>>;

internal sealed class DeleteGoalCommandHandler(
    IGoalRepository goalRepository,
    ICurrentUserProvider currentUserProvider,
    ILogger<DeleteGoalCommandHandler> logger)
    : IRequestHandler<DeleteGoalCommand, ErrorOr<Success>>
{
    public async ValueTask<ErrorOr<Success>> Handle(DeleteGoalCommand request, CancellationToken ct)
    {
        var userId = currentUserProvider.GetUserId();
        var goal = await goalRepository.GetByIdAsync(request.Id, userId, ct);

        if (goal is null)
            return Error.NotFound("Goal.NotFound", "Goal not found.");

        await goalRepository.DeleteAsync(request.Id, userId, ct);

        LogMessages.GoalDeleted(logger, request.Id);

        return Result.Success;
    }
}
