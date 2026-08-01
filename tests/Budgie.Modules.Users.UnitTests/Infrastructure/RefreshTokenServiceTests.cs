using Budgie.Modules.Users.Domain;
using Budgie.Modules.Users.Infrastructure.Authentication;
using Budgie.Modules.Users.Infrastructure.Database;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Budgie.Modules.Users.UnitTests.Infrastructure;

public class RefreshTokenServiceTests
{
    private readonly UsersDbContext _dbContext;
    private readonly RefreshTokenService _service;

    public RefreshTokenServiceTests()
    {
        var options = new DbContextOptionsBuilder<UsersDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _dbContext = new UsersDbContext(options);
        _service = new RefreshTokenService();
    }

    [Fact]
    public void GenerateToken_ReturnsNonEmptyString()
    {
        var token = _service.GenerateToken();

        token.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void HashToken_ReturnsConsistentHash()
    {
        var token = "test-refresh-token-value";

        var hash1 = _service.HashToken(token);
        var hash2 = _service.HashToken(token);

        hash1.Should().Be(hash2);
    }

    [Fact]
    public void HashToken_DifferentTokens_DifferentHashes()
    {
        var hash1 = _service.HashToken("token-a");
        var hash2 = _service.HashToken("token-b");

        hash1.Should().NotBe(hash2);
    }

    [Fact]
    public void CreateRefreshToken_ReturnsTokenAndHash()
    {
        var (token, hash) = _service.CreateRefreshToken();

        token.Should().NotBeNullOrEmpty();
        hash.Should().NotBeNullOrEmpty();
        hash.Should().Be(_service.HashToken(token));
    }

    [Fact]
    public void IsRefreshTokenExpired_FutureDate_ReturnsFalse()
    {
        var future = DateTime.UtcNow.AddDays(30);

        var expired = _service.IsRefreshTokenExpired(future);

        expired.Should().BeFalse();
    }

    [Fact]
    public void IsRefreshTokenExpired_PastDate_ReturnsTrue()
    {
        var past = DateTime.UtcNow.AddDays(-1);

        var expired = _service.IsRefreshTokenExpired(past);

        expired.Should().BeTrue();
    }
}
