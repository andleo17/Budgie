using Budgie.Modules.Finances.Application.Reports;
using Budgie.Modules.Finances.Application.Reports.GetCategorySpending;
using Budgie.Modules.Finances.Domain.Accounts;
using Budgie.Modules.Finances.Domain.Categories;
using Budgie.Modules.Finances.Domain.Transactions;
using Budgie.Modules.Finances.Infrastructure.Database;
using Budgie.Shared.Abstractions;
using Budgie.Shared.Enums;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Xunit;

namespace Budgie.Modules.Finances.UnitTests.Application.Reports.GetCategorySpending;

public class GetCategorySpendingQueryHandlerTests
{
    private readonly FinancesDbContext _dbContext;
    private readonly GetCategorySpendingQueryHandler _handler;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly Guid _userId = Guid.NewGuid();

    public GetCategorySpendingQueryHandlerTests()
    {
        _currentUserProvider = Substitute.For<ICurrentUserProvider>();
        _currentUserProvider.UserId.Returns(_userId);

        var options = new DbContextOptionsBuilder<FinancesDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _dbContext = new FinancesDbContext(options, _currentUserProvider);
        _handler = new GetCategorySpendingQueryHandler(_dbContext);
    }

    [Fact]
    public async Task Handle_ShouldReturnCategorySpendingBreakdown()
    {
        var account = Account.Create(_userId, "Bank", "USD", 2000m);
        _dbContext.Accounts.Add(account);
        var food = Category.Create(_userId, "Food", 1, "#FF0000", "pizza");
        var transport = Category.Create(_userId, "Transport", 1, "#00FF00", "car");
        _dbContext.Categories.AddRange(food, transport);
        var now = DateTime.UtcNow;
        var start = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        _dbContext.Transactions.AddRange(
            Transaction.Create(_userId, account.Id, food.Id, 100m, "USD", 1m, start.AddDays(2), "Groceries", TransactionType.Expense),
            Transaction.Create(_userId, account.Id, food.Id, 50m, "USD", 1m, start.AddDays(5), "Lunch", TransactionType.Expense),
            Transaction.Create(_userId, account.Id, transport.Id, 30m, "USD", 1m, start.AddDays(3), "Bus", TransactionType.Expense));
        await _dbContext.SaveChangesAsync();

        var result = await _handler.Handle(
            new GetCategorySpendingQuery(start, start.AddMonths(1)), CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.Should().HaveCount(2);
        result.Value[0].CategoryName.Should().Be("Food");
        result.Value[0].TotalAmount.Should().Be(150m);
        result.Value[1].CategoryName.Should().Be("Transport");
        result.Value[1].TotalAmount.Should().Be(30m);
    }

    [Fact]
    public async Task Handle_ShouldExcludeOtherTransactionTypes()
    {
        var account = Account.Create(_userId, "Bank", "USD", 2000m);
        _dbContext.Accounts.Add(account);
        var now = DateTime.UtcNow;
        var start = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        _dbContext.Transactions.AddRange(
            Transaction.Create(_userId, account.Id, null, 1000m, "USD", 1m, start.AddDays(1), "Salary", TransactionType.Income),
            Transaction.Create(_userId, account.Id, null, 200m, "USD", 1m, start.AddDays(2), "Shopping", TransactionType.Expense));
        await _dbContext.SaveChangesAsync();

        var result = await _handler.Handle(
            new GetCategorySpendingQuery(start, start.AddMonths(1)), CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.Should().HaveCount(1);
        result.Value[0].TotalAmount.Should().Be(200m);
    }
}
