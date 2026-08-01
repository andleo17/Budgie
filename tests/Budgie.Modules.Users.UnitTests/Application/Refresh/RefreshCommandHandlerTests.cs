using Budgie.Modules.Users.Application.Login;
using Budgie.Modules.Users.Application.Refresh;
using Budgie.Modules.Users.Domain;
using Budgie.Modules.Users.Infrastructure.Authentication;
using Budgie.Modules.Users.Infrastructure.Database;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Xunit;

namespace Budgie.Modules.Users.UnitTests.Application.Refresh;

public class RefreshCommandHandlerTests
{
    private readonly UsersDbContext _dbContext;
    private readonly IJwtProvider _jwtProvider;
    private readonly IRefreshTokenService _refreshTokenService;

    public RefreshCommandHandlerTests()
    {
        _jwtProvider = Substitute.For<IJwtProvider>();
        _jwtProvider.Generate(Arg.Any<User>()).Returns("new-jwt-token");

        _refreshTokenService = Substitute.For<IRefreshTokenService>();
        _refreshTokenService.HashToken(Arg.Any<string>()).Returns("known-token-hash");
        _refreshTokenService.CreateRefreshToken().Returns(("new-refresh-token", "new-token-hash"));

        var options = new DbContextOptionsBuilder<UsersDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _dbContext = new UsersDbContext(options);
    }

    [Fact]
    public async Task Handle_ValidRefreshToken_ReturnsNewTokens()
    {
        var user = User.Create("test@example.com", BCrypt.Net.BCrypt.HashPassword("password"), "Test User");
        _dbContext.Users.Add(user);

        var storedToken = RefreshToken.Create(user.Id, "known-token-hash", DateTime.UtcNow.AddDays(30));
        _dbContext.RefreshTokens.Add(storedToken);
        await _dbContext.SaveChangesAsync();

        var handler = new RefreshCommandHandler(_dbContext, _jwtProvider, _refreshTokenService);
        var command = new RefreshCommand("some-raw-token");

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.Token.Should().Be("new-jwt-token");
        result.Value.RefreshToken.Should().Be("new-refresh-token");
    }

    [Fact]
    public async Task Handle_ValidRefreshToken_RevokesOldAndCreatesNew()
    {
        var user = User.Create("test@example.com", BCrypt.Net.BCrypt.HashPassword("password"), "Test User");
        _dbContext.Users.Add(user);

        var storedToken = RefreshToken.Create(user.Id, "known-token-hash", DateTime.UtcNow.AddDays(30));
        _dbContext.RefreshTokens.Add(storedToken);
        await _dbContext.SaveChangesAsync();

        var handler = new RefreshCommandHandler(_dbContext, _jwtProvider, _refreshTokenService);
        var command = new RefreshCommand("some-raw-token");

        await handler.Handle(command, CancellationToken.None);

        var oldToken = await _dbContext.RefreshTokens.FirstAsync(t => t.Id == storedToken.Id);
        oldToken.IsRevoked.Should().BeTrue();

        var newToken = await _dbContext.RefreshTokens.FirstOrDefaultAsync(t => t.TokenHash == "new-token-hash");
        newToken.Should().NotBeNull();
    }

    [Fact]
    public async Task Handle_InvalidToken_ReturnsUnauthorized()
    {
        _refreshTokenService.HashToken(Arg.Any<string>()).Returns("non-existent-hash");

        var handler = new RefreshCommandHandler(_dbContext, _jwtProvider, _refreshTokenService);
        var command = new RefreshCommand("invalid-token");

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("Auth.InvalidRefreshToken");
    }

    [Fact]
    public async Task Handle_RevokedToken_ReturnsUnauthorized()
    {
        var user = User.Create("test@example.com", BCrypt.Net.BCrypt.HashPassword("password"), "Test User");
        _dbContext.Users.Add(user);

        var storedToken = RefreshToken.Create(user.Id, "known-token-hash", DateTime.UtcNow.AddDays(30));
        storedToken.Revoke();
        _dbContext.RefreshTokens.Add(storedToken);
        await _dbContext.SaveChangesAsync();

        var handler = new RefreshCommandHandler(_dbContext, _jwtProvider, _refreshTokenService);
        var command = new RefreshCommand("revoked-token");

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("Auth.InvalidRefreshToken");
    }

    [Fact]
    public async Task Handle_ExpiredToken_ReturnsUnauthorized()
    {
        var user = User.Create("test@example.com", BCrypt.Net.BCrypt.HashPassword("password"), "Test User");
        _dbContext.Users.Add(user);

        var storedToken = RefreshToken.Create(user.Id, "known-token-hash", DateTime.UtcNow.AddDays(30));
        _dbContext.RefreshTokens.Add(storedToken);
        await _dbContext.SaveChangesAsync();

        _dbContext.Entry(storedToken).Property("ExpiresAt").CurrentValue =
            DateTime.UtcNow.AddDays(-1);
        await _dbContext.SaveChangesAsync();

        var handler = new RefreshCommandHandler(_dbContext, _jwtProvider, _refreshTokenService);
        var command = new RefreshCommand("expired-token");

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("Auth.InvalidRefreshToken");
    }
}
