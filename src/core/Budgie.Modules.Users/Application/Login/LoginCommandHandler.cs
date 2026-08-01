using Budgie.Modules.Users.Domain;
using Budgie.Modules.Users.Infrastructure.Authentication;
using Budgie.Modules.Users.Infrastructure.Database;
using Budgie.Modules.Users.Infrastructure.Logging;
using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;
using BC = BCrypt.Net.BCrypt;

namespace Budgie.Modules.Users.Application.Login;

internal sealed class LoginCommandHandler(
    UsersDbContext dbContext,
    IJwtProvider jwtProvider,
    IRefreshTokenService refreshTokenService,
    ILogger<LoginCommandHandler> logger)
    : IRequestHandler<LoginCommand, ErrorOr<LoginResponse>>
{
    public async Task<ErrorOr<LoginResponse>> Handle(LoginCommand request, CancellationToken cancellationToken)
    {
        var user = await dbContext.Users.FirstOrDefaultAsync(u => u.Email == request.Email, cancellationToken);

        if (user is null || !BC.Verify(request.Password, user.PasswordHash))
        {
            LogMessages.FailedLoginAttempt(logger, request.Email);
            return Error.Unauthorized("Auth.InvalidCredentials", "Invalid email or password.");
        }

        var token = jwtProvider.Generate(user);

        var (refreshToken, tokenHash) = refreshTokenService.CreateRefreshToken();
        var refreshTokenEntity = RefreshToken.Create(
            user.Id,
            tokenHash,
            DateTime.UtcNow.AddDays(30));

        dbContext.RefreshTokens.Add(refreshTokenEntity);
        await dbContext.SaveChangesAsync(cancellationToken);

        LogMessages.UserLoggedIn(logger, request.Email);

        return new LoginResponse(token, refreshToken);
    }
}
