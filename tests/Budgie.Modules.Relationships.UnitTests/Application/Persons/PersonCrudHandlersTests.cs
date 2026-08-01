using Budgie.Modules.Relationships.Application.Persons.DeletePerson;
using Budgie.Modules.Relationships.Application.Persons.GetPersons;
using Budgie.Modules.Relationships.Application.Persons.UpdatePerson;
using Budgie.Modules.Relationships.Domain.Persons;
using Budgie.Modules.Relationships.Infrastructure.Database;
using Budgie.Shared.Abstractions;
using ErrorOr;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace Budgie.Modules.Relationships.UnitTests.Application.Persons;

public class PersonCrudHandlersTests
{
    private readonly RelationshipsDbContext _dbContext;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly Guid _userId = Guid.NewGuid();

    public PersonCrudHandlersTests()
    {
        _currentUserProvider = Substitute.For<ICurrentUserProvider>();
        _currentUserProvider.UserId.Returns(_userId);

        var options = new DbContextOptionsBuilder<RelationshipsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _dbContext = new RelationshipsDbContext(options, _currentUserProvider);
    }

    [Fact]
    public async Task Handle_Update_WhenPersonExists_ShouldUpdate()
    {
        var person = Person.Create(_userId, "Old Name");
        _dbContext.Persons.Add(person);
        await _dbContext.SaveChangesAsync();

        var handler = new UpdatePersonCommandHandler(_dbContext, _currentUserProvider, NullLogger<UpdatePersonCommandHandler>.Instance);
        var command = new UpdatePersonCommand(person.Id, "New Name");
        var result = await handler.Handle(command, CancellationToken.None);

        result.IsError.Should().BeFalse();
        var updated = await _dbContext.Persons.FirstAsync();
        updated.Name.Value.Should().Be("New Name");
    }

    [Fact]
    public async Task Handle_Delete_WhenPersonExists_ShouldDelete()
    {
        var person = Person.Create(_userId, "To Delete");
        _dbContext.Persons.Add(person);
        await _dbContext.SaveChangesAsync();

        var handler = new DeletePersonCommandHandler(_dbContext, NullLogger<DeletePersonCommandHandler>.Instance);
        var command = new DeletePersonCommand(person.Id);
        var result = await handler.Handle(command, CancellationToken.None);

        result.IsError.Should().BeFalse();
        var exists = await _dbContext.Persons.AnyAsync(p => p.Id == person.Id);
        exists.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_GetPersons_ShouldReturnUserPersons()
    {
        _dbContext.Persons.Add(Person.Create(_userId, "Person 1"));
        _dbContext.Persons.Add(Person.Create(_userId, "Person 2"));
        _dbContext.Persons.Add(Person.Create(Guid.NewGuid(), "Other User Person"));
        await _dbContext.SaveChangesAsync();

        var handler = new GetPersonsQueryHandler(_dbContext);
        var query = new GetPersonsQuery();
        var result = await handler.Handle(query, CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.Should().HaveCount(2);
    }
}
