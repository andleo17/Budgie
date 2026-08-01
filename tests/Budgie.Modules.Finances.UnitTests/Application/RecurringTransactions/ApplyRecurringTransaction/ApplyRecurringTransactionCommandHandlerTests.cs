using Budgie.Modules.Finances.Application.RecurringTransactions.ApplyRecurringTransaction;
using Budgie.Modules.Finances.Domain.Accounts;
using Budgie.Modules.Finances.Domain.RecurringTransactions;
using Budgie.Modules.Finances.Infrastructure.Database;
using Budgie.Shared.Abstractions;
using Budgie.Shared.Enums;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Xunit;

namespace Budgie.Modules.Finances.UnitTests.Application.RecurringTransactions.ApplyRecurringTransaction;

public class ApplyRecurringTransactionCommandHandlerTests
{
    private readonly FinancesDbContext _dbContext;
    private readonly ApplyRecurringTransactionCommandHandler _handler;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly Guid _userId = Guid.NewGuid();

    public ApplyRecurringTransactionCommandHandlerTests()
    {
        _currentUserProvider = Substitute.For<ICurrentUserProvider>();
        _currentUserProvider.UserId.Returns(_userId);
        _currentUserProvider.GetUserId().Returns(_userId);

        var options = new DbContextOptionsBuilder<FinancesDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _dbContext = new FinancesDbContext(options, _currentUserProvider);
        _handler = new ApplyRecurringTransactionCommandHandler(_dbContext, _currentUserProvider);
    }

    [Fact]
    public async Task Handle_ShouldApplyRecurringTransaction()
    {
        var account = Account.Create(_userId, "Checking", "USD", 1000m);
        _dbContext.Accounts.Add(account);
        var rt = RecurringTransaction.Create(
            _userId, "Rent", account.Id, null, 200m, "USD", 1m,
            TransactionType.Expense, null, 1, true,
            RecurringApplicability.Automatic, RecurringFrequency.Monthly,
            null, RecurringEndType.OnDate, null, DateTime.UtcNow.AddYears(1),
            DateTime.UtcNow);
        _dbContext.RecurringTransactions.Add(rt);
        await _dbContext.SaveChangesAsync();

        var result = await _handler.Handle(new ApplyRecurringTransactionCommand(rt.Id), CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.Should().NotBeEmpty();

        var transaction = await _dbContext.Transactions.FirstAsync();
        transaction.AccountId.Should().Be(account.Id);
        transaction.Amount.Amount.Should().Be(200m);

        // Balance updated
        account.Balance.Amount.Should().Be(800m);

        // Recurring transaction updated
        var updatedRt = await _dbContext.RecurringTransactions.FirstAsync();
        updatedRt.TimesApplied.Should().Be(1);
        updatedRt.LastAppliedDate.Should().NotBeNull();
    }

    [Fact]
    public async Task Handle_ShouldReturnError_WhenRecurringTransactionNotFound()
    {
        var result = await _handler.Handle(new ApplyRecurringTransactionCommand(Guid.NewGuid()), CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Type.Should().Be(ErrorOr.ErrorType.NotFound);
        result.FirstError.Code.Should().Be("RecurringTransaction.NotFound");
    }
}
