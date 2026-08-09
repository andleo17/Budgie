using ErrorOr;
using Mediator;

namespace Budgie.Modules.Users.Application.Register;

public record RegisterUserCommand(string Email, string Password, string Name) : IRequest<ErrorOr<Guid>>;
