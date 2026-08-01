using System.Security.Cryptography;
using System.Text;

namespace Budgie.Modules.Users.Infrastructure.Authentication;

internal sealed class RefreshTokenService : IRefreshTokenService
{
    public string GenerateToken()
    {
        return Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
    }

    public string HashToken(string token)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        return Convert.ToBase64String(bytes);
    }

    public (string RefreshToken, string TokenHash) CreateRefreshToken()
    {
        var token = GenerateToken();
        var hash = HashToken(token);
        return (token, hash);
    }

    public bool IsRefreshTokenExpired(DateTime expiresAt)
    {
        return DateTime.UtcNow > expiresAt;
    }
}
