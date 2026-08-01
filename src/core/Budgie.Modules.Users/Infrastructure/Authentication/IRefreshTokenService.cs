namespace Budgie.Modules.Users.Infrastructure.Authentication;

public interface IRefreshTokenService
{
    string GenerateToken();
    string HashToken(string token);
    (string RefreshToken, string TokenHash) CreateRefreshToken();
    bool IsRefreshTokenExpired(DateTime expiresAt);
}
