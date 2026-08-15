using Budgie.Modules.Finances.Application;
using Budgie.Shared.Abstractions;
using ErrorOr;
using Mediator;

namespace Budgie.Modules.Finances.Application.Goals.GetGoals;

public record GetGoalsQuery() : IRequest<ErrorOr<List<GoalResponse>>>;

internal sealed class GetGoalsQueryHandler(
    IGoalRepository goalRepository,
    ICurrentUserProvider currentUserProvider)
    : IRequestHandler<GetGoalsQuery, ErrorOr<List<GoalResponse>>>
{
    public async ValueTask<ErrorOr<List<GoalResponse>>> Handle(GetGoalsQuery request, CancellationToken ct)
    {
        var userId = currentUserProvider.GetUserId();
        var goals = await goalRepository.GetGoalsByUserIdAsync(userId, ct);
        return goals;
    }
}
