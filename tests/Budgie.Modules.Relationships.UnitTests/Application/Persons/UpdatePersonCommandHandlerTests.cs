using Budgie.Modules.Relationships.Application.Persons.UpdatePerson;
using Budgie.Modules.Relationships.Domain.Persons;
using Budgie.Modules.Relationships.Domain.Tags;
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
    public async Task Handle_WithNewTagName_CreatesTagAndAssociatesIt()
    {
        var person = Person.Create(_userId, "Someone");
        _dbContext.Persons.Add(person);
        await _dbContext.SaveChangesAsync();

        var result = await _handler.Handle(
            new UpdatePersonCommand(person.Id, "Someone", ["New Tag"]),
            CancellationToken.None);

        result.IsError.Should().BeFalse();
        var updated = await _dbContext.Persons.Include(p => p.Tags).FirstAsync(p => p.Id == person.Id);
        updated.Tags.Should().ContainSingle(t => t.Name.Value == "New Tag");
        var tag = await _dbContext.Tags.SingleAsync(t => t.Name.Value == "New Tag");
        tag.UserId.Should().Be(_userId);
    }

    [Fact]
    public async Task Handle_WithExistingTagName_ReusesExistingTag()
    {
        var person = Person.Create(_userId, "Someone");
        var tag = Tag.Create(_userId, "Family");
        person.Tags.Add(tag);
        _dbContext.Persons.Add(person);
        await _dbContext.SaveChangesAsync();

        var result = await _handler.Handle(
            new UpdatePersonCommand(person.Id, "Someone", ["Family"]),
            CancellationToken.None);

        result.IsError.Should().BeFalse();
        var tagCount = await _dbContext.Tags.CountAsync(t => t.Name.Value == "Family");
        tagCount.Should().Be(1);
    }

    [Fact]
    public void Handle_WithEmptyName_ThrowsArgumentException()
    {
        var act = () => new UpdatePersonCommand(Guid.NewGuid(), "");

        var ex = Record.Exception(() => Person.Create(_userId, ""));
        ex.Should().BeOfType<ArgumentException>();
    }
}