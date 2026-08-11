using Budgie.Modules.Users.Application;
using Budgie.Modules.Users.Application.Login;
using Budgie.Modules.Users.Application.Refresh;
using Budgie.Modules.Users.Domain;
using Budgie.Modules.Users.Infrastructure.Authentication;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace Budgie.Modules.Users.UnitTests.Application.Refresh;

public class RefreshCommandHandlerTests
{
    private readonly IUserRepository _userRepository = Substitute.For<IUserRepository>();
    private readonly IRefreshTokenRepository _refreshTokenRepository = Substitute.For<IRefreshTokenRepository>();
    private readonly IJwtProvider _jwtProvider = Substitute.For<IJwtProvider>();
    private readonly IRefreshTokenService _refreshTokenService = Substitute.For<IRefreshTokenService>();
    private readonly RefreshCommandHandler _handler;

    public RefreshCommandHandlerTests()
    {
        _handler = new RefreshCommandHandler(_userRepository, _refreshTokenRepository,
            _jwtProvider, _refreshTokenService);
    }

    [Fact]
    public async Task Handle_ValidRefreshToken_RotatesAndReturnsNewTokens()
    {
        var userId = Guid.NewGuid();
        var stored = RefreshToken.Create(userId, "stored-hash", DateTime.UtcNow.AddDays(30));
        var user = User.Create("test@example.com", "hash", "Test User");
        user.Id = userId;

        _refreshTokenService.HashToken("refresh-token").Returns("stored-hash");
        _refreshTokenRepository.GetByHashAsync("stored-hash", Arg.Any<CancellationToken>()).Returns(stored);
        _userRepository.GetByIdAsync(userId, Arg.Any<CancellationToken>()).Returns(user);
        _jwtProvider.Generate(user).Returns("new-jwt");
        _refreshTokenService.CreateRefreshToken().Returns(("new-refresh", "new-hash"));

        var result = await _handler.Handle(new RefreshCommand("refresh-token"), CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.Token.Should().Be("new-jwt");
        result.Value.RefreshToken.Should().Be("new-refresh");
        await _refreshTokenRepository.Received(1).RevokeAsync(stored.Id, Arg.Any<CancellationToken>());
        await _refreshTokenRepository.Received(1).AddAsync(
            Arg.Is<RefreshToken>(rt => rt.TokenHash == "new-hash" && rt.UserId == userId),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_InvalidRefreshToken_ReturnsUnauthorized()
    {
        _refreshTokenService.HashToken("bad-token").Returns("bad-hash");
        _refreshTokenRepository.GetByHashAsync("bad-hash", Arg.Any<CancellationToken>()).Returns((RefreshToken?)null);

        var result = await _handler.Handle(new RefreshCommand("bad-token"), CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("Auth.InvalidRefreshToken");
    }

    [Fact]
    public async Task Handle_RevokedRefreshToken_ReturnsUnauthorized()
    {
        var stored = RefreshToken.Create(Guid.NewGuid(), "hash", DateTime.UtcNow.AddDays(30));
        stored.Revoke();
        _refreshTokenService.HashToken("revoked").Returns("hash");
        _refreshTokenRepository.GetByHashAsync("hash", Arg.Any<CancellationToken>()).Returns(stored);

        var result = await _handler.Handle(new RefreshCommand("revoked"), CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("Auth.InvalidRefreshToken");
    }
}
