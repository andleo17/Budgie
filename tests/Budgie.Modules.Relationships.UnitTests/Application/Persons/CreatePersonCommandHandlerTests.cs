using Budgie.Modules.Relationships.Application.Persons.CreatePerson;
using Budgie.Modules.Relationships.Infrastructure.Database;
using Budgie.Shared.Abstractions;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace Budgie.Modules.Relationships.UnitTests.Application.Persons;

public class CreatePersonCommandHandlerTests
{
    private readonly RelationshipsDbContext _dbContext;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly CreatePersonCommandHandler _handler;
    private readonly Guid _userId = Guid.NewGuid();

    public CreatePersonCommandHandlerTests()
    {
        _currentUserProvider = Substitute.For<ICurrentUserProvider>();
        _currentUserProvider.GetUserId().Returns(_userId);
        _currentUserProvider.UserId.Returns(_userId);

        var options = new DbContextOptionsBuilder<RelationshipsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _dbContext = new RelationshipsDbContext(options, _currentUserProvider);
        _handler = new CreatePersonCommandHandler(_dbContext, _currentUserProvider, NullLogger<CreatePersonCommandHandler>.Instance);
    }

    [Fact]
    public async Task Handle_ShouldCreatePerson()
    {
        // Arrange
        var command = new CreatePersonCommand("John Doe");

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsError.Should().BeFalse();
        var person = await _dbContext.Persons.FirstAsync();
        person.Name.Value.Should().Be("John Doe");
        person.UserId.Should().Be(_userId);
    }
}
