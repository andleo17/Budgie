using Budgie.Modules.Finances.Application.Goals.DeleteGoal;
using Budgie.Modules.Finances.Domain.Accounts;
using Budgie.Modules.Finances.Domain.Goals;
using Budgie.Modules.Finances.Infrastructure.Database;
using Budgie.Shared.Abstractions;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace Budgie.Modules.Finances.UnitTests.Application.Goals.DeleteGoal;

public class DeleteGoalCommandHandlerTests
{
    private readonly FinancesDbContext _dbContext;
    private readonly DeleteGoalCommandHandler _handler;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly Guid _userId = Guid.NewGuid();

    public DeleteGoalCommandHandlerTests()
    {
        _currentUserProvider = Substitute.For<ICurrentUserProvider>();
        _currentUserProvider.UserId.Returns(_userId);

        var options = new DbContextOptionsBuilder<FinancesDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _dbContext = new FinancesDbContext(options, _currentUserProvider);
        _handler = new DeleteGoalCommandHandler(_dbContext, NullLogger<DeleteGoalCommandHandler>.Instance);
    }

    [Fact]
    public async Task Handle_ShouldDeleteGoal()
    {
        var account = Account.Create(_userId, "Savings", "USD", 1000m);
        _dbContext.Accounts.Add(account);
        var goal = Goal.Create(_userId, account.Id, 5000m, "USD", null);
        _dbContext.Goals.Add(goal);
        await _dbContext.SaveChangesAsync();

        var result = await _handler.Handle(new DeleteGoalCommand(goal.Id), CancellationToken.None);

        result.IsError.Should().BeFalse();
        var exists = await _dbContext.Goals.AnyAsync(g => g.Id == goal.Id);
        exists.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_ShouldReturnNotFound_WhenGoalDoesNotExist()
    {
        var result = await _handler.Handle(new DeleteGoalCommand(Guid.NewGuid()), CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Type.Should().Be(ErrorOr.ErrorType.NotFound);
        result.FirstError.Code.Should().Be("Goal.NotFound");
    }
}
