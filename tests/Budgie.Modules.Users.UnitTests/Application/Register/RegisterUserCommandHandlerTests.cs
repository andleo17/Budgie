using Budgie.Modules.Users.Application;
using Budgie.Modules.Users.Application.Register;
using Budgie.Modules.Users.Domain;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace Budgie.Modules.Users.UnitTests.Application.Register;

public class RegisterUserCommandHandlerTests
{
    private readonly IUserRepository _userRepository = Substitute.For<IUserRepository>();
    private readonly IUserSettingsRepository _settingsRepository = Substitute.For<IUserSettingsRepository>();
    private readonly IRegistrationPolicy _registrationPolicy = Substitute.For<IRegistrationPolicy>();
    private readonly RegisterUserCommandHandler _handler;

    public RegisterUserCommandHandlerTests()
    {
        _registrationPolicy.CanRegisterAsync(Arg.Any<CancellationToken>()).Returns(true);

        _handler = new RegisterUserCommandHandler(
            _userRepository, _settingsRepository, _registrationPolicy, NullLogger<RegisterUserCommandHandler>.Instance);
    }

    [Fact]
    public async Task Handle_ValidCredentials_CreatesUserAndReturnsId()
    {
        var command = new RegisterUserCommand("test@example.com", "SecurePass123!", "Test User");

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.Should().NotBeEmpty();
        await _userRepository.Received(1).AddAsync(Arg.Is<User>(u => u.Email.Value == "test@example.com"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ValidCredentials_CreatesDefaultSettings()
    {
        var command = new RegisterUserCommand("test@example.com", "SecurePass123!", "Test User");

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsError.Should().BeFalse();
        await _settingsRepository.Received(1).AddAsync(
            Arg.Is<UserSettings>(s => s.UserId == result.Value && !s.IsConfigured),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ExistingEmail_ReturnsConflictError()
    {
        _userRepository.EmailExistsAsync("existing@example.com", Arg.Any<CancellationToken>())
            .Returns(true);

        var command = new RegisterUserCommand("existing@example.com", "AnotherPass123!", "New User");

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("User.EmailAlreadyExists");
        await _userRepository.DidNotReceive().AddAsync(Arg.Any<User>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_RegistrationClosed_ReturnsForbiddenError()
    {
        _registrationPolicy.CanRegisterAsync(Arg.Any<CancellationToken>()).Returns(false);

        var command = new RegisterUserCommand("new@example.com", "SecurePass123!", "New User");

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("User.RegistrationClosed");
        await _userRepository.DidNotReceive().AddAsync(Arg.Any<User>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_InvalidEmail_ThrowsArgumentException()
    {
        var command = new RegisterUserCommand("not-an-email", "password123", "Test User");

        Func<Task> act = () => _handler.Handle(command, CancellationToken.None).AsTask();

        await act.Should().ThrowExactlyAsync<ArgumentException>();
    }

    [Fact]
    public async Task Handle_EmptyName_ThrowsArgumentException()
    {
        var command = new RegisterUserCommand("test@example.com", "password123", "");

        Func<Task> act = () => _handler.Handle(command, CancellationToken.None).AsTask();

        await act.Should().ThrowExactlyAsync<ArgumentException>();
    }
}
