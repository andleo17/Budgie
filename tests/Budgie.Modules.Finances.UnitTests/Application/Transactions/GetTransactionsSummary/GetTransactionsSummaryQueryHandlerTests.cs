using Budgie.Modules.Finances.Application.Transactions.GetTransactionsSummary;
using Budgie.Modules.Finances.Domain.Accounts;
using Budgie.Modules.Finances.Domain.Transactions;
using Budgie.Modules.Finances.Infrastructure.Database;
using Budgie.Shared.Abstractions;
using Budgie.Shared.Enums;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Xunit;

namespace Budgie.Modules.Finances.UnitTests.Application.Transactions.GetTransactionsSummary;

public class GetTransactionsSummaryQueryHandlerTests
{
    private readonly FinancesDbContext _dbContext;
    private readonly GetTransactionsSummaryQueryHandler _handler;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly Guid _userId = Guid.NewGuid();

    public GetTransactionsSummaryQueryHandlerTests()
    {
        _currentUserProvider = Substitute.For<ICurrentUserProvider>();
        _currentUserProvider.UserId.Returns(_userId);

        var options = new DbContextOptionsBuilder<FinancesDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _dbContext = new FinancesDbContext(options, _currentUserProvider);
        _handler = new GetTransactionsSummaryQueryHandler(_dbContext);
    }

    [Fact]
    public async Task Handle_ShouldReturnSummaryTotals()
    {
        var account = Account.Create(_userId, "Bank", "USD", 2000m);
        _dbContext.Accounts.Add(account);
        var now = DateTime.UtcNow;
        var start = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        _dbContext.Transactions.AddRange(
            Transaction.Create(_userId, account.Id, null, 1000m, "USD", 1m, start.AddDays(2), "Salary", TransactionType.Income),
            Transaction.Create(_userId, account.Id, null, 300m, "USD", 1m, start.AddDays(5), "Groceries", TransactionType.Expense),
            Transaction.Create(_userId, account.Id, null, 200m, "USD", 1m, start.AddDays(10), "Dinner", TransactionType.Expense));
        await _dbContext.SaveChangesAsync();

        var result = await _handler.Handle(
            new GetTransactionsSummaryQuery(start, start.AddMonths(1)), CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.MainCurrency.Should().Be("USD");
        result.Value.Currencies.Should().HaveCount(1);
        result.Value.Currencies[0].Income.Should().Be(1000m);
        result.Value.Currencies[0].Expenses.Should().Be(500m);
    }

    [Fact]
    public async Task Handle_ShouldRespectAccountFilter()
    {
        var account1 = Account.Create(_userId, "Bank1", "USD", 2000m);
        var account2 = Account.Create(_userId, "Bank2", "USD", 2000m);
        _dbContext.Accounts.AddRange(account1, account2);
        var now = DateTime.UtcNow;
        var start = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        _dbContext.Transactions.AddRange(
            Transaction.Create(_userId, account1.Id, null, 500m, "USD", 1m, start.AddDays(1), "Inc1", TransactionType.Income),
            Transaction.Create(_userId, account2.Id, null, 300m, "USD", 1m, start.AddDays(1), "Inc2", TransactionType.Income));
        await _dbContext.SaveChangesAsync();

        var result = await _handler.Handle(
            new GetTransactionsSummaryQuery(start, start.AddMonths(1), new[] { account1.Id }), CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.Currencies[0].Income.Should().Be(500m);
    }

    [Fact]
    public async Task Handle_ShouldIncludeRepaymentAsIncome()
    {
        var account = Account.Create(_userId, "Bank", "USD", 1000m);
        _dbContext.Accounts.Add(account);
        var now = DateTime.UtcNow;
        var start = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        _dbContext.Transactions.Add(
            Transaction.Create(_userId, account.Id, null, 500m, "USD", 1m, start.AddDays(1), "Repayment", TransactionType.Repayment));
        await _dbContext.SaveChangesAsync();

        var result = await _handler.Handle(
            new GetTransactionsSummaryQuery(start, start.AddMonths(1)), CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.Currencies[0].Income.Should().Be(500m);
    }
}
