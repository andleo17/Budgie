using Budgie.Modules.Finances.Application.Categories.CreateCategory;
using Budgie.Modules.Finances.Domain.Categories;
using Budgie.Modules.Finances.Infrastructure.Database;
using Budgie.Shared.Abstractions;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace Budgie.Modules.Finances.UnitTests.Application.Categories;

public class CreateCategoryCommandHandlerTests
{
    private readonly FinancesDbContext _dbContext;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly CreateCategoryCommandHandler _handler;
    private readonly Guid _userId = Guid.NewGuid();

    public CreateCategoryCommandHandlerTests()
    {
        _currentUserProvider = Substitute.For<ICurrentUserProvider>();
        _currentUserProvider.GetUserId().Returns(_userId);
        _currentUserProvider.UserId.Returns(_userId);

        var options = new DbContextOptionsBuilder<FinancesDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _dbContext = new FinancesDbContext(options, _currentUserProvider);
        _handler = new CreateCategoryCommandHandler(_dbContext, _currentUserProvider, NullLogger<CreateCategoryCommandHandler>.Instance);
    }

    [Fact]
    public async Task Handle_ShouldCreateCategory()
    {
        var command = new CreateCategoryCommand("Food", "#FF0000", "pizza", 1);

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsError.Should().BeFalse();
        var category = await _dbContext.Categories.FirstAsync();
        category.Name.Value.Should().Be("Food");
        category.UserId.Should().Be(_userId);
        category.Type.Should().Be(1);
    }

    [Fact]
    public async Task Handle_WhenParentAtMaxDepth_ShouldReturnError()
    {
        // L0 → L1 → L2 (depth 0, 1, 2). L2 is at max depth → cannot have children.
        var l0 = Category.Create(_userId, "L0", 1);
        var l1 = Category.Create(_userId, "L1", 1, null, null, l0.Id);
        var l2 = Category.Create(_userId, "L2", 1, null, null, l1.Id);
        _dbContext.Categories.AddRange(l0, l1, l2);
        await _dbContext.SaveChangesAsync();

        var command = new CreateCategoryCommand("Too Deep", null, null, 1, l2.Id);

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("Category.MaxDepthExceeded");
    }

    [Fact]
    public async Task Handle_WhenParentWithinDepthLimit_ShouldSucceed()
    {
        // L0 → L1 (depth 0, 1). L1 is at depth 1 < 2 → can have children.
        var l0 = Category.Create(_userId, "L0", 1);
        var l1 = Category.Create(_userId, "L1", 1, null, null, l0.Id);
        _dbContext.Categories.AddRange(l0, l1);
        await _dbContext.SaveChangesAsync();

        var command = new CreateCategoryCommand("OK", null, null, 1, l1.Id);

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsError.Should().BeFalse();
    }
}
