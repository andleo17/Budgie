using Budgie.Modules.Finances.Application.Goals.GetGoals;
using Budgie.Modules.Finances.Domain.Accounts;
using Budgie.Modules.Finances.Domain.Goals;
using Budgie.Modules.Finances.Infrastructure.Database;
using Budgie.Shared.Abstractions;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Xunit;

namespace Budgie.Modules.Finances.UnitTests.Application.Goals.GetGoals;

public class GetGoalsQueryHandlerTests
{
    private readonly FinancesDbContext _dbContext;
    private readonly GetGoalsQueryHandler _handler;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly Guid _userId = Guid.NewGuid();

    public GetGoalsQueryHandlerTests()
    {
        _currentUserProvider = Substitute.For<ICurrentUserProvider>();
        _currentUserProvider.UserId.Returns(_userId);

        var options = new DbContextOptionsBuilder<FinancesDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _dbContext = new FinancesDbContext(options, _currentUserProvider);
        _handler = new GetGoalsQueryHandler(_dbContext);
    }

    [Fact]
    public async Task Handle_ShouldReturnGoalsForUsersAccounts()
    {
        var account = Account.Create(_userId, "Savings", "USD", 1000m);
        _dbContext.Accounts.Add(account);
        var goal = Goal.Create(_userId, account.Id, 5000m, "USD", DateTime.UtcNow.AddMonths(6));
        _dbContext.Goals.Add(goal);
        await _dbContext.SaveChangesAsync();

        var result = await _handler.Handle(new GetGoalsQuery(), CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.Should().HaveCount(1);
        result.Value[0].AccountId.Should().Be(account.Id);
        result.Value[0].TargetAmount.Should().Be(5000m);
    }

    [Fact]
    public async Task Handle_ShouldReturnEmptyList_WhenNoGoals()
    {
        var result = await _handler.Handle(new GetGoalsQuery(), CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_ShouldExcludeOtherUsersGoals()
    {
        var otherUserId = Guid.NewGuid();
        var myAccount = Account.Create(_userId, "My Savings", "USD", 1000m);
        var theirAccount = Account.Create(otherUserId, "Their Savings", "USD", 2000m);
        _dbContext.Accounts.AddRange(myAccount, theirAccount);
        _dbContext.Goals.AddRange(
            Goal.Create(_userId, myAccount.Id, 5000m, "USD", null),
            Goal.Create(otherUserId, theirAccount.Id, 10000m, "USD", null));
        await _dbContext.SaveChangesAsync();

        var result = await _handler.Handle(new GetGoalsQuery(), CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.Should().HaveCount(1);
        result.Value[0].AccountId.Should().Be(myAccount.Id);
    }

    [Fact]
    public async Task Handle_ShouldCalculateProgressPercentage()
    {
        var account = Account.Create(_userId, "Savings", "USD", 2500m);
        _dbContext.Accounts.Add(account);
        var goal = Goal.Create(_userId, account.Id, 5000m, "USD", null);
        _dbContext.Goals.Add(goal);
        await _dbContext.SaveChangesAsync();

        var result = await _handler.Handle(new GetGoalsQuery(), CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value[0].ProgressPercentage.Should().Be(50.0);
    }
}
