using ErrorOr;
using Mediator;

namespace Budgie.Modules.Finances.Application.Goals.GetGoals;

public record GetGoalsQuery() : IRequest<ErrorOr<List<GoalResponse>>>;
