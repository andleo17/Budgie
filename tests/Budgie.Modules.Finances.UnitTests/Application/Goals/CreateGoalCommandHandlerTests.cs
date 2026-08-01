using Budgie.Modules.Finances.Application.Goals.CreateGoal;
using Budgie.Modules.Finances.Domain.Accounts;
using Budgie.Modules.Finances.Infrastructure.Database;
using Budgie.Shared.Abstractions;
using Budgie.Shared.Domain.ValueObjects;
using Budgie.Shared.Enums;
using ErrorOr;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace Budgie.Modules.Finances.UnitTests.Application.Goals;

public class CreateGoalCommandHandlerTests
{
    private readonly FinancesDbContext _dbContext;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly CreateGoalCommandHandler _handler;
    private readonly Guid _userId = Guid.NewGuid();

    public CreateGoalCommandHandlerTests()
    {
        _currentUserProvider = Substitute.For<ICurrentUserProvider>();
        _currentUserProvider.GetUserId().Returns(_userId);
        _currentUserProvider.UserId.Returns(_userId);

        var options = new DbContextOptionsBuilder<FinancesDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _dbContext = new FinancesDbContext(options, _currentUserProvider);
        _handler = new CreateGoalCommandHandler(_dbContext, _currentUserProvider, NullLogger<CreateGoalCommandHandler>.Instance);
    }

    [Fact]
    public async Task Handle_ShouldCreateGoal()
    {
        var account = Account.Create(_userId, "Savings", "USD", 1000m);
        _dbContext.Accounts.Add(account);
        await _dbContext.SaveChangesAsync();

        var command = new CreateGoalCommand(account.Id, 5000m, DateTime.UtcNow.AddMonths(6));
        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsError.Should().BeFalse();
        var goal = await _dbContext.Goals.FirstAsync();
        goal.AccountId.Should().Be(account.Id);
        goal.TargetAmount.Amount.Should().Be(5000m);
        goal.TargetAmount.Currency.Code.Should().Be("USD");
    }

    [Fact]
    public async Task Handle_AccountNotFound_ReturnsError()
    {
        var command = new CreateGoalCommand(Guid.NewGuid(), 5000m, null);
        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("Account.NotFound");
    }

    [Fact]
    public async Task Handle_ZeroTarget_ReturnsError()
    {
        var account = Account.Create(_userId, "Savings", "USD", 1000m);
        _dbContext.Accounts.Add(account);
        await _dbContext.SaveChangesAsync();

        var command = new CreateGoalCommand(account.Id, 0m, null);

        await Assert.ThrowsAsync<ArgumentException>(() => _handler.Handle(command, CancellationToken.None));
    }

    [Fact]
    public async Task Handle_NegativeTarget_ReturnsError()
    {
        var account = Account.Create(_userId, "Savings", "USD", 1000m);
        _dbContext.Accounts.Add(account);
        await _dbContext.SaveChangesAsync();

        var command = new CreateGoalCommand(account.Id, -100m, null);

        await Assert.ThrowsAsync<ArgumentException>(() => _handler.Handle(command, CancellationToken.None));
    }
}
