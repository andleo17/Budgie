using Budgie.Modules.Finances.Application.RecurringTransactions.ApplyAllDueRecurringTransactions;
using Budgie.Modules.Finances.Domain.Accounts;
using Budgie.Modules.Finances.Domain.RecurringTransactions;
using Budgie.Modules.Finances.Infrastructure.Database;
using Budgie.Shared.Abstractions;
using Budgie.Shared.Enums;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Xunit;

namespace Budgie.Modules.Finances.UnitTests.Application.RecurringTransactions.ApplyAllDueRecurringTransactions;

public class ApplyAllDueRecurringTransactionsCommandHandlerTests
{
    private readonly FinancesDbContext _dbContext;
    private readonly ApplyAllDueRecurringTransactionsCommandHandler _handler;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly Guid _userId = Guid.NewGuid();

    public ApplyAllDueRecurringTransactionsCommandHandlerTests()
    {
        _currentUserProvider = Substitute.For<ICurrentUserProvider>();
        _currentUserProvider.UserId.Returns(_userId);
        _currentUserProvider.GetUserId().Returns(_userId);

        var options = new DbContextOptionsBuilder<FinancesDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _dbContext = new FinancesDbContext(options, _currentUserProvider);
        _handler = new ApplyAllDueRecurringTransactionsCommandHandler(_dbContext, _currentUserProvider);
    }

    [Fact]
    public async Task Handle_ShouldApplyAllDueRecurringTransactions()
    {
        var account = Account.Create(_userId, "Checking", "USD", 2000m);
        _dbContext.Accounts.Add(account);
        var due1 = RecurringTransaction.Create(
            _userId, "Rent", account.Id, null, 500m, "USD", 1m,
            TransactionType.Expense, null, 1, true,
            RecurringApplicability.Automatic, RecurringFrequency.Monthly,
            null, RecurringEndType.OnDate, null, DateTime.UtcNow.AddYears(1),
            DateTime.UtcNow.AddDays(-5));
        var due2 = RecurringTransaction.Create(
            _userId, "Subscription", account.Id, null, 50m, "USD", 1m,
            TransactionType.Expense, null, 1, true,
            RecurringApplicability.Automatic, RecurringFrequency.Monthly,
            null, RecurringEndType.OnDate, null, DateTime.UtcNow.AddYears(1),
            DateTime.UtcNow.AddDays(-3));
        _dbContext.RecurringTransactions.AddRange(due1, due2);
        await _dbContext.SaveChangesAsync();

        var result = await _handler.Handle(new ApplyAllDueRecurringTransactionsCommand(), CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.Should().HaveCount(2);

        // Balance updated: 2000 - 500 - 50 = 1450
        await _dbContext.Entry(account).ReloadAsync();
        account.Balance.Amount.Should().Be(1450m);

        var transactions = await _dbContext.Transactions.ToListAsync();
        transactions.Should().HaveCount(2);

        var updatedRts = await _dbContext.RecurringTransactions.ToListAsync();
        updatedRts.Should().AllSatisfy(rt => rt.TimesApplied.Should().Be(1));
    }

    [Fact]
    public async Task Handle_ShouldReturnEmpty_WhenNoDue()
    {
        var result = await _handler.Handle(new ApplyAllDueRecurringTransactionsCommand(), CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_ShouldSkipManualRecurringTransactions()
    {
        var account = Account.Create(_userId, "Checking", "USD", 1000m);
        _dbContext.Accounts.Add(account);
        var manual = RecurringTransaction.Create(
            _userId, "Manual", account.Id, null, 100m, "USD", 1m,
            TransactionType.Expense, null, 1, true,
            RecurringApplicability.Manual, RecurringFrequency.Monthly,
            null, RecurringEndType.OnDate, null, DateTime.UtcNow.AddYears(1),
            DateTime.UtcNow.AddDays(-5));
        _dbContext.RecurringTransactions.Add(manual);
        await _dbContext.SaveChangesAsync();

        var result = await _handler.Handle(new ApplyAllDueRecurringTransactionsCommand(), CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.Should().BeEmpty();
    }
}
