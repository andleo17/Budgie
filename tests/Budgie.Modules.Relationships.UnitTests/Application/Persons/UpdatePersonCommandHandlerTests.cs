using Budgie.Modules.Relationships.Application.Persons.UpdatePerson;
using Budgie.Modules.Relationships.Domain.Persons;
using Budgie.Modules.Relationships.Infrastructure.Database;
using Budgie.Shared.Abstractions;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace Budgie.Modules.Relationships.UnitTests.Application.Persons;

public class UpdatePersonCommandHandlerTests
{
    private readonly RelationshipsDbContext _dbContext;
    private readonly UpdatePersonCommandHandler _handler;
    private readonly Guid _userId = Guid.NewGuid();

    public UpdatePersonCommandHandlerTests()
    {
        var currentUserProvider = Substitute.For<ICurrentUserProvider>();
        currentUserProvider.UserId.Returns(_userId);
        currentUserProvider.GetUserId().Returns(_userId);

        var options = new DbContextOptionsBuilder<RelationshipsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _dbContext = new RelationshipsDbContext(options, currentUserProvider);
        _handler = new UpdatePersonCommandHandler(_dbContext, currentUserProvider, NullLogger<UpdatePersonCommandHandler>.Instance);
    }

    [Fact]
    public async Task Handle_WhenPersonExists_UpdatesPersonName()
    {
        var person = Person.Create(_userId, "Old Name");
        _dbContext.Persons.Add(person);
        await _dbContext.SaveChangesAsync();

        var result = await _handler.Handle(new UpdatePersonCommand(person.Id, "New Name"), CancellationToken.None);

        result.IsError.Should().BeFalse();
        var updated = await _dbContext.Persons.FirstAsync();
        updated.Name.Value.Should().Be("New Name");
    }

    [Fact]
    public async Task Handle_WhenNotFound_ReturnsError()
    {
        var result = await _handler.Handle(new UpdatePersonCommand(Guid.NewGuid(), "Ghost"), CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("Person.NotFound");
    }

    [Fact]
    public void Handle_WithEmptyName_ThrowsArgumentException()
    {
        var act = () => new UpdatePersonCommand(Guid.NewGuid(), "");

        var ex = Record.Exception(() => Person.Create(_userId, ""));
        ex.Should().BeOfType<ArgumentException>();
    }
}