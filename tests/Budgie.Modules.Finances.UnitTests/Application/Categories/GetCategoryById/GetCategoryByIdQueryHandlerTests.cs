using Budgie.Modules.Finances.Application.Categories;
using Budgie.Modules.Finances.Application.Categories.GetCategoryById;
using Budgie.Modules.Finances.Domain.Categories;
using Budgie.Modules.Finances.Infrastructure.Database;
using Budgie.Shared.Abstractions;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Xunit;

namespace Budgie.Modules.Finances.UnitTests.Application.Categories.GetCategoryById;

public class GetCategoryByIdQueryHandlerTests
{
    private readonly FinancesDbContext _dbContext;
    private readonly GetCategoryByIdQueryHandler _handler;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly Guid _userId = Guid.NewGuid();

    public GetCategoryByIdQueryHandlerTests()
    {
        _currentUserProvider = Substitute.For<ICurrentUserProvider>();
        _currentUserProvider.UserId.Returns(_userId);

        var options = new DbContextOptionsBuilder<FinancesDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _dbContext = new FinancesDbContext(options, _currentUserProvider);
        _handler = new GetCategoryByIdQueryHandler(_dbContext);
    }

    [Fact]
    public async Task Handle_ShouldReturnCategoryById()
    {
        var cat = Category.Create(_userId, "Food", 1, "#FF0000", "pizza");
        _dbContext.Categories.Add(cat);
        await _dbContext.SaveChangesAsync();

        var result = await _handler.Handle(new GetCategoryByIdQuery(cat.Id), CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.Id.Should().Be(cat.Id);
        result.Value.Name.Should().Be("Food");
    }

    [Fact]
    public async Task Handle_ShouldReturnNotFound_WhenCategoryDoesNotExist()
    {
        var result = await _handler.Handle(new GetCategoryByIdQuery(Guid.NewGuid()), CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Type.Should().Be(ErrorOr.ErrorType.NotFound);
        result.FirstError.Code.Should().Be("Category.NotFound");
    }
}
