using Budgie.Modules.Users.Application;
using Budgie.Modules.Users.Application.Admin.CreateUser;
using Budgie.Modules.Users.Application.Invites;
using Budgie.Modules.Users.Domain;
using Budgie.Modules.Users.Domain.Events;
using Budgie.Shared.Abstractions;
using Budgie.Shared.Enums;
using FluentAssertions;
using Mediator;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace Budgie.Modules.Users.UnitTests.Application;

public class CreateInviteCommandHandlerTests
{
    private readonly IUserInviteRepository _inviteRepository = Substitute.For<IUserInviteRepository>();
    private readonly IUserRepository _userRepository = Substitute.For<IUserRepository>();
    private readonly ICurrentUserProvider _currentUserProvider = Substitute.For<ICurrentUserProvider>();
    private readonly CreateInviteCommandHandler _handler;

    public CreateInviteCommandHandlerTests()
    {
        _handler = new CreateInviteCommandHandler(_inviteRepository, _userRepository, _currentUserProvider);
    }

    [Fact]
    public async Task Handle_AdminUser_CreatesInviteWithToken()
    {
        var adminId = Guid.NewGuid();
        var admin = User.Create("admin@example.com", "hash", "Admin", UserRole.Admin);
        _currentUserProvider.GetUserId().Returns(adminId);
        _userRepository.GetByIdAsync(adminId, Arg.Any<CancellationToken>()).Returns(admin);

        var result = await _handler.Handle(new CreateInviteCommand("maria@example.com", 48), CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.Token.Should().StartWith("inv_");
        result.Value.Email.Should().Be("maria@example.com");
        result.Value.ExpiresAt.Should().BeAfter(DateTime.UtcNow);
        await _inviteRepository.Received(1).AddAsync(Arg.Is<UserInvite>(i =>
            !string.IsNullOrWhiteSpace(i.TokenHash) && i.CreatedByUserId == adminId),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_NonAdminUser_ReturnsForbidden()
    {
        var userId = Guid.NewGuid();
        var user = User.Create("user@example.com", "hash", "User", UserRole.User);
        _currentUserProvider.GetUserId().Returns(userId);
        _userRepository.GetByIdAsync(userId, Arg.Any<CancellationToken>()).Returns(user);

        var result = await _handler.Handle(new CreateInviteCommand("maria@example.com", 48), CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Type.Should().Be(ErrorOr.ErrorType.Forbidden);
        await _inviteRepository.DidNotReceive().AddAsync(Arg.Any<UserInvite>(), Arg.Any<CancellationToken>());
    }
}

public class AdminCreateUserCommandHandlerTests
{
    private readonly IUserRepository _userRepository = Substitute.For<IUserRepository>();
    private readonly ICurrentUserProvider _currentUserProvider = Substitute.For<ICurrentUserProvider>();
    private readonly IMediator _mediator = Substitute.For<IMediator>();
    private readonly AdminCreateUserCommandHandler _handler;

    public AdminCreateUserCommandHandlerTests()
    {
        _handler = new AdminCreateUserCommandHandler(
            _userRepository, _currentUserProvider, _mediator, NullLogger<AdminCreateUserCommandHandler>.Instance);
    }

    [Fact]
    public async Task Handle_AdminProvisionsUser_AndPublishesEvent()
    {
        var adminId = Guid.NewGuid();
        var admin = User.Create("admin@example.com", "hash", "Admin", UserRole.Admin);
        _currentUserProvider.GetUserId().Returns(adminId);
        _userRepository.GetByIdAsync(adminId, Arg.Any<CancellationToken>()).Returns(admin);

        var result = await _handler.Handle(
            new AdminCreateUserCommand("carlos@example.com", "Carlos", "TempPassword123!"), CancellationToken.None);

        result.IsError.Should().BeFalse();
        await _userRepository.Received(1).AddAsync(Arg.Is<User>(u =>
            u.Email.Value == "carlos@example.com" && u.Role == UserRole.User),
            Arg.Any<CancellationToken>());
        await _mediator.Received(1).Publish(
            Arg.Is<UserRegisteredEvent>(e => e.Email == "carlos@example.com"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_NonAdmin_ReturnsForbidden()
    {
        var userId = Guid.NewGuid();
        var user = User.Create("user@example.com", "hash", "User", UserRole.User);
        _currentUserProvider.GetUserId().Returns(userId);
        _userRepository.GetByIdAsync(userId, Arg.Any<CancellationToken>()).Returns(user);

        var result = await _handler.Handle(
            new AdminCreateUserCommand("carlos@example.com", "Carlos", "TempPassword123!"), CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Type.Should().Be(ErrorOr.ErrorType.Forbidden);
        await _userRepository.DidNotReceive().AddAsync(Arg.Any<User>(), Arg.Any<CancellationToken>());
    }
}
