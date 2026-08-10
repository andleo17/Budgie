using Budgie.Modules.Users.Application;
using Budgie.Modules.Users.Domain;
using Budgie.Modules.Users.Infrastructure.Authentication;
using Budgie.Modules.Users.Infrastructure.Logging;
using ErrorOr;
using Mediator;
using BC = BCrypt.Net.BCrypt;

namespace Budgie.Modules.Users.Application.Login;

internal sealed class LoginCommandHandler(
    IUserRepository userRepository,
    IRefreshTokenRepository refreshTokenRepository,
    IJwtProvider jwtProvider,
    IRefreshTokenService refreshTokenService,
    ILogger<LoginCommandHandler> logger)
    : IRequestHandler<LoginCommand, ErrorOr<LoginResponse>>
{
    public async ValueTask<ErrorOr<LoginResponse>> Handle(LoginCommand request, CancellationToken ct)
    {
        var user = await userRepository.GetByEmailAsync(request.Email, ct);

        if (user is null || !BC.Verify(request.Password, user.PasswordHash))
        {
            LogMessages.FailedLoginAttempt(logger, request.Email);
            return Error.Unauthorized("Auth.InvalidCredentials", "Invalid email or password.");
        }

        var token = jwtProvider.Generate(user);
        var (refreshTokenValue, tokenHash) = refreshTokenService.CreateRefreshToken();
        var refreshTokenEntity = RefreshToken.Create(user.Id, tokenHash, DateTime.UtcNow.AddDays(30));
        await refreshTokenRepository.AddAsync(refreshTokenEntity, ct);

        LogMessages.UserLoggedIn(logger, request.Email);
        return new LoginResponse(token, refreshTokenValue);
    }
}
