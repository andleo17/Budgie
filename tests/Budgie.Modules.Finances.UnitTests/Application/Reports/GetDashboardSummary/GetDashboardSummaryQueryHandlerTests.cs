using Budgie.Modules.Finances.Application.Reports.GetDashboardSummary;
using Budgie.Modules.Finances.Domain.Accounts;
using Budgie.Modules.Finances.Domain.Transactions;
using Budgie.Modules.Finances.Infrastructure.Database;
using Budgie.Shared.Abstractions;
using Budgie.Shared.Enums;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Xunit;

namespace Budgie.Modules.Finances.UnitTests.Application.Reports.GetDashboardSummary;

public class GetDashboardSummaryQueryHandlerTests : IDisposable
{
    private readonly FinancesDbContext _dbContext;
    private readonly GetDashboardSummaryQueryHandler _handler;
    private readonly ICurrentUserProvider _currentUserProvider = Substitute.For<ICurrentUserProvider>();
    private readonly Guid _userId = Guid.NewGuid();

    public GetDashboardSummaryQueryHandlerTests()
    {
        _currentUserProvider.UserId.Returns(_userId);
        _currentUserProvider.GetUserId().Returns(_userId);

        var options = new DbContextOptionsBuilder<FinancesDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _dbContext = new FinancesDbContext(options, _currentUserProvider);
        _handler = new GetDashboardSummaryQueryHandler(_dbContext);
    }

    [Fact]
    public async Task Handle_ShouldReturnZeroBalances_WhenNoAccountsExist()
    {
        var result = await _handler.Handle(new GetDashboardSummaryQuery("USD"), CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.TotalBalance.Should().Be(0);
        result.Value.MonthlyIncome.Should().Be(0);
        result.Value.MonthlyExpenses.Should().Be(0);
        result.Value.NetFlow.Should().Be(0);
    }

    [Fact]
    public async Task Handle_ShouldCalculateTotalBalance_FromAllAccounts()
    {
        _dbContext.Accounts.AddRange(
            Account.Create(_userId, "Wallet", "USD", 500m),
            Account.Create(_userId, "Savings", "USD", 1000m));
        await _dbContext.SaveChangesAsync();

        var result = await _handler.Handle(new GetDashboardSummaryQuery("USD"), CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.TotalBalance.Should().Be(1500m);
    }

    [Fact]
    public async Task Handle_ShouldCalculateMonthlyIncomeAndExpenses_WithUtcDates()
    {
        var account = Account.Create(_userId, "Bank", "USD", 2000m);
        _dbContext.Accounts.Add(account);
        await _dbContext.SaveChangesAsync();

        var now = DateTime.UtcNow;
        var firstDayOfMonth = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);

        _dbContext.Transactions.AddRange(
            Transaction.Create(_userId, account.Id, null, 1000m, "USD", 1m,
                firstDayOfMonth.AddDays(2), "Salary", TransactionType.Income),
            Transaction.Create(_userId, account.Id, null, 300m, "USD", 1m,
                firstDayOfMonth.AddDays(5), "Groceries", TransactionType.Expense),
            Transaction.Create(_userId, account.Id, null, 200m, "USD", 1m,
                firstDayOfMonth.AddDays(10), "Dinner", TransactionType.Expense),
            Transaction.Create(_userId, account.Id, null, 150m, "USD", 1m,
                firstDayOfMonth.AddMonths(-1), "Last month expense", TransactionType.Expense));
        await _dbContext.SaveChangesAsync();

        var result = await _handler.Handle(new GetDashboardSummaryQuery("USD"), CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.MonthlyIncome.Should().Be(1000m);
        result.Value.MonthlyExpenses.Should().Be(500m);
        result.Value.NetFlow.Should().Be(500m);
    }

    [Fact]
    public async Task Handle_ShouldIncludeRepaymentAsIncome()
    {
        var account = Account.Create(_userId, "Bank", "USD", 1000m);
        _dbContext.Accounts.Add(account);
        await _dbContext.SaveChangesAsync();

        var now = DateTime.UtcNow;
        var firstDayOfMonth = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);

        _dbContext.Transactions.Add(
            Transaction.Create(_userId, account.Id, null, 500m, "USD", 1m,
                firstDayOfMonth.AddDays(1), "Loan repayment", TransactionType.Repayment));
        await _dbContext.SaveChangesAsync();

        var result = await _handler.Handle(new GetDashboardSummaryQuery("USD"), CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.MonthlyIncome.Should().Be(500m);
    }

    [Fact]
    public async Task Handle_ShouldIncludeLoanAsExpense()
    {
        var account = Account.Create(_userId, "Bank", "USD", 1000m);
        _dbContext.Accounts.Add(account);
        await _dbContext.SaveChangesAsync();

        var now = DateTime.UtcNow;
        var firstDayOfMonth = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);

        _dbContext.Transactions.Add(
            Transaction.Create(_userId, account.Id, null, 300m, "USD", 1m,
                firstDayOfMonth.AddDays(1), "Loan given", TransactionType.Loan));
        await _dbContext.SaveChangesAsync();

        var result = await _handler.Handle(new GetDashboardSummaryQuery("USD"), CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.MonthlyExpenses.Should().Be(300m);
    }

    [Fact]
    public async Task Handle_ShouldExcludeTransactionsBeforeFirstDayOfMonth()
    {
        var account = Account.Create(_userId, "Bank", "USD", 1000m);
        _dbContext.Accounts.Add(account);
        await _dbContext.SaveChangesAsync();

        var now = DateTime.UtcNow;
        var firstDayOfMonth = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);

        _dbContext.Transactions.AddRange(
            Transaction.Create(_userId, account.Id, null, 100m, "USD", 1m,
                firstDayOfMonth, "First day", TransactionType.Expense),
            Transaction.Create(_userId, account.Id, null, 300m, "USD", 1m,
                firstDayOfMonth.AddDays(-1), "Last day prev month", TransactionType.Income));
        await _dbContext.SaveChangesAsync();

        var result = await _handler.Handle(new GetDashboardSummaryQuery("USD"), CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.MonthlyExpenses.Should().Be(100m);
        result.Value.MonthlyIncome.Should().Be(0m);
    }

    public void Dispose() => _dbContext.Dispose();
}
