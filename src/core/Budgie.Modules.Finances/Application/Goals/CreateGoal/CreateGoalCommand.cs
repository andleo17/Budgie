using ErrorOr;
using MediatR;

namespace Budgie.Modules.Finances.Application.Goals.CreateGoal;

public record CreateGoalCommand(
    Guid AccountId,
    decimal TargetAmount,
    DateTime? TargetDate) : IRequest<ErrorOr<Guid>>;
