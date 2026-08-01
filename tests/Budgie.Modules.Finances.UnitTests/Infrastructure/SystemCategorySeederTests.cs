using Budgie.Modules.Finances.Domain.Categories;
using Budgie.Modules.Finances.Infrastructure.Database;
using Budgie.Shared.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using FluentAssertions;
using Xunit;

namespace Budgie.Modules.Finances.UnitTests.Infrastructure;

public class SystemCategorySeederTests
{
    private readonly FinancesDbContext _dbContext;
    private readonly SystemCategorySeeder _seeder;

    public SystemCategorySeederTests()
    {
        var currentUserProvider = Substitute.For<ICurrentUserProvider>();
        currentUserProvider.UserId.Returns(Guid.NewGuid());

        var options = new DbContextOptionsBuilder<FinancesDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _dbContext = new FinancesDbContext(options, currentUserProvider);

        var services = new ServiceCollection();
        services.AddSingleton(_dbContext);

        var scopeFactory = services.BuildServiceProvider().GetRequiredService<IServiceScopeFactory>();
        _seeder = new SystemCategorySeeder(scopeFactory, NullLogger<SystemCategorySeeder>.Instance);
    }

    [Fact]
    public async Task StartAsync_WhenNoneExist_SeedsSystemCategories()
    {
        await _seeder.StartAsync(CancellationToken.None);

        var count = await _dbContext.Categories.IgnoreQueryFilters().CountAsync();
        count.Should().Be(6);
    }

    [Fact]
    public async Task StartAsync_WhenAlreadySeeded_DoesNotDuplicate()
    {
        await _seeder.StartAsync(CancellationToken.None);
        await _seeder.StartAsync(CancellationToken.None);

        var count = await _dbContext.Categories.IgnoreQueryFilters().CountAsync();
        count.Should().Be(6);
    }
}