using ErrorOr;
using MediatR;

namespace Budgie.Modules.Finances.Application.Reports.GetGoalsStatus;

public record GetGoalsStatusQuery() : IRequest<ErrorOr<List<GoalStatus>>>;
