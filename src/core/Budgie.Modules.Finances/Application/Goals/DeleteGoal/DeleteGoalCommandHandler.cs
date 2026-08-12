using Budgie.Modules.Finances.Infrastructure.Logging;
using Budgie.Shared.Abstractions;
using Budgie.Shared.Infrastructure;
using ErrorOr;
using Mediator;

namespace Budgie.Modules.Finances.Application.Goals.DeleteGoal;

public record DeleteGoalCommand(Guid Id) : IRequest<ErrorOr<Success>>;

internal sealed class DeleteGoalCommandHandler(
    IDbConnectionFactory factory,
    ICurrentUserProvider currentUserProvider,
    ILogger<DeleteGoalCommandHandler> logger)
    : IRequestHandler<DeleteGoalCommand, ErrorOr<Success>>
{
    public async ValueTask<ErrorOr<Success>> Handle(DeleteGoalCommand request, CancellationToken ct)
    {
        var userId = currentUserProvider.GetUserId();

        var affected = await factory.ExecuteAsync(
            @"DELETE FROM ""Goals"" WHERE ""Id"" = @Id AND ""UserId"" = @UserId",
            new { Id = request.Id, UserId = userId }, ct);

        if (affected == 0)
            return Error.NotFound("Goal.NotFound", "Goal not found.");

        LogMessages.GoalDeleted(logger, request.Id);

        return Result.Success;
    }
}
