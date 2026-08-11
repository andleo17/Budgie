using Budgie.Modules.Users.Application;
using Budgie.Modules.Users.Application.Login;
using Budgie.Modules.Users.Domain;
using Budgie.Modules.Users.Infrastructure.Authentication;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace Budgie.Modules.Users.UnitTests.Application.Login;

public class LoginCommandHandlerTests
{
    private readonly IUserRepository _userRepository = Substitute.For<IUserRepository>();
    private readonly IRefreshTokenRepository _refreshTokenRepository = Substitute.For<IRefreshTokenRepository>();
    private readonly IJwtProvider _jwtProvider = Substitute.For<IJwtProvider>();
    private readonly IRefreshTokenService _refreshTokenService = Substitute.For<IRefreshTokenService>();
    private readonly LoginCommandHandler _handler;

    public LoginCommandHandlerTests()
    {
        _handler = new LoginCommandHandler(_userRepository, _refreshTokenRepository,
            _jwtProvider, _refreshTokenService, NullLogger<LoginCommandHandler>.Instance);
    }

    [Fact]
    public async Task Handle_ValidCredentials_ReturnsTokenAndRefreshToken()
    {
        var user = User.Create("test@example.com", BCrypt.Net.BCrypt.HashPassword("Password123!"), "Test User");
        _userRepository.GetByEmailAsync("test@example.com", Arg.Any<CancellationToken>()).Returns(user);
        _jwtProvider.Generate(user).Returns("jwt-token");
        _refreshTokenService.CreateRefreshToken().Returns(("refresh-token", "token-hash"));

        var command = new LoginCommand("test@example.com", "Password123!");

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.Token.Should().Be("jwt-token");
        result.Value.RefreshToken.Should().Be("refresh-token");
        await _refreshTokenRepository.Received(1).AddAsync(
            Arg.Is<RefreshToken>(rt => rt.UserId == user.Id && rt.TokenHash == "token-hash"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_InvalidPassword_ReturnsUnauthorized()
    {
        var user = User.Create("test@example.com", BCrypt.Net.BCrypt.HashPassword("Correct123!"), "Test User");
        _userRepository.GetByEmailAsync("test@example.com", Arg.Any<CancellationToken>()).Returns(user);

        var command = new LoginCommand("test@example.com", "WrongPassword!");

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("Auth.InvalidCredentials");
    }

    [Fact]
    public async Task Handle_UnknownEmail_ReturnsUnauthorized()
    {
        _userRepository.GetByEmailAsync("missing@example.com", Arg.Any<CancellationToken>()).Returns((User?)null);

        var command = new LoginCommand("missing@example.com", "Password123!");

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("Auth.InvalidCredentials");
    }
}
