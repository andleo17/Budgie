using Budgie.Modules.Finances.Application.RecurringTransactions.GetDueRecurringTransactions;
using Budgie.Modules.Finances.Domain.Accounts;
using Budgie.Modules.Finances.Domain.RecurringTransactions;
using Budgie.Modules.Finances.Infrastructure.Database;
using Budgie.Shared.Abstractions;
using Budgie.Shared.Enums;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Xunit;

namespace Budgie.Modules.Finances.UnitTests.Application.RecurringTransactions.GetDueRecurringTransactions;

public class GetDueRecurringTransactionsQueryHandlerTests
{
    private readonly FinancesDbContext _dbContext;
    private readonly GetDueRecurringTransactionsQueryHandler _handler;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly Guid _userId = Guid.NewGuid();

    public GetDueRecurringTransactionsQueryHandlerTests()
    {
        _currentUserProvider = Substitute.For<ICurrentUserProvider>();
        _currentUserProvider.UserId.Returns(_userId);
        _currentUserProvider.GetUserId().Returns(_userId);

        var options = new DbContextOptionsBuilder<FinancesDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _dbContext = new FinancesDbContext(options, _currentUserProvider);
        _handler = new GetDueRecurringTransactionsQueryHandler(_dbContext, _currentUserProvider);
    }

    [Fact]
    public async Task Handle_ShouldReturnDueRecurringTransactions()
    {
        var account = Account.Create(_userId, "Checking", "USD", 1000m);
        _dbContext.Accounts.Add(account);
        var pastDue = RecurringTransaction.Create(
            _userId, "Rent", account.Id, null, 1200m, "USD", 1m,
            TransactionType.Expense, null, 1, true,
            RecurringApplicability.Automatic, RecurringFrequency.Monthly,
            null, RecurringEndType.OnDate, null, DateTime.UtcNow.AddYears(1),
            DateTime.UtcNow.AddDays(-5));
        var future = RecurringTransaction.Create(
            _userId, "Future Sub", account.Id, null, 10m, "USD", 1m,
            TransactionType.Expense, null, 1, true,
            RecurringApplicability.Automatic, RecurringFrequency.Monthly,
            null, RecurringEndType.OnDate, null, DateTime.UtcNow.AddYears(1),
            DateTime.UtcNow.AddDays(5));
        _dbContext.RecurringTransactions.AddRange(pastDue, future);
        await _dbContext.SaveChangesAsync();

        var result = await _handler.Handle(new GetDueRecurringTransactionsQuery(), CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.Should().HaveCount(1);
        result.Value[0].Name.Should().Be("Rent");
    }

    [Fact]
    public async Task Handle_ShouldExcludeInactiveRecurringTransactions()
    {
        var account = Account.Create(_userId, "Checking", "USD", 1000m);
        _dbContext.Accounts.Add(account);
        var inactive = RecurringTransaction.Create(
            _userId, "Old", account.Id, null, 100m, "USD", 1m,
            TransactionType.Expense, null, 1, true,
            RecurringApplicability.Automatic, RecurringFrequency.Monthly,
            null, RecurringEndType.OnDate, null, DateTime.UtcNow.AddYears(1),
            DateTime.UtcNow.AddDays(-10));
        _dbContext.RecurringTransactions.Add(inactive);
        await _dbContext.SaveChangesAsync();

        // Deactivate it
        var rt = await _dbContext.RecurringTransactions.FirstAsync();
        typeof(RecurringTransaction).GetProperty("IsActive")!.SetValue(rt, false);
        await _dbContext.SaveChangesAsync();

        var result = await _handler.Handle(new GetDueRecurringTransactionsQuery(), CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_ShouldReturnEmpty_WhenNoDueTransactions()
    {
        var result = await _handler.Handle(new GetDueRecurringTransactionsQuery(), CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.Should().BeEmpty();
    }
}
