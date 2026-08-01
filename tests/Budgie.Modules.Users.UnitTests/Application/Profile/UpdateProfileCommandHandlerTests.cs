using Budgie.Modules.Users.Application.Profile.UpdateProfile;
using Budgie.Modules.Users.Domain;
using Budgie.Modules.Users.Infrastructure.Database;
using Budgie.Shared.Abstractions;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Xunit;

namespace Budgie.Modules.Users.UnitTests.Application.Profile;

public class UpdateProfileCommandHandlerTests
{
    private readonly UsersDbContext _dbContext;
    private readonly ICurrentUserProvider _currentUserProvider;

    public UpdateProfileCommandHandlerTests()
    {
        _currentUserProvider = Substitute.For<ICurrentUserProvider>();

        var options = new DbContextOptionsBuilder<UsersDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _dbContext = new UsersDbContext(options);
    }

    [Fact]
    public async Task Handle_ExistingUser_UpdatesName()
    {
        var user = User.Create("test@example.com", BCrypt.Net.BCrypt.HashPassword("password"), "Original Name");
        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync();

        _currentUserProvider.GetUserId().Returns(user.Id);

        var handler = new UpdateProfileCommandHandler(_dbContext, _currentUserProvider);
        var command = new UpdateProfileCommand("Updated Name");

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsError.Should().BeFalse();
        var updatedUser = await _dbContext.Users.FirstAsync(u => u.Id == user.Id);
        updatedUser.Name.Value.Should().Be("Updated Name");
    }

    [Fact]
    public async Task Handle_NonExistentUser_ReturnsNotFound()
    {
        _currentUserProvider.GetUserId().Returns(Guid.NewGuid());

        var handler = new UpdateProfileCommandHandler(_dbContext, _currentUserProvider);
        var command = new UpdateProfileCommand("New Name");

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("User.NotFound");
    }
}
