using Budgie.Modules.Relationships.Application.Persons.DeletePerson;
using Budgie.Modules.Relationships.Domain.Persons;
using Budgie.Modules.Relationships.Infrastructure.Database;
using Budgie.Shared.Abstractions;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace Budgie.Modules.Relationships.UnitTests.Application.Persons;

public class DeletePersonCommandHandlerTests
{
    private readonly RelationshipsDbContext _dbContext;
    private readonly DeletePersonCommandHandler _handler;
    private readonly Guid _userId = Guid.NewGuid();

    public DeletePersonCommandHandlerTests()
    {
        var currentUserProvider = Substitute.For<ICurrentUserProvider>();
        currentUserProvider.UserId.Returns(_userId);

        var options = new DbContextOptionsBuilder<RelationshipsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _dbContext = new RelationshipsDbContext(options, currentUserProvider);
        _handler = new DeletePersonCommandHandler(_dbContext, NullLogger<DeletePersonCommandHandler>.Instance);
    }

    [Fact]
    public async Task Handle_WhenPersonExists_DeletesPerson()
    {
        var person = Person.Create(_userId, "To Delete");
        _dbContext.Persons.Add(person);
        await _dbContext.SaveChangesAsync();

        var result = await _handler.Handle(new DeletePersonCommand(person.Id), CancellationToken.None);

        result.IsError.Should().BeFalse();
        var exists = await _dbContext.Persons.AnyAsync(p => p.Id == person.Id);
        exists.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_WhenNotFound_ReturnsError()
    {
        var result = await _handler.Handle(new DeletePersonCommand(Guid.NewGuid()), CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("Person.NotFound");
    }
}