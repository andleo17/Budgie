using Budgie.Modules.Finances.Application.Transactions.CreateTransaction;
using Budgie.Modules.Finances.Application.Transactions.UpdateTransaction;
using Budgie.Modules.Finances.Domain.Accounts;
using Budgie.Modules.Finances.Domain.Categories;
using Budgie.Modules.Finances.Domain.Transactions;
using Budgie.Modules.Finances.Infrastructure.Database;
using Budgie.Shared.Abstractions;
using Budgie.Shared.Domain.ValueObjects;
using Budgie.Shared.Enums;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace Budgie.Modules.Finances.UnitTests.Application.Transactions.UpdateTransaction;

public class UpdateTransactionCommandHandlerTests : IDisposable
{
    private readonly FinancesDbContext _dbContext;
    private readonly UpdateTransactionCommandHandler _handler;
    private readonly ICurrentUserProvider _currentUserProvider = Substitute.For<ICurrentUserProvider>();
    private readonly Guid _userId = Guid.NewGuid();

    public UpdateTransactionCommandHandlerTests()
    {
        _currentUserProvider.UserId.Returns(_userId);
        _currentUserProvider.GetUserId().Returns(_userId);

        var options = new DbContextOptionsBuilder<FinancesDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _dbContext = new FinancesDbContext(options, _currentUserProvider);
        _handler = new UpdateTransactionCommandHandler(_dbContext, NullLogger<UpdateTransactionCommandHandler>.Instance);
    }

    [Fact]
    public async Task Handle_ShouldUpdateFieldsAndBalance()
    {
        var account = Account.Create(_userId, "Bank", "USD", 1000m);
        _dbContext.Accounts.Add(account);
        var tx = Transaction.Create(_userId, account.Id, null, 200m, "USD", 1m, DateTime.UtcNow, "Old", TransactionType.Expense);
        account.ApplyTransaction(Money.FromPositive(200m, CurrencyCode.From("USD")), 1m, 1, TransactionType.Expense);
        _dbContext.Transactions.Add(tx);
        await _dbContext.SaveChangesAsync();

        var command = new UpdateTransactionCommand(tx.Id, null, 150m, "USD", 1m, DateTime.UtcNow, "Updated", TransactionType.Expense, 1);
        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsError.Should().BeFalse();
        var updated = await _dbContext.Transactions.FirstAsync(t => t.Id == tx.Id);
        updated.Amount.Amount.Should().Be(150m);
        updated.Notes.Should().Be("Updated");
        var reloadedAccount = await _dbContext.Accounts.FindAsync(account.Id);
        reloadedAccount!.Balance.Amount.Should().Be(850m);
    }

    [Fact]
    public async Task Handle_ShouldReturnNotFound_WhenTransactionDoesNotExist()
    {
        var command = new UpdateTransactionCommand(Guid.NewGuid(), null, 100m, "USD", 1m, DateTime.UtcNow, "Test", TransactionType.Expense, 1);
        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Type.Should().Be(ErrorOr.ErrorType.NotFound);
    }

    [Fact]
    public async Task Handle_ShouldReturnError_WhenDateBeforeAccountCreation()
    {
        var account = Account.Create(_userId, "Bank", "USD", 1000m);
        _dbContext.Accounts.Add(account);
        var tx = Transaction.Create(_userId, account.Id, null, 100m, "USD", 1m, DateTime.UtcNow, "Test", TransactionType.Expense);
        account.ApplyTransaction(Money.FromPositive(100m, CurrencyCode.From("USD")), 1m, 1, TransactionType.Expense);
        _dbContext.Transactions.Add(tx);
        await _dbContext.SaveChangesAsync();

        var pastDate = account.CreatedAt.AddDays(-1);
        var command = new UpdateTransactionCommand(tx.Id, null, 100m, "USD", 1m, pastDate, "Test", TransactionType.Expense, 1);
        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("Transaction.DateBeforeAccountCreation");
    }

