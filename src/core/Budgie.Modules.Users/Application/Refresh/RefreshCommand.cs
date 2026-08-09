using Budgie.Modules.Users.Application.Login;
using ErrorOr;
using Mediator;

namespace Budgie.Modules.Users.Application.Refresh;

public record RefreshCommand(string RefreshToken) : IRequest<ErrorOr<LoginResponse>>;
