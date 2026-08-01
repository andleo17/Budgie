using Budgie.Modules.Finances.Application.Goals.UpdateGoal;
using Budgie.Modules.Finances.Domain.Accounts;
using Budgie.Modules.Finances.Domain.Goals;
using Budgie.Modules.Finances.Infrastructure.Database;
using Budgie.Shared.Abstractions;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace Budgie.Modules.Finances.UnitTests.Application.Goals.UpdateGoal;

public class UpdateGoalCommandHandlerTests
{
    private readonly FinancesDbContext _dbContext;
    private readonly UpdateGoalCommandHandler _handler;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly Guid _userId = Guid.NewGuid();

    public UpdateGoalCommandHandlerTests()
    {
        _currentUserProvider = Substitute.For<ICurrentUserProvider>();
        _currentUserProvider.UserId.Returns(_userId);

        var options = new DbContextOptionsBuilder<FinancesDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _dbContext = new FinancesDbContext(options, _currentUserProvider);
        _handler = new UpdateGoalCommandHandler(_dbContext, NullLogger<UpdateGoalCommandHandler>.Instance);
    }

    [Fact]
    public async Task Handle_ShouldUpdateTargetAmount()
    {
        var account = Account.Create(_userId, "Savings", "USD", 1000m);
        _dbContext.Accounts.Add(account);
        var goal = Goal.Create(_userId, account.Id, 5000m, "USD", null);
        _dbContext.Goals.Add(goal);
        await _dbContext.SaveChangesAsync();

        var result = await _handler.Handle(
            new UpdateGoalCommand(goal.Id, 8000m, null), CancellationToken.None);

        result.IsError.Should().BeFalse();
        var updated = await _dbContext.Goals.FirstAsync();
        updated.TargetAmount.Amount.Should().Be(8000m);
    }

    [Fact]
    public async Task Handle_ShouldUpdateTargetDate()
    {
        var account = Account.Create(_userId, "Savings", "USD", 1000m);
        _dbContext.Accounts.Add(account);
        var goal = Goal.Create(_userId, account.Id, 5000m, "USD", null);
        _dbContext.Goals.Add(goal);
        await _dbContext.SaveChangesAsync();

        var newDate = DateTime.UtcNow.AddMonths(12);
        var result = await _handler.Handle(
            new UpdateGoalCommand(goal.Id, 5000m, newDate), CancellationToken.None);

        result.IsError.Should().BeFalse();
        var updated = await _dbContext.Goals.FirstAsync();
        updated.TargetDate.Should().NotBeNull();
    }

    [Fact]
    public async Task Handle_ShouldReturnError_WhenGoalNotFound()
    {
        var result = await _handler.Handle(
            new UpdateGoalCommand(Guid.NewGuid(), 5000m, null), CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Type.Should().Be(ErrorOr.ErrorType.NotFound);
        result.FirstError.Code.Should().Be("Goal.NotFound");
    }
}
