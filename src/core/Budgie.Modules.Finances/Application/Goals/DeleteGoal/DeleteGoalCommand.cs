using ErrorOr;
using Mediator;

namespace Budgie.Modules.Finances.Application.Goals.DeleteGoal;

public record DeleteGoalCommand(Guid Id) : IRequest<ErrorOr<Success>>;
