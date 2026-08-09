using Budgie.Modules.Relationships.Application.Tags.CreateTag;
using Budgie.Modules.Relationships.Application.Tags.DeleteTag;
using Budgie.Modules.Relationships.Application.Tags.UpdateTag;
using Budgie.Modules.Relationships.Domain.Tags;
using Budgie.Modules.Relationships.Infrastructure.Database;
using Budgie.Shared.Abstractions;
using ErrorOr;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Xunit;

namespace Budgie.Modules.Relationships.UnitTests.Application.Tags;

public class TagCrudHandlerTests
{
    private readonly RelationshipsDbContext _dbContext;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly Guid _userId = Guid.NewGuid();

    public TagCrudHandlerTests()
    {
        _currentUserProvider = Substitute.For<ICurrentUserProvider>();
        _currentUserProvider.UserId.Returns(_userId);
        _currentUserProvider.GetUserId().Returns(_userId);

        var options = new DbContextOptionsBuilder<RelationshipsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _dbContext = new RelationshipsDbContext(options, _currentUserProvider);
    }

    [Fact]
    public async Task Handle_CreateTag_ShouldCreate()
    {
        var handler = new CreateTagCommandHandler(_dbContext, _currentUserProvider);
        var command = new CreateTagCommand("Friend");

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsError.Should().BeFalse();
        var tag = await _dbContext.Tags.FirstAsync();
        tag.Name.Value.Should().Be("Friend");
        tag.UserId.Should().Be(_userId);
    }

    [Fact]
    public async Task Handle_CreateTag_EmptyName_ReturnsError()
    {
        var handler = new CreateTagCommandHandler(_dbContext, _currentUserProvider);
        var command = new CreateTagCommand("");

        var act = () => handler.Handle(command, CancellationToken.None).AsTask();

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task Handle_CreateTag_WithColor_StoresColor()
    {
        var handler = new CreateTagCommandHandler(_dbContext, _currentUserProvider);
        var command = new CreateTagCommand("Family", "#00FF00");

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsError.Should().BeFalse();
        var tag = await _dbContext.Tags.FirstAsync();
        tag.Color!.Value.Should().Be("#00FF00");
    }

    [Fact]
    public async Task Handle_UpdateTag_WhenExists_ShouldUpdate()
    {
        var tag = Tag.Create(_userId, "Old", "#000000");
        _dbContext.Tags.Add(tag);
        await _dbContext.SaveChangesAsync();

        var handler = new UpdateTagCommandHandler(_dbContext);
        var command = new UpdateTagCommand(tag.Id, "New", "#FFFFFF");
        var result = await handler.Handle(command, CancellationToken.None);

        result.IsError.Should().BeFalse();
        var updated = await _dbContext.Tags.FirstAsync();
        updated.Name.Value.Should().Be("New");
        updated.Color!.Value.Should().Be("#FFFFFF");
    }

    [Fact]
    public async Task Handle_UpdateTag_WhenNotFound_ReturnsError()
    {
        var handler = new UpdateTagCommandHandler(_dbContext);
        var command = new UpdateTagCommand(Guid.NewGuid(), "Ghost", null);
        var result = await handler.Handle(command, CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("Tag.NotFound");
    }

    [Fact]
    public async Task Handle_DeleteTag_WhenExists_ShouldDelete()
    {
        var tag = Tag.Create(_userId, "Delete Me");
        _dbContext.Tags.Add(tag);
        await _dbContext.SaveChangesAsync();

        var handler = new DeleteTagCommandHandler(_dbContext);
        var command = new DeleteTagCommand(tag.Id);
        var result = await handler.Handle(command, CancellationToken.None);

        result.IsError.Should().BeFalse();
        var exists = await _dbContext.Tags.AnyAsync(t => t.Id == tag.Id);
        exists.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_DeleteTag_WhenNotFound_ReturnsError()
    {
        var handler = new DeleteTagCommandHandler(_dbContext);
        var command = new DeleteTagCommand(Guid.NewGuid());
        var result = await handler.Handle(command, CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("Tag.NotFound");
    }
}
