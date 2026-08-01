using Budgie.Modules.Relationships.Application.Tags;
using Budgie.Modules.Relationships.Application.Tags.GetTags;
using Budgie.Modules.Relationships.Domain.Tags;
using Budgie.Modules.Relationships.Infrastructure.Database;
using Budgie.Shared.Abstractions;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Xunit;

namespace Budgie.Modules.Relationships.UnitTests.Application.Tags;

public class GetTagsQueryHandlerTests
{
    private readonly RelationshipsDbContext _dbContext;
    private readonly GetTagsQueryHandler _handler;
    private readonly Guid _userId = Guid.NewGuid();

    public GetTagsQueryHandlerTests()
    {
        var currentUserProvider = Substitute.For<ICurrentUserProvider>();
        currentUserProvider.UserId.Returns(_userId);

        var options = new DbContextOptionsBuilder<RelationshipsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _dbContext = new RelationshipsDbContext(options, currentUserProvider);
        _handler = new GetTagsQueryHandler(_dbContext);
    }

    [Fact]
    public async Task Handle_ShouldReturnAllTagsForUser()
    {
        _dbContext.Tags.Add(Tag.Create(_userId, "Friend"));
        _dbContext.Tags.Add(Tag.Create(_userId, "Family"));
        await _dbContext.SaveChangesAsync();

        var result = await _handler.Handle(new GetTagsQuery(), CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.Should().HaveCount(2);
        result.Value.Select(t => t.Name).Should().BeEquivalentTo(new[] { "Friend", "Family" });
    }

    [Fact]
    public async Task Handle_WhenNoTags_ReturnsEmptyList()
    {
        var result = await _handler.Handle(new GetTagsQuery(), CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.Should().BeEmpty();
    }
}