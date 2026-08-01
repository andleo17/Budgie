using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Budgie.Modules.Users.Domain;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace Budgie.Modules.Users.Infrastructure.Authentication;

public interface IJwtProvider
{
    string Generate(User user);
}

public class JwtProvider(IConfiguration configuration) : IJwtProvider
{
    public string Generate(User user)
    {
        var claims = new Claim[]
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email),
            new("name", user.Name)
        };

        var secretKey = configuration["Jwt:Secret"] ?? "a-very-long-and-secure-secret-key-123456789";
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: configuration["Jwt:Issuer"] ?? "Budgie",
            audience: configuration["Jwt:Audience"] ?? "Budgie",
            claims: claims,
            expires: DateTime.UtcNow.AddHours(24),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
