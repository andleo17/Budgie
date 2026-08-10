using Budgie.Modules.Users.Application;
using Budgie.Modules.Users.Application.Login;
using Budgie.Modules.Users.Domain;
using Budgie.Modules.Users.Infrastructure.Authentication;
using ErrorOr;
using Mediator;

namespace Budgie.Modules.Users.Application.Refresh;

internal sealed class RefreshCommandHandler(
    IUserRepository userRepository,
    IRefreshTokenRepository refreshTokenRepository,
    IJwtProvider jwtProvider,
    IRefreshTokenService refreshTokenService)
    : IRequestHandler<RefreshCommand, ErrorOr<LoginResponse>>
{
    public async ValueTask<ErrorOr<LoginResponse>> Handle(RefreshCommand request, CancellationToken ct)
    {
        var tokenHash = refreshTokenService.HashToken(request.RefreshToken);
        var storedToken = await refreshTokenRepository.GetByHashAsync(tokenHash, ct);

        if (storedToken is null || storedToken.IsRevoked || storedToken.IsExpired)
        {
            if (storedToken is not null)
                await refreshTokenRepository.RevokeAsync(storedToken.Id, ct);
            return Error.Unauthorized("Auth.InvalidRefreshToken", "Invalid or expired refresh token.");
        }

        var user = await userRepository.GetByIdAsync(storedToken.UserId, ct);
        if (user is null)
        {
            await refreshTokenRepository.RevokeAsync(storedToken.Id, ct);
            return Error.Unauthorized("Auth.UserNotFound", "User not found.");
        }

        await refreshTokenRepository.RevokeAsync(storedToken.Id, ct);

        var newToken = jwtProvider.Generate(user);
        var (newRefreshTokenValue, newTokenHash) = refreshTokenService.CreateRefreshToken();
        var newRefreshTokenEntity = RefreshToken.Create(user.Id, newTokenHash, DateTime.UtcNow.AddDays(30));
        await refreshTokenRepository.AddAsync(newRefreshTokenEntity, ct);

        return new LoginResponse(newToken, newRefreshTokenValue);
    }
}
