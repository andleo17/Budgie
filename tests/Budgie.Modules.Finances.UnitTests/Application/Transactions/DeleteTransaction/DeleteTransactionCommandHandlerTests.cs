using Budgie.Modules.Finances.Application.Transactions.DeleteTransaction;
using Budgie.Modules.Finances.Domain.Accounts;
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

namespace Budgie.Modules.Finances.UnitTests.Application.Transactions.DeleteTransaction;

public class DeleteTransactionCommandHandlerTests
{
    private readonly FinancesDbContext _dbContext;
    private readonly DeleteTransactionCommandHandler _handler;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly Guid _userId = Guid.NewGuid();

    public DeleteTransactionCommandHandlerTests()
    {
        _currentUserProvider = Substitute.For<ICurrentUserProvider>();
        _currentUserProvider.UserId.Returns(_userId);

        var options = new DbContextOptionsBuilder<FinancesDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _dbContext = new FinancesDbContext(options, _currentUserProvider);
        _handler = new DeleteTransactionCommandHandler(_dbContext, NullLogger<DeleteTransactionCommandHandler>.Instance);
    }

    [Fact]
    public async Task Handle_ShouldDeleteTransactionAndRevertBalance()
    {
        var account = Account.Create(_userId, "Bank", "USD", 1000m);
        _dbContext.Accounts.Add(account);
        var tx = Transaction.Create(_userId, account.Id, null, 200m, "USD", 1m, DateTime.UtcNow, "Expense", TransactionType.Expense);
        account.ApplyTransaction(Money.FromPositive(200m, CurrencyCode.From("USD")), 1m, 1, TransactionType.Expense);
        _dbContext.Transactions.Add(tx);
        await _dbContext.SaveChangesAsync();

        var result = await _handler.Handle(new DeleteTransactionCommand(tx.Id), CancellationToken.None);

        result.IsError.Should().BeFalse();
        account.Balance.Amount.Should().Be(1000m);
        var exists = await _dbContext.Transactions.AnyAsync(t => t.Id == tx.Id);
        exists.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_ShouldDeleteIncomeAndRevertBalance()
    {
        var account = Account.Create(_userId, "Bank", "USD", 1000m);
        _dbContext.Accounts.Add(account);
        var tx = Transaction.Create(_userId, account.Id, null, 500m, "USD", 1m, DateTime.UtcNow, "Income", TransactionType.Income);
        account.ApplyTransaction(Money.FromPositive(500m, CurrencyCode.From("USD")), 1m, 1, TransactionType.Income);
        _dbContext.Transactions.Add(tx);
        await _dbContext.SaveChangesAsync();

        var result = await _handler.Handle(new DeleteTransactionCommand(tx.Id), CancellationToken.None);

        result.IsError.Should().BeFalse();
        account.Balance.Amount.Should().Be(1000m);
    }

    [Fact]
    public async Task Handle_ShouldReturnNotFound_WhenTransactionDoesNotExist()
    {
        var result = await _handler.Handle(new DeleteTransactionCommand(Guid.NewGuid()), CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Type.Should().Be(ErrorOr.ErrorType.NotFound);
        result.FirstError.Code.Should().Be("Transaction.NotFound");
    }

    [Fact]
    public async Task Handle_ShouldReturnError_WhenIsInitialBalance()
    {
        var account = Account.Create(_userId, "Bank", "USD", 1000m);
        _dbContext.Accounts.Add(account);

        var tx = Transaction.Create(_userId, account.Id, null, 100m, "USD", 1m, DateTime.UtcNow, "Initial", TransactionType.Income, isInitialBalance: true);
        _dbContext.Transactions.Add(tx);
        await _dbContext.SaveChangesAsync();

        var result = await _handler.Handle(new DeleteTransactionCommand(tx.Id), CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Type.Should().Be(ErrorOr.ErrorType.Validation);
        result.FirstError.Code.Should().Be("Transaction.CannotDeleteInitialBalance");
    }

    [Fact]
    public async Task Handle_ShouldDeleteChildTransactions_WhenDeletingParent()
    {
        var account = Account.Create(_userId, "Bank", "USD", 1000m);
        _dbContext.Accounts.Add(account);

        var parent = Transaction.Create(_userId, account.Id, null, 200m, "USD", 1m, DateTime.UtcNow, "Parent", TransactionType.Expense);
        var child = Transaction.Create(_userId, account.Id, null, 50m, "USD", 1m, DateTime.UtcNow, "Child", TransactionType.Loan, parentTransactionId: parent.Id);

        account.ApplyTransaction(Money.FromPositive(200m, CurrencyCode.From("USD")), 1m, 1, TransactionType.Expense);
        _dbContext.Transactions.AddRange(parent, child);
        await _dbContext.SaveChangesAsync();

        var result = await _handler.Handle(new DeleteTransactionCommand(parent.Id), CancellationToken.None);

        result.IsError.Should().BeFalse();
        var transactions = await _dbContext.Transactions.ToListAsync();
        transactions.Should().BeEmpty();

        var updatedAccount = await _dbContext.Accounts.FindAsync(account.Id);
        updatedAccount!.Balance.Amount.Should().Be(1000m);
    }

    [Fact]
    public async Task Handle_ShouldRevertBalance_WhenDeletingExpenseWithChildren()
    {
        var account = Account.Create(_userId, "Bank", "USD", 1000m);
        _dbContext.Accounts.Add(account);

        var parent = Transaction.Create(_userId, account.Id, null, 200m, "USD", 1m, DateTime.UtcNow, "Expense", TransactionType.Expense);
        var child = Transaction.Create(_userId, account.Id, null, 50m, "USD", 1m, DateTime.UtcNow, "Cashback", TransactionType.Income, parentTransactionId: parent.Id);

        account.ApplyTransaction(Money.FromPositive(200m, CurrencyCode.From("USD")), 1m, 1, TransactionType.Expense);
        account.ApplyTransaction(Money.FromPositive(50m, CurrencyCode.From("USD")), 1m, 1, TransactionType.Income);
        _dbContext.Transactions.AddRange(parent, child);
        await _dbContext.SaveChangesAsync();

        var result = await _handler.Handle(new DeleteTransactionCommand(parent.Id), CancellationToken.None);

        result.IsError.Should().BeFalse();
        var updatedAccount = await _dbContext.Accounts.FindAsync(account.Id);
        updatedAccount!.Balance.Amount.Should().Be(1000m);
    }

    [Fact]
    public async Task Handle_ShouldRestoreRepaidParticipants_WhenDeletingBulkRepayment()
    {
        var account = Account.Create(_userId, "Bank", "USD", 1000m);
        _dbContext.Accounts.Add(account);

        var personId = Guid.NewGuid();
        var expense = Transaction.Create(_userId, account.Id, null, 50m, "USD", 1m, DateTime.UtcNow, "Gasto", TransactionType.Expense);
        var parentParticipant = expense.CreateParticipant(personId, 30m, remainingAmount: 30m);
        account.ApplyTransaction(Money.FromPositive(50m, CurrencyCode.From("USD")), 1m, 1, TransactionType.Expense);
        _dbContext.Transactions.Add(expense);

        var childLoan = Transaction.Create(_userId, account.Id, null, 30m, "USD", 1m, DateTime.UtcNow, "Gasto", TransactionType.Loan, parentTransactionId: expense.Id);
        var childParticipant = childLoan.CreateParticipant(personId, 30m, remainingAmount: 30m);
        _dbContext.Transactions.Add(childLoan);
        await _dbContext.SaveChangesAsync();

        var repayment = Transaction.Create(_userId, account.Id, null, 30m, "USD", 1m, DateTime.UtcNow, "Pago", TransactionType.Repayment);
        repayment.RecordRepaidParticipants(System.Text.Json.JsonSerializer.Serialize(
            new { repaid = new[] { new { p = childParticipant.Id.ToString(), a = 30m } } }));
        _dbContext.Transactions.Add(repayment);
        parentParticipant.ReduceRemaining(30m);
        childParticipant.ReduceRemaining(30m);
        await _dbContext.SaveChangesAsync();

        var result = await _handler.Handle(new DeleteTransactionCommand(repayment.Id), CancellationToken.None);

        result.IsError.Should().BeFalse();

        var reloadedParent = await _dbContext.TransactionParticipants.FindAsync(parentParticipant.Id);
        reloadedParent!.RemainingAmount.Amount.Should().Be(30m);

        var reloadedChild = await _dbContext.TransactionParticipants.FindAsync(childParticipant.Id);
        reloadedChild!.RemainingAmount.Amount.Should().Be(30m);
    }
}
