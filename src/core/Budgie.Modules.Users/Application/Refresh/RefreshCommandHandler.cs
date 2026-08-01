using Budgie.Modules.Users.Application.Login;
using Budgie.Modules.Users.Domain;
using Budgie.Modules.Users.Infrastructure.Authentication;
using Budgie.Modules.Users.Infrastructure.Database;
using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Budgie.Modules.Users.Application.Refresh;

internal sealed class RefreshCommandHandler(
    UsersDbContext dbContext,
    IJwtProvider jwtProvider,
    IRefreshTokenService refreshTokenService)
    : IRequestHandler<RefreshCommand, ErrorOr<LoginResponse>>
{
    public async Task<ErrorOr<LoginResponse>> Handle(RefreshCommand request, CancellationToken cancellationToken)
    {
        var tokenHash = refreshTokenService.HashToken(request.RefreshToken);
        var storedToken = await dbContext.RefreshTokens
            .FirstOrDefaultAsync(rt => rt.TokenHash == tokenHash, cancellationToken);

        if (storedToken is null || storedToken.IsRevoked || storedToken.IsExpired)
        {
            if (storedToken is not null)
            {
                storedToken.Revoke();
                await dbContext.SaveChangesAsync(cancellationToken);
            }
            return Error.Unauthorized("Auth.InvalidRefreshToken", "Invalid or expired refresh token.");
        }

        var user = await dbContext.Users.FirstOrDefaultAsync(u => u.Id == storedToken.UserId, cancellationToken);
        if (user is null)
        {
            storedToken.Revoke();
            await dbContext.SaveChangesAsync(cancellationToken);
            return Error.Unauthorized("Auth.UserNotFound", "User not found.");
        }

        // Rotate: revoke old, create new
        storedToken.Revoke();

        var newToken = jwtProvider.Generate(user);
        var (newRefreshToken, newTokenHash) = refreshTokenService.CreateRefreshToken();
        var newRefreshTokenEntity = RefreshToken.Create(
            user.Id,
            newTokenHash,
            DateTime.UtcNow.AddDays(30));

        dbContext.RefreshTokens.Add(newRefreshTokenEntity);
        await dbContext.SaveChangesAsync(cancellationToken);

        return new LoginResponse(newToken, newRefreshToken);
    }
}
