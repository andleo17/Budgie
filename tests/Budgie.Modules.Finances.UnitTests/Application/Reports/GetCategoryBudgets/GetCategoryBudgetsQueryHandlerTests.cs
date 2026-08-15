using Budgie.Modules.Finances.Application;
using Budgie.Modules.Finances.Application.Reports.GetCategoryBudgets;
using Budgie.Shared.Abstractions;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace Budgie.Modules.Finances.UnitTests.Application.Reports.GetCategoryBudgets;

public class GetCategoryBudgetsQueryHandlerTests
{
    private readonly IReportRepository _reportRepository = Substitute.For<IReportRepository>();
    private readonly ICurrentUserProvider _currentUserProvider = Substitute.For<ICurrentUserProvider>();
    private readonly GetCategoryBudgetsQueryHandler _handler;

    public GetCategoryBudgetsQueryHandlerTests()
    {
        _handler = new GetCategoryBudgetsQueryHandler(_reportRepository, _currentUserProvider);
    }

    [Fact]
    public async Task Handle_BudgetBelow80Percent_ReturnsNormalStatus()
    {
        var userId = Guid.NewGuid();
        _currentUserProvider.GetUserId().Returns(userId);
        var categoryId = Guid.NewGuid();
        _reportRepository.GetCategoryBudgetsAsync(userId, Arg.Any<DateTime>(), Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns([new CategoryBudget(categoryId, "Food", null, null, 100m, 50m, 0m, 0, BudgetThresholdStatus.Normal)]);

        var result = await _handler.Handle(new GetCategoryBudgetsQuery(), CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.Single().Percentage.Should().Be(50);
        result.Value.Single().Remaining.Should().Be(50m);
        result.Value.Single().Status.Should().Be(BudgetThresholdStatus.Normal);
    }

    [Fact]
    public async Task Handle_BudgetBetween80And100_ReturnsWarningStatus()
    {
        var userId = Guid.NewGuid();
        _currentUserProvider.GetUserId().Returns(userId);
        var categoryId = Guid.NewGuid();
        _reportRepository.GetCategoryBudgetsAsync(userId, Arg.Any<DateTime>(), Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns([new CategoryBudget(categoryId, "Food", null, null, 100m, 85m, 0m, 0, BudgetThresholdStatus.Normal)]);

        var result = await _handler.Handle(new GetCategoryBudgetsQuery(), CancellationToken.None);

        result.Value.Single().Percentage.Should().Be(85);
        result.Value.Single().Status.Should().Be(BudgetThresholdStatus.Warning);
    }

    [Fact]
    public async Task Handle_BudgetAtOrAbove100_ReturnsExceededStatus()
    {
        var userId = Guid.NewGuid();
        _currentUserProvider.GetUserId().Returns(userId);
        var categoryId = Guid.NewGuid();
        _reportRepository.GetCategoryBudgetsAsync(userId, Arg.Any<DateTime>(), Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns([new CategoryBudget(categoryId, "Food", null, null, 100m, 120m, 0m, 0, BudgetThresholdStatus.Normal)]);

        var result = await _handler.Handle(new GetCategoryBudgetsQuery(), CancellationToken.None);

        result.Value.Single().Percentage.Should().Be(120);
        result.Value.Single().Remaining.Should().Be(-20m);
        result.Value.Single().Status.Should().Be(BudgetThresholdStatus.Exceeded);
    }
}
