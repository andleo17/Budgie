using Budgie.Modules.Relationships.Application.Persons;
using Budgie.Modules.Relationships.Application.Persons.GetPersons;
using Budgie.Modules.Relationships.Application.Tags;
using Budgie.Modules.Relationships.Domain.Persons;
using Budgie.Modules.Relationships.Infrastructure.Database;
using Budgie.Shared.Abstractions;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Xunit;

namespace Budgie.Modules.Relationships.UnitTests.Application.Persons;

public class GetPersonsQueryHandlerTests
{
    private readonly RelationshipsDbContext _dbContext;
    private readonly GetPersonsQueryHandler _handler;
    private readonly Guid _userId = Guid.NewGuid();

    public GetPersonsQueryHandlerTests()
    {
        var currentUserProvider = Substitute.For<ICurrentUserProvider>();
        currentUserProvider.UserId.Returns(_userId);

        var options = new DbContextOptionsBuilder<RelationshipsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _dbContext = new RelationshipsDbContext(options, currentUserProvider);
        _handler = new GetPersonsQueryHandler(_dbContext);
    }

    [Fact]
    public async Task Handle_ShouldReturnAllPersonsForUser()
    {
        _dbContext.Persons.Add(Person.Create(_userId, "Alice"));
        _dbContext.Persons.Add(Person.Create(_userId, "Bob"));
        await _dbContext.SaveChangesAsync();

        var result = await _handler.Handle(new GetPersonsQuery(), CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.Should().HaveCount(2);
        result.Value.Select(p => p.Name).Should().BeEquivalentTo(new[] { "Alice", "Bob" });
    }

    [Fact]
    public async Task Handle_WhenNoPersons_ReturnsEmptyList()
    {
        var result = await _handler.Handle(new GetPersonsQuery(), CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_ExcludesOtherUsersPersons()
    {
        _dbContext.Persons.Add(Person.Create(_userId, "My Person"));
        _dbContext.Persons.Add(Person.Create(Guid.NewGuid(), "Other Person"));
        await _dbContext.SaveChangesAsync();

        var result = await _handler.Handle(new GetPersonsQuery(), CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.Should().ContainSingle(p => p.Name == "My Person");
    }
}