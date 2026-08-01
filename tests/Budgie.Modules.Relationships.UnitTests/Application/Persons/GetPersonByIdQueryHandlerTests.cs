using Budgie.Modules.Relationships.Application.Persons;
using Budgie.Modules.Relationships.Application.Persons.GetPersonById;
using Budgie.Modules.Relationships.Domain.Persons;
using Budgie.Modules.Relationships.Infrastructure.Database;
using Budgie.Shared.Abstractions;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Xunit;

namespace Budgie.Modules.Relationships.UnitTests.Application.Persons;

public class GetPersonByIdQueryHandlerTests
{
    private readonly RelationshipsDbContext _dbContext;
    private readonly GetPersonByIdQueryHandler _handler;
    private readonly Guid _userId = Guid.NewGuid();

    public GetPersonByIdQueryHandlerTests()
    {
        var currentUserProvider = Substitute.For<ICurrentUserProvider>();
        currentUserProvider.UserId.Returns(_userId);

        var options = new DbContextOptionsBuilder<RelationshipsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _dbContext = new RelationshipsDbContext(options, currentUserProvider);
        _handler = new GetPersonByIdQueryHandler(_dbContext);
    }

    [Fact]
    public async Task Handle_WhenPersonExists_ReturnsPerson()
    {
        var person = Person.Create(_userId, "Alice");
        _dbContext.Persons.Add(person);
        await _dbContext.SaveChangesAsync();

        var result = await _handler.Handle(new GetPersonByIdQuery(person.Id), CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.Name.Should().Be("Alice");
        result.Value.Id.Should().Be(person.Id);
    }

    [Fact]
    public async Task Handle_WhenNotFound_ReturnsError()
    {
        var result = await _handler.Handle(new GetPersonByIdQuery(Guid.NewGuid()), CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("Person.NotFound");
    }
}