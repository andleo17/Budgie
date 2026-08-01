using Budgie.Modules.Finances.Domain.Goals;
using Budgie.Shared.Domain.ValueObjects;
using FluentAssertions;
using Xunit;

namespace Budgie.Modules.Finances.UnitTests.Domain.Goals;

public class GoalTests
{
    [Fact]
    public void Create_ShouldCreateWithPositiveTargetAmount()
    {
        var userId = Guid.NewGuid();
        var accountId = Guid.NewGuid();
        var targetDate = new DateTime(2025, 12, 31, 0, 0, 0, DateTimeKind.Utc);

        var goal = Goal.Create(userId, accountId, 5000m, "USD", targetDate);

        goal.Id.Should().NotBeEmpty();
        goal.UserId.Should().Be(userId);
        goal.AccountId.Should().Be(accountId);
        goal.TargetAmount.Amount.Should().Be(5000m);
        goal.TargetAmount.Currency.Code.Should().Be("USD");
        goal.TargetDate.Should().NotBeNull();
    }

    [Fact]
    public void Create_ShouldThrow_WhenTargetAmountIsZero()
    {
        var act = () => Goal.Create(Guid.NewGuid(), Guid.NewGuid(), 0m, "USD", null);
        act.Should().Throw<ArgumentException>().WithMessage("Amount must be positive.*");
    }

    [Fact]
    public void Create_ShouldThrow_WhenTargetAmountIsNegative()
    {
        var act = () => Goal.Create(Guid.NewGuid(), Guid.NewGuid(), -100m, "USD", null);
        act.Should().Throw<ArgumentException>().WithMessage("Amount must be positive.*");
    }

    [Fact]
    public void Create_ShouldNormalizeTargetDate()
    {
        var targetDate = DateTime.UtcNow;
        var goal = Goal.Create(Guid.NewGuid(), Guid.NewGuid(), 5000m, "USD", targetDate);
        goal.TargetDate!.Value.Kind.Should().Be(DateTimeKind.Unspecified);
    }

    [Fact]
    public void Create_ShouldNormalizeCreatedAt()
    {
        var goal = Goal.Create(Guid.NewGuid(), Guid.NewGuid(), 5000m, "USD", null);
        goal.CreatedAt.Kind.Should().Be(DateTimeKind.Unspecified);
    }

    [Fact]
    public void Create_ShouldAllowNullTargetDate()
    {
        var goal = Goal.Create(Guid.NewGuid(), Guid.NewGuid(), 5000m, "USD", null);
        goal.TargetDate.Should().BeNull();
    }

    [Fact]
    public void Update_ShouldUpdateTargetAmountAndDate()
    {
        var goal = Goal.Create(Guid.NewGuid(), Guid.NewGuid(), 5000m, "USD", new DateTime(2025, 12, 31, 0, 0, 0, DateTimeKind.Utc));
        var newDate = new DateTime(2026, 6, 30, 0, 0, 0, DateTimeKind.Utc);

        goal.Update(10000m, "EUR", newDate);

        goal.TargetAmount.Amount.Should().Be(10000m);
        goal.TargetAmount.Currency.Code.Should().Be("EUR");
        goal.TargetDate.Should().Be(newDate);
    }

    [Fact]
    public void Update_ShouldAllowNullTargetDate()
    {
        var goal = Goal.Create(Guid.NewGuid(), Guid.NewGuid(), 5000m, "USD", new DateTime(2025, 12, 31, 0, 0, 0, DateTimeKind.Utc));

        goal.Update(5000m, "USD", null);

        goal.TargetDate.Should().BeNull();
    }

    [Fact]
    public void Update_ShouldThrow_WhenTargetAmountIsZero()
    {
        var goal = Goal.Create(Guid.NewGuid(), Guid.NewGuid(), 5000m, "USD", null);
        var act = () => goal.Update(0m, "USD", null);
        act.Should().Throw<ArgumentException>().WithMessage("Amount must be positive.*");
    }
}