    [Fact]
    public async Task Handle_ShouldReturnError_WhenInitialBalanceDateLocked()
    {
        var account = Account.Create(_userId, "Bank", "USD", 1000m);
        _dbContext.Accounts.Add(account);
        var tx = Transaction.Create(_userId, account.Id, null, 1000m, "USD", 1m, DateTime.UtcNow, "Initial", TransactionType.Income, isInitialBalance: true);
        account.ApplyTransaction(Money.FromPositive(1000m, CurrencyCode.From("USD")), 1m, 1, TransactionType.Income);
        _dbContext.Transactions.Add(tx);
        await _dbContext.SaveChangesAsync();

        var newDate = DateTime.UtcNow.AddDays(1);
        var command = new UpdateTransactionCommand(tx.Id, null, 1000m, "USD", 1m, newDate, "Initial", TransactionType.Income, 1);
        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("Transaction.InitialBalanceDateLocked");
    }

    [Fact]
    public async Task Handle_WithParticipants_ShouldRecreateChildren()
    {
        var account = Account.Create(_userId, "Bank", "USD", 1000m);
        _dbContext.Accounts.Add(account);
        var personId = Guid.NewGuid();

        var tx = Transaction.Create(_userId, account.Id, null, 100m, "USD", 1m, DateTime.UtcNow, "Shared", TransactionType.Expense);
        tx.CreateParticipant(personId, 50m, remainingAmount: 50m);
        account.ApplyTransaction(Money.FromPositive(100m, CurrencyCode.From("USD")), 1m, 1, TransactionType.Expense);
        _dbContext.Transactions.Add(tx);
        await _dbContext.SaveChangesAsync();

        var newPersonId = Guid.NewGuid();
        var command = new UpdateTransactionCommand(
            tx.Id, null, 100m, "USD", 1m, DateTime.UtcNow, "Shared updated", TransactionType.Expense, 1,
            [new TransactionParticipantDto(newPersonId, 30m, 30m, false, null)]);

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsError.Should().BeFalse();
        var oldChildren = await _dbContext.Transactions
            .Where(t => t.ParentTransactionId == tx.Id)
            .ToListAsync();
        oldChildren.Should().HaveCount(1);
        oldChildren[0].Amount.Amount.Should().Be(30m);
        oldChildren[0].Type.Should().Be(TransactionType.Loan);
    }

    [Fact]
    public async Task Handle_WithParticipantCashRepaid_ShouldCreateChildIncome()
    {
        var account = Account.Create(_userId, "Bank", "USD", 1000m);
        _dbContext.Accounts.Add(account);
        var personId = Guid.NewGuid();

        var tx = Transaction.Create(_userId, account.Id, null, 100m, "USD", 1m, DateTime.UtcNow, "Shared", TransactionType.Expense);
        account.ApplyTransaction(Money.FromPositive(100m, CurrencyCode.From("USD")), 1m, 1, TransactionType.Expense);
        _dbContext.Transactions.Add(tx);
        await _dbContext.SaveChangesAsync();

        var command = new UpdateTransactionCommand(
            tx.Id, null, 100m, "USD", 1m, DateTime.UtcNow, "Shared", TransactionType.Expense, 1,
            [new TransactionParticipantDto(personId, 50m, 0m, false, Guid.NewGuid())]);

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsError.Should().BeFalse();
        var children = await _dbContext.Transactions
            .Where(t => t.ParentTransactionId == tx.Id)
            .ToListAsync();
        children.Should().HaveCount(1);
        children[0].Type.Should().Be(TransactionType.Income);
    }

    [Fact]
    public async Task Handle_WithTrackingOnlyParticipant_ShouldNotCreateChild()
    {
        var account = Account.Create(_userId, "Bank", "USD", 1000m);
        _dbContext.Accounts.Add(account);
        var personId = Guid.NewGuid();

        var tx = Transaction.Create(_userId, account.Id, null, 100m, "USD", 1m, DateTime.UtcNow, "Tagged", TransactionType.Expense);
        account.ApplyTransaction(Money.FromPositive(100m, CurrencyCode.From("USD")), 1m, 1, TransactionType.Expense);
        _dbContext.Transactions.Add(tx);
        await _dbContext.SaveChangesAsync();

        var command = new UpdateTransactionCommand(
            tx.Id, null, 100m, "USD", 1m, DateTime.UtcNow, "Tagged", TransactionType.Expense, 1,
            [new TransactionParticipantDto(personId, 0m, 0m, true, null)]);

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsError.Should().BeFalse();
        var children = await _dbContext.Transactions
            .Where(t => t.ParentTransactionId == tx.Id)
            .ToListAsync();
        children.Should().BeEmpty();
    }

    public void Dispose()
    {
        _dbContext.Dispose();
    }
}
