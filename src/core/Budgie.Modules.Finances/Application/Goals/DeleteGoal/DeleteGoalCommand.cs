using ErrorOr;
using MediatR;

namespace Budgie.Modules.Finances.Application.Goals.DeleteGoal;

public record DeleteGoalCommand(Guid Id) : IRequest<ErrorOr<Success>>;
