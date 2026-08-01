using Budgie.Modules.Users.Application.Login;
using Budgie.Modules.Users.Domain;
using Budgie.Modules.Users.Infrastructure.Authentication;
using Budgie.Modules.Users.Infrastructure.Database;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace Budgie.Modules.Users.UnitTests.Application.Login;

public class LoginCommandHandlerTests
{
    private readonly UsersDbContext _dbContext;
    private readonly IJwtProvider _jwtProvider;
    private readonly IRefreshTokenService _refreshTokenService;

    public LoginCommandHandlerTests()
    {
        _jwtProvider = Substitute.For<IJwtProvider>();
        _jwtProvider.Generate(Arg.Any<User>()).Returns("test-jwt-token");

        _refreshTokenService = Substitute.For<IRefreshTokenService>();
        _refreshTokenService.CreateRefreshToken().Returns(("test-refresh-token", "test-token-hash"));

        var options = new DbContextOptionsBuilder<UsersDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _dbContext = new UsersDbContext(options);
    }

    [Fact]
    public async Task Handle_ValidCredentials_ReturnsTokens()
    {
        var password = "SecurePass123!";
        var user = User.Create("test@example.com", BCrypt.Net.BCrypt.HashPassword(password), "Test User");
        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync();

        var handler = new LoginCommandHandler(_dbContext, _jwtProvider, _refreshTokenService, NullLogger<LoginCommandHandler>.Instance);
        var command = new LoginCommand("test@example.com", password);

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.Token.Should().Be("test-jwt-token");
        result.Value.RefreshToken.Should().Be("test-refresh-token");
    }

    [Fact]
    public async Task Handle_ValidCredentials_StoresRefreshToken()
    {
        var password = "SecurePass123!";
        var user = User.Create("test@example.com", BCrypt.Net.BCrypt.HashPassword(password), "Test User");
        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync();

        var handler = new LoginCommandHandler(_dbContext, _jwtProvider, _refreshTokenService, NullLogger<LoginCommandHandler>.Instance);
        var command = new LoginCommand("test@example.com", password);

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsError.Should().BeFalse();
        var storedToken = await _dbContext.RefreshTokens.FirstOrDefaultAsync(t => t.UserId == user.Id);
        storedToken.Should().NotBeNull();
        storedToken!.TokenHash.Should().Be("test-token-hash");
    }

    [Fact]
    public async Task Handle_WrongPassword_ReturnsUnauthorized()
    {
        var user = User.Create("test@example.com", BCrypt.Net.BCrypt.HashPassword("correct-password"), "Test User");
        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync();

        var handler = new LoginCommandHandler(_dbContext, _jwtProvider, _refreshTokenService, NullLogger<LoginCommandHandler>.Instance);
        var command = new LoginCommand("test@example.com", "wrong-password");

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("Auth.InvalidCredentials");
    }

    [Fact]
    public async Task Handle_NonExistentEmail_ReturnsUnauthorized()
    {
        var handler = new LoginCommandHandler(_dbContext, _jwtProvider, _refreshTokenService, NullLogger<LoginCommandHandler>.Instance);
        var command = new LoginCommand("nonexistent@example.com", "any-password");

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("Auth.InvalidCredentials");
    }
}
