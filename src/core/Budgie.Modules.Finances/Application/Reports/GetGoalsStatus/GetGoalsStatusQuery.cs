using ErrorOr;
using Mediator;

namespace Budgie.Modules.Finances.Application.Reports.GetGoalsStatus;

public record GetGoalsStatusQuery() : IRequest<ErrorOr<List<GoalStatus>>>;
