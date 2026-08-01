using Budgie.Modules.Finances.Application.Categories.CreateCategory;
using Budgie.Modules.Finances.Application.Categories.DeleteCategory;
using Budgie.Modules.Finances.Application.Categories.UpdateCategory;
using Budgie.Modules.Finances.Domain.Categories;
using Budgie.Modules.Finances.Infrastructure.Database;
using Budgie.Shared.Abstractions;
using ErrorOr;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace Budgie.Modules.Finances.UnitTests.Application.Categories;

public class CategoryCrudHandlersTests
{
    private readonly FinancesDbContext _dbContext;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly Guid _userId = Guid.NewGuid();

    public CategoryCrudHandlersTests()
    {
        _currentUserProvider = Substitute.For<ICurrentUserProvider>();
        _currentUserProvider.UserId.Returns(_userId);
        _currentUserProvider.GetUserId().Returns(_userId);

        var options = new DbContextOptionsBuilder<FinancesDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _dbContext = new FinancesDbContext(options, _currentUserProvider);
    }

    [Fact]
    public async Task Handle_Update_WhenCategoryExists_ShouldUpdate()
    {
        var category = Category.Create(_userId, "Old Name", 1, null, null);
        _dbContext.Categories.Add(category);
        await _dbContext.SaveChangesAsync();

        var handler = new UpdateCategoryCommandHandler(_dbContext, NullLogger<UpdateCategoryCommandHandler>.Instance);
        var command = new UpdateCategoryCommand(category.Id, "New Name", "#000000", "icon");
        var result = await handler.Handle(command, CancellationToken.None);

        result.IsError.Should().BeFalse();
        var updated = await _dbContext.Categories.FirstAsync();
        updated.Name.Value.Should().Be("New Name");
    }

    [Fact]
    public async Task Handle_Update_WhenClearingParentId_ShouldBecomeRoot()
    {
        var parent = Category.Create(_userId, "Parent", 1, null, null);
        var child = Category.Create(_userId, "Child", 1, null, null, parent.Id);
        _dbContext.Categories.AddRange(parent, child);
        await _dbContext.SaveChangesAsync();

        var handler = new UpdateCategoryCommandHandler(_dbContext, NullLogger<UpdateCategoryCommandHandler>.Instance);
        var command = new UpdateCategoryCommand(child.Id, "Child", null, null, null, null);
        var result = await handler.Handle(command, CancellationToken.None);

        result.IsError.Should().BeFalse();
        var updated = await _dbContext.Categories.FirstAsync(c => c.Id == child.Id);
        updated.ParentId.Should().BeNull();
    }

