using ErrorOr;
using MediatR;

namespace Budgie.Modules.Finances.Application.Goals.UpdateGoal;

public record UpdateGoalCommand(
    Guid Id,
    decimal TargetAmount,
    DateTime? TargetDate) : IRequest<ErrorOr<Success>>;
