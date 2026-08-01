using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Budgie.Modules.Users.Domain;
using Budgie.Modules.Users.Infrastructure.Authentication;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace Budgie.Modules.Users.UnitTests.Infrastructure;

public class JwtProviderTests
{
    private readonly IConfiguration _configuration;

    public JwtProviderTests()
    {
        _configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Secret"] = "ThisIsAVeryLongSecretKeyForTesting1234567890!",
                ["Jwt:Issuer"] = "Budgie",
                ["Jwt:Audience"] = "Budgie"
            })
            .Build();
    }

    [Fact]
    public void Generate_ReturnsValidJwtToken()
    {
        var user = User.Create("test@example.com", BCrypt.Net.BCrypt.HashPassword("password"), "Test User");
        var provider = new JwtProvider(_configuration);

        var token = provider.Generate(user);

        token.Should().NotBeNullOrEmpty();
        var handler = new JwtSecurityTokenHandler();
        var canRead = handler.CanReadToken(token);
        canRead.Should().BeTrue();
    }

    [Fact]
    public void Generate_TokenContainsCorrectClaims()
    {
        var user = User.Create("test@example.com", BCrypt.Net.BCrypt.HashPassword("password"), "Test User");
        var provider = new JwtProvider(_configuration);

        var token = provider.Generate(user);

        var handler = new JwtSecurityTokenHandler();
        var jwtToken = handler.ReadJwtToken(token);
        jwtToken.Subject.Should().Be(user.Id.ToString());
        jwtToken.Claims.Should().Contain(c => c.Type == JwtRegisteredClaimNames.Email && c.Value == "test@example.com");
        jwtToken.Claims.Should().Contain(c => c.Type == "name" && c.Value == "Test User");
    }

    [Fact]
    public void Generate_TokenHasCorrectIssuerAndAudience()
    {
        var user = User.Create("test@example.com", BCrypt.Net.BCrypt.HashPassword("password"), "Test User");
        var provider = new JwtProvider(_configuration);

        var token = provider.Generate(user);

        var handler = new JwtSecurityTokenHandler();
        var jwtToken = handler.ReadJwtToken(token);
        jwtToken.Issuer.Should().Be("Budgie");
        jwtToken.Audiences.Should().Contain("Budgie");
    }

    [Fact]
    public void Generate_TokenHasCorrectExpiration()
    {
        var user = User.Create("test@example.com", BCrypt.Net.BCrypt.HashPassword("password"), "Test User");
        var provider = new JwtProvider(_configuration);

        var token = provider.Generate(user);

        var handler = new JwtSecurityTokenHandler();
        var jwtToken = handler.ReadJwtToken(token);
        var expectedExpiry = DateTime.UtcNow.AddHours(24);
        jwtToken.ValidTo.Should().BeCloseTo(expectedExpiry, TimeSpan.FromSeconds(10));
    }
}