    [Fact]
    public async Task Handle_Update_WhenParentExceedsMaxDepth_ShouldReturnError()
    {
        // L0 → L1 → L2 (depths 0, 1, 2). L2 is at max depth, cannot have children.
        var l0 = Category.Create(_userId, "L0", 1);
        var l1 = Category.Create(_userId, "L1", 1, null, null, l0.Id);
        var l2 = Category.Create(_userId, "L2", 1, null, null, l1.Id);
        _dbContext.Categories.AddRange(l0, l1, l2);
        await _dbContext.SaveChangesAsync();

        var moved = Category.Create(_userId, "Moved", 1);
        _dbContext.Categories.Add(moved);
        await _dbContext.SaveChangesAsync();

        var handler = new UpdateCategoryCommandHandler(_dbContext, NullLogger<UpdateCategoryCommandHandler>.Instance);
        var command = new UpdateCategoryCommand(moved.Id, "Moved", null, null, null, l2.Id);
        var result = await handler.Handle(command, CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("Category.MaxDepthExceeded");
    }

    [Fact]
    public async Task Handle_Update_WhenParentWithinDepthLimit_ShouldSucceed()
    {
        // L0 → L1 (depths 0, 1). L1 is at depth 1 < 2 → can have children.
        var l0 = Category.Create(_userId, "L0", 1);
        var l1 = Category.Create(_userId, "L1", 1, null, null, l0.Id);
        _dbContext.Categories.AddRange(l0, l1);
        await _dbContext.SaveChangesAsync();

        var moved = Category.Create(_userId, "Moved", 1);
        _dbContext.Categories.Add(moved);
        await _dbContext.SaveChangesAsync();

        var handler = new UpdateCategoryCommandHandler(_dbContext, NullLogger<UpdateCategoryCommandHandler>.Instance);
        var command = new UpdateCategoryCommand(moved.Id, "Moved", null, null, null, l1.Id);
        var result = await handler.Handle(command, CancellationToken.None);

        result.IsError.Should().BeFalse();
        var updated = await _dbContext.Categories.FirstAsync(c => c.Id == moved.Id);
        updated.ParentId.Should().Be(l1.Id);
    }

    [Fact]
    public async Task Handle_Update_WhenSettingOwnParent_ShouldReturnError()
    {
        var category = Category.Create(_userId, "Self", 1, null, null);
        _dbContext.Categories.Add(category);
        await _dbContext.SaveChangesAsync();

        var handler = new UpdateCategoryCommandHandler(_dbContext, NullLogger<UpdateCategoryCommandHandler>.Instance);
        var command = new UpdateCategoryCommand(category.Id, "Self", null, null, null, category.Id);
        var result = await handler.Handle(command, CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("Category.CircularReference");
    }

    [Fact]
    public async Task Handle_Update_WhenCreatingCircularReference_ShouldReturnError()
    {
        var parent = Category.Create(_userId, "Parent", 1, null, null);
        var child = Category.Create(_userId, "Child", 1, null, null, parent.Id);
        _dbContext.Categories.AddRange(parent, child);
        await _dbContext.SaveChangesAsync();

        // Make parent a child of its own child → circular
        var handler = new UpdateCategoryCommandHandler(_dbContext, NullLogger<UpdateCategoryCommandHandler>.Instance);
        var command = new UpdateCategoryCommand(parent.Id, "Parent", null, null, null, child.Id);
        var result = await handler.Handle(command, CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("Category.CircularReference");
    }

    [Fact]
    public async Task Handle_Update_WhenMovingWithDescendantsExceedsMaxDepth_ShouldReturnError()
    {
        // A → B → C (A root, B depth 1, C depth 2)
        // E → F (E root, F depth 1)
        var a = Category.Create(_userId, "A", 1);
        var b = Category.Create(_userId, "B", 1, null, null, a.Id);
        var c = Category.Create(_userId, "C", 1, null, null, b.Id);
        var e = Category.Create(_userId, "E", 1);
        var f = Category.Create(_userId, "F", 1, null, null, e.Id);
        _dbContext.Categories.AddRange(a, b, c, e, f);
        await _dbContext.SaveChangesAsync();

        var handler = new UpdateCategoryCommandHandler(_dbContext, NullLogger<UpdateCategoryCommandHandler>.Instance);

        // Move B under F: parentDepth=1 (< 2 ✓), B current depth=1, delta=(1+1)-1=1
        // C would go to depth 2+1=3 > 2 → exceeds
        var command = new UpdateCategoryCommand(b.Id, "B", null, null, null, f.Id);
        var result = await handler.Handle(command, CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("Category.MaxDepthExceeded");
    }

    [Fact]
    public async Task Handle_Update_WhenMovingWithoutDescendantOverflow_ShouldSucceed()
    {
        // A → B → C (A root, B depth 1, C depth 2)
        // E (root, depth 0)
        var a = Category.Create(_userId, "A", 1);
        var b = Category.Create(_userId, "B", 1, null, null, a.Id);
        var c = Category.Create(_userId, "C", 1, null, null, b.Id);
        var e = Category.Create(_userId, "E", 1);
        _dbContext.Categories.AddRange(a, b, c, e);
        await _dbContext.SaveChangesAsync();

        var handler = new UpdateCategoryCommandHandler(_dbContext, NullLogger<UpdateCategoryCommandHandler>.Instance);

        // Move B under E: parentDepth=0, B's current depth=1, delta=0
        // C stays at depth 2 → ok
        var command = new UpdateCategoryCommand(b.Id, "B", null, null, null, e.Id);
        var result = await handler.Handle(command, CancellationToken.None);

        result.IsError.Should().BeFalse();
        var updated = await _dbContext.Categories.FirstAsync(c => c.Id == b.Id);
        updated.ParentId.Should().Be(e.Id);
    }

    [Fact]
    public async Task Handle_Delete_WhenCategoryExists_ShouldDelete()
    {
        var category = Category.Create(_userId, "To Delete", 1, null, null);
        _dbContext.Categories.Add(category);
        await _dbContext.SaveChangesAsync();

        var handler = new DeleteCategoryCommandHandler(_dbContext, NullLogger<DeleteCategoryCommandHandler>.Instance);
        var command = new DeleteCategoryCommand(category.Id);
        var result = await handler.Handle(command, CancellationToken.None);

        result.IsError.Should().BeFalse();
        var exists = await _dbContext.Categories.AnyAsync(c => c.Id == category.Id);
        exists.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_CreateCategory_ShouldCreateWithParent()
    {
        var parent = Category.Create(_userId, "Parent", 1, null, null);
        _dbContext.Categories.Add(parent);
        await _dbContext.SaveChangesAsync();

        var handler = new CreateCategoryCommandHandler(_dbContext, _currentUserProvider, NullLogger<CreateCategoryCommandHandler>.Instance);
        var command = new CreateCategoryCommand("Child", null, null, 1, parent.Id);
        var result = await handler.Handle(command, CancellationToken.None);

        result.IsError.Should().BeFalse();
        var child = await _dbContext.Categories.FirstAsync(c => c.Id == result.Value);
        child.ParentId.Should().Be(parent.Id);
    }

    [Fact]
    public async Task Handle_CreateCategory_WithColor_StoresColor()
    {
        var handler = new CreateCategoryCommandHandler(_dbContext, _currentUserProvider, NullLogger<CreateCategoryCommandHandler>.Instance);
        var command = new CreateCategoryCommand("Colored", "#00FF00", null, 1);
        var result = await handler.Handle(command, CancellationToken.None);

        result.IsError.Should().BeFalse();
        var category = await _dbContext.Categories.FirstAsync(c => c.Id == result.Value);
        category.Color.Should().NotBeNull();
        category.Color!.Value.Should().Be("#00FF00");
    }

    [Fact]
    public async Task Handle_CreateCategory_EmptyName_ReturnsError()
    {
        var handler = new CreateCategoryCommandHandler(_dbContext, _currentUserProvider, NullLogger<CreateCategoryCommandHandler>.Instance);
        var command = new CreateCategoryCommand(" ", null, null, 1);

        await Assert.ThrowsAsync<ArgumentException>(() => handler.Handle(command, CancellationToken.None));
    }

    [Fact]
    public async Task Handle_UpdateCategory_ShouldUpdateFields()
    {
        var category = Category.Create(_userId, "Original", 1, "#FF0000", "star");
        _dbContext.Categories.Add(category);
        await _dbContext.SaveChangesAsync();

        var handler = new UpdateCategoryCommandHandler(_dbContext, NullLogger<UpdateCategoryCommandHandler>.Instance);
        var command = new UpdateCategoryCommand(category.Id, "Updated", "#0000FF", "moon", 2);
        var result = await handler.Handle(command, CancellationToken.None);

        result.IsError.Should().BeFalse();
        var updated = await _dbContext.Categories.FirstAsync(c => c.Id == category.Id);
        updated.Name.Value.Should().Be("Updated");
        updated.Color!.Value.Should().Be("#0000FF");
        updated.Icon.Should().Be("moon");
        updated.Type.Should().Be(2);
    }

    [Fact]
    public async Task Handle_UpdateCategory_WithInvalidName_ReturnsError()
    {
        var category = Category.Create(_userId, "Valid", 1);
        _dbContext.Categories.Add(category);
        await _dbContext.SaveChangesAsync();

        var handler = new UpdateCategoryCommandHandler(_dbContext, NullLogger<UpdateCategoryCommandHandler>.Instance);
        var command = new UpdateCategoryCommand(category.Id, "", null, null);

        await Assert.ThrowsAsync<ArgumentException>(() => handler.Handle(command, CancellationToken.None));
    }

    [Fact]
    public async Task Handle_DeleteCategory_WithChildren_HandlesCorrectly()
    {
        var parent = Category.Create(_userId, "Parent", 1);
        var child = Category.Create(_userId, "Child", 1, null, null, parent.Id);
        _dbContext.Categories.AddRange(parent, child);
        await _dbContext.SaveChangesAsync();

        var handler = new DeleteCategoryCommandHandler(_dbContext, NullLogger<DeleteCategoryCommandHandler>.Instance);
        var command = new DeleteCategoryCommand(parent.Id);
        var result = await handler.Handle(command, CancellationToken.None);

        result.IsError.Should().BeFalse();
        var parentExists = await _dbContext.Categories.AnyAsync(c => c.Id == parent.Id);
        parentExists.Should().BeFalse();
        var updatedChild = await _dbContext.Categories.FirstAsync(c => c.Id == child.Id);
        updatedChild.ParentId.Should().BeNull();
    }
}
