using Budgie.Modules.Users.Application.Profile;
using Budgie.Modules.Users.Application.Profile.GetProfile;
using Budgie.Modules.Users.Domain;
using Budgie.Modules.Users.Infrastructure.Database;
using Budgie.Shared.Abstractions;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Xunit;

namespace Budgie.Modules.Users.UnitTests.Application.Profile;

public class GetProfileQueryHandlerTests
{
    private readonly UsersDbContext _dbContext;
    private readonly ICurrentUserProvider _currentUserProvider;

    public GetProfileQueryHandlerTests()
    {
        _currentUserProvider = Substitute.For<ICurrentUserProvider>();

        var options = new DbContextOptionsBuilder<UsersDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _dbContext = new UsersDbContext(options);
    }

    [Fact]
    public async Task Handle_ExistingUser_ReturnsProfile()
    {
        var user = User.Create("test@example.com", BCrypt.Net.BCrypt.HashPassword("password"), "Test User");
        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync();

        _currentUserProvider.GetUserId().Returns(user.Id);

        var handler = new GetProfileQueryHandler(_dbContext, _currentUserProvider);
        var query = new GetProfileQuery();

        var result = await handler.Handle(query, CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.Id.Should().Be(user.Id);
        result.Value.Email.Should().Be("test@example.com");
        result.Value.Name.Should().Be("Test User");
    }

    [Fact]
    public async Task Handle_NonExistentUser_ReturnsNotFound()
    {
        _currentUserProvider.GetUserId().Returns(Guid.NewGuid());

        var handler = new GetProfileQueryHandler(_dbContext, _currentUserProvider);
        var query = new GetProfileQuery();

        var result = await handler.Handle(query, CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("User.NotFound");
    }
}
