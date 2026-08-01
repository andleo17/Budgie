using Budgie.Modules.Finances.Application.Reports;
using Budgie.Modules.Finances.Application.Reports.GetGoalsStatus;
using Budgie.Modules.Finances.Domain.Accounts;
using Budgie.Modules.Finances.Domain.Goals;
using Budgie.Modules.Finances.Infrastructure.Database;
using Budgie.Shared.Abstractions;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Xunit;

namespace Budgie.Modules.Finances.UnitTests.Application.Reports.GetGoalsStatus;

public class GetGoalsStatusQueryHandlerTests
{
    private readonly FinancesDbContext _dbContext;
    private readonly GetGoalsStatusQueryHandler _handler;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly Guid _userId = Guid.NewGuid();

    public GetGoalsStatusQueryHandlerTests()
    {
        _currentUserProvider = Substitute.For<ICurrentUserProvider>();
        _currentUserProvider.UserId.Returns(_userId);

        var options = new DbContextOptionsBuilder<FinancesDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _dbContext = new FinancesDbContext(options, _currentUserProvider);
        _handler = new GetGoalsStatusQueryHandler(_dbContext);
    }

    [Fact]
    public async Task Handle_ShouldReturnGoalsStatusWithProgress()
    {
        var account = Account.Create(_userId, "Savings", "USD", 2500m);
        _dbContext.Accounts.Add(account);
        var goal = Goal.Create(_userId, account.Id, 5000m, "USD", DateTime.UtcNow.AddMonths(6));
        _dbContext.Goals.Add(goal);
        await _dbContext.SaveChangesAsync();

        var result = await _handler.Handle(new GetGoalsStatusQuery(), CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.Should().HaveCount(1);
        result.Value[0].AccountId.Should().Be(account.Id);
        result.Value[0].CurrentBalance.Should().Be(2500m);
        result.Value[0].TargetAmount.Should().Be(5000m);
        result.Value[0].ProgressPercentage.Should().Be(50.0);
    }

    [Fact]
    public async Task Handle_ShouldReturnEmpty_WhenNoGoals()
    {
        var result = await _handler.Handle(new GetGoalsStatusQuery(), CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.Should().BeEmpty();
    }
}
