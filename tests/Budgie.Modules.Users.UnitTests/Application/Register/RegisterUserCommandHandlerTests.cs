using Budgie.Modules.Users.Application.Register;
using Budgie.Modules.Users.Domain;
using Budgie.Modules.Users.Infrastructure.Database;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Budgie.Modules.Users.UnitTests.Application.Register;

public class RegisterUserCommandHandlerTests
{
    private readonly UsersDbContext _dbContext;

    public RegisterUserCommandHandlerTests()
    {
        var options = new DbContextOptionsBuilder<UsersDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _dbContext = new UsersDbContext(options);
    }

    [Fact]
    public async Task Handle_ValidCredentials_CreatesUserAndReturnsId()
    {
        var handler = new RegisterUserCommandHandler(_dbContext, NullLogger<RegisterUserCommandHandler>.Instance);
        var command = new RegisterUserCommand("test@example.com", "SecurePass123!", "Test User");

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsError.Should().BeFalse();
        var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.Id == result.Value);
        user.Should().NotBeNull();
        user!.Email.Value.Should().Be("test@example.com");
        user.Name.Value.Should().Be("Test User");
    }

    [Fact]
    public async Task Handle_ValidCredentials_CreatesDefaultSettings()
    {
        var handler = new RegisterUserCommandHandler(_dbContext, NullLogger<RegisterUserCommandHandler>.Instance);
        var command = new RegisterUserCommand("test@example.com", "SecurePass123!", "Test User");

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsError.Should().BeFalse();
        var settings = await _dbContext.UserSettings.FirstOrDefaultAsync(s => s.UserId == result.Value);
        settings.Should().NotBeNull();
        settings!.IsConfigured.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_ExistingEmail_ReturnsConflictError()
    {
        var existingUser = User.Create("existing@example.com", BCrypt.Net.BCrypt.HashPassword("password"), "Existing");
        _dbContext.Users.Add(existingUser);
        await _dbContext.SaveChangesAsync();

        var handler = new RegisterUserCommandHandler(_dbContext, NullLogger<RegisterUserCommandHandler>.Instance);
        var command = new RegisterUserCommand("existing@example.com", "AnotherPass123!", "New User");

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("User.EmailAlreadyExists");
    }

    [Fact]
    public async Task Handle_InvalidEmail_ThrowsArgumentException()
    {
        var handler = new RegisterUserCommandHandler(_dbContext, NullLogger<RegisterUserCommandHandler>.Instance);
        var command = new RegisterUserCommand("not-an-email", "password123", "Test User");

        Func<Task> act = () => handler.Handle(command, CancellationToken.None).AsTask();

        await act.Should().ThrowExactlyAsync<ArgumentException>();
    }

    [Fact]
    public async Task Handle_EmptyName_ThrowsArgumentException()
    {
        var handler = new RegisterUserCommandHandler(_dbContext, NullLogger<RegisterUserCommandHandler>.Instance);
        var command = new RegisterUserCommand("test@example.com", "password123", "");

        Func<Task> act = () => handler.Handle(command, CancellationToken.None).AsTask();

        await act.Should().ThrowExactlyAsync<ArgumentException>();
    }

    [Fact]
    public async Task Handle_NullName_ThrowsArgumentException()
    {
        var handler = new RegisterUserCommandHandler(_dbContext, NullLogger<RegisterUserCommandHandler>.Instance);
        var command = new RegisterUserCommand("test@example.com", "password123", null!);

        Func<Task> act = () => handler.Handle(command, CancellationToken.None).AsTask();

        await act.Should().ThrowExactlyAsync<ArgumentException>();
    }
}
