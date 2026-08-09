using ErrorOr;
using Mediator;

namespace Budgie.Modules.Users.Application.Login;

public record LoginCommand(string Email, string Password) : IRequest<ErrorOr<LoginResponse>>;
