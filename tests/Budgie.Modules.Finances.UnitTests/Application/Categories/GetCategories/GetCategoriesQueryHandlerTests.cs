using Budgie.Modules.Finances.Application.Categories;
using Budgie.Modules.Finances.Application.Categories.GetCategories;
using Budgie.Modules.Finances.Domain.Categories;
using Budgie.Modules.Finances.Infrastructure.Database;
using Budgie.Shared.Abstractions;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Xunit;

namespace Budgie.Modules.Finances.UnitTests.Application.Categories.GetCategories;

public class GetCategoriesQueryHandlerTests
{
    private readonly FinancesDbContext _dbContext;
    private readonly GetCategoriesQueryHandler _handler;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly Guid _userId = Guid.NewGuid();

    public GetCategoriesQueryHandlerTests()
    {
        _currentUserProvider = Substitute.For<ICurrentUserProvider>();
        _currentUserProvider.UserId.Returns(_userId);

        var options = new DbContextOptionsBuilder<FinancesDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _dbContext = new FinancesDbContext(options, _currentUserProvider);
        _handler = new GetCategoriesQueryHandler(_dbContext, _currentUserProvider);
    }

    [Fact]
    public async Task Handle_ShouldReturnUserCategories()
    {
        var cat = Category.Create(_userId, "Food", 1, "#FF0000", "pizza");
        _dbContext.Categories.Add(cat);
        await _dbContext.SaveChangesAsync();

        var result = await _handler.Handle(new GetCategoriesQuery(), CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.Should().ContainSingle(c => c.Name == "Food");
    }

    [Fact]
    public async Task Handle_WhenIncludeSystem_ShouldIncludeSystemCategories()
    {
        var systemCat = Category.Create(Guid.Empty, "System Cat", 1, null, null);
        typeof(Category).GetProperty("UserId")!.SetValue(systemCat, null);
        typeof(Category).GetProperty("IsSystem")!.SetValue(systemCat, true);
        var userCat = Category.Create(_userId, "My Cat", 1, null, null);
        _dbContext.Categories.AddRange(systemCat, userCat);
        await _dbContext.SaveChangesAsync();

        var result = await _handler.Handle(new GetCategoriesQuery(IncludeSystem: true), CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.Should().HaveCount(2);
    }

    [Fact]
    public async Task Handle_WhenNotIncludeSystem_ShouldExcludeSystemCategories()
    {
        var systemCat = Category.Create(Guid.Empty, "System Cat", 1, null, null);
        typeof(Category).GetProperty("UserId")!.SetValue(systemCat, null);
        typeof(Category).GetProperty("IsSystem")!.SetValue(systemCat, true);
        var userCat = Category.Create(_userId, "My Cat", 1, null, null);
        _dbContext.Categories.AddRange(systemCat, userCat);
        await _dbContext.SaveChangesAsync();

        var result = await _handler.Handle(new GetCategoriesQuery(), CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.Should().ContainSingle(c => c.Name == "My Cat");
        result.Value.Should().NotContain(c => c.Name == "System Cat");
    }
}
