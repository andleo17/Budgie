using Budgie.Modules.Finances.Application.Transactions.BulkRepay;
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

namespace Budgie.Modules.Finances.UnitTests.Application.Transactions.BulkRepay;

public class BulkRepayCommandHandlerTests : IDisposable
{
    private readonly FinancesDbContext _dbContext;
    private readonly BulkRepayCommandHandler _handler;
    private readonly ICurrentUserProvider _currentUserProvider = Substitute.For<ICurrentUserProvider>();
    private readonly Guid _userId = Guid.NewGuid();

    public BulkRepayCommandHandlerTests()
    {
        _currentUserProvider.UserId.Returns(_userId);
        _currentUserProvider.GetUserId().Returns(_userId);

        var options = new DbContextOptionsBuilder<FinancesDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _dbContext = new FinancesDbContext(options, _currentUserProvider);
        _handler = new BulkRepayCommandHandler(_dbContext, _currentUserProvider, NullLogger<BulkRepayCommandHandler>.Instance);
    }

    [Fact]
    public async Task Handle_ShouldReturnError_WhenAccountNotFound()
    {
        var command = new BulkRepayCommand(Guid.NewGuid(), 100m, "USD", 1.0m, DateTime.UtcNow, "Pay", []);

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("Account.NotFound");
    }

    [Fact]
    public async Task Handle_ShouldReduceRemainingAmountsAndCreateRepayment()
    {
        var account = Account.Create(_userId, "Wallet", "USD", 1000m);
        _dbContext.Accounts.Add(account);

        var loan1 = Transaction.Create(_userId, account.Id, null, 100m, "USD", 1.0m, DateTime.UtcNow, "Loan 1", TransactionType.Loan);
        var p1 = loan1.CreateParticipant(Guid.NewGuid(), 100m, remainingAmount: 100m);
        _dbContext.Transactions.Add(loan1);
        account.ApplyTransaction(Money.FromPositive(100m, CurrencyCode.From("USD")), 1.0m, 1, TransactionType.Loan);

        var loan2 = Transaction.Create(_userId, account.Id, null, 200m, "USD", 1.0m, DateTime.UtcNow, "Loan 2", TransactionType.Loan);
        var p2 = loan2.CreateParticipant(Guid.NewGuid(), 200m, remainingAmount: 200m);
        _dbContext.Transactions.Add(loan2);
        account.ApplyTransaction(Money.FromPositive(200m, CurrencyCode.From("USD")), 1.0m, 1, TransactionType.Loan);

        await _dbContext.SaveChangesAsync();

        var command = new BulkRepayCommand(
            account.Id, 150m, "USD", 1.0m, DateTime.UtcNow, "Bulk payment",
            [
                new DebtRepayment(p1.Id, 100m, loan1.Id),
                new DebtRepayment(p2.Id, 50m, loan2.Id),
            ]);

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsError.Should().BeFalse();

        var reloadedP1 = await _dbContext.TransactionParticipants.FindAsync(p1.Id);
        reloadedP1!.RemainingAmount.Amount.Should().Be(0);

        var reloadedP2 = await _dbContext.TransactionParticipants.FindAsync(p2.Id);
        reloadedP2!.RemainingAmount.Amount.Should().Be(150m);

        var updatedAccount = await _dbContext.Accounts.FindAsync(account.Id);
        updatedAccount!.Balance.Amount.Should().Be(1000m - 100m - 200m + 150m);

        var repayments = await _dbContext.Transactions
            .Where(t => t.Type == TransactionType.Repayment)
            .ToListAsync();
        repayments.Should().HaveCount(1);
        repayments[0].Amount.Amount.Should().Be(150m);
    }

    [Fact]
    public async Task Handle_ShouldSkipMissingParticipants()
    {
        var account = Account.Create(_userId, "Wallet", "USD", 1000m);
        _dbContext.Accounts.Add(account);
        await _dbContext.SaveChangesAsync();

        var command = new BulkRepayCommand(
            account.Id, 50m, "USD", 1.0m, DateTime.UtcNow, "Partial",
            [new DebtRepayment(Guid.NewGuid(), 50m, Guid.NewGuid())]);

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsError.Should().BeFalse();
        var updatedAccount = await _dbContext.Accounts.FindAsync(account.Id);
        updatedAccount!.Balance.Amount.Should().Be(1050m);
    }

    [Fact]
    public async Task Handle_ShouldReduceParticipantRemaining()
    {
        var account = Account.Create(_userId, "Wallet", "USD", 1000m);
        _dbContext.Accounts.Add(account);

        var expense = Transaction.Create(_userId, account.Id, null, 100m, "USD", 1.0m, DateTime.UtcNow, "Expense", TransactionType.Expense);
        var participant = expense.CreateParticipant(Guid.NewGuid(), 100m, remainingAmount: 50m);
        _dbContext.Transactions.Add(expense);
        account.ApplyTransaction(Money.FromPositive(100m, CurrencyCode.From("USD")), 1.0m, 1, TransactionType.Expense);
        await _dbContext.SaveChangesAsync();

        var command = new BulkRepayCommand(
            account.Id, 30m, "USD", 1.0m, DateTime.UtcNow, "Partial repayment",
            [new DebtRepayment(participant.Id, 30m, expense.Id)]);

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsError.Should().BeFalse();
        var reloaded = await _dbContext.TransactionParticipants.FindAsync(participant.Id);
        reloaded!.RemainingAmount.Amount.Should().Be(20m);
    }

    [Fact]
    public async Task Handle_ShouldReduceParentParticipant_WhenChildLoanRepaid()
    {
        var account = Account.Create(_userId, "Wallet", "USD", 1000m);
        _dbContext.Accounts.Add(account);

        var personId = Guid.NewGuid();
        var expense = Transaction.Create(_userId, account.Id, null, 100m, "USD", 1m, DateTime.UtcNow, "Expense", TransactionType.Expense);
        var expenseParticipant = expense.CreateParticipant(personId, 100m, remainingAmount: 100m);
        account.ApplyTransaction(Money.FromPositive(100m, CurrencyCode.From("USD")), 1m, 1, TransactionType.Expense);
        _dbContext.Transactions.Add(expense);

        var childLoan = Transaction.Create(_userId, account.Id, null, 100m, "USD", 1m, DateTime.UtcNow, null, TransactionType.Loan, parentTransactionId: expense.Id);
        var childParticipant = childLoan.CreateParticipant(personId, 100m, remainingAmount: 100m);
        _dbContext.Transactions.Add(childLoan);
        await _dbContext.SaveChangesAsync();

        var command = new BulkRepayCommand(
            account.Id, 60m, "USD", 1m, DateTime.UtcNow, "Partial repayment",
            [new DebtRepayment(childParticipant.Id, 60m, childLoan.Id)]);

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsError.Should().BeFalse();
        var reloaded = await _dbContext.TransactionParticipants.FindAsync(childParticipant.Id);
        reloaded!.RemainingAmount.Amount.Should().Be(40m);
    }

    [Fact]
    public async Task Handle_ShouldKeepUserNotesClean_AndStoreRepaidMetadataSeparately()
    {
        var account = Account.Create(_userId, "Wallet", "USD", 1000m);
        _dbContext.Accounts.Add(account);

        var personId = Guid.NewGuid();
        var loan = Transaction.Create(_userId, account.Id, null, 100m, "USD", 1m, DateTime.UtcNow, "Loan", TransactionType.Loan);
        var participant = loan.CreateParticipant(personId, 100m, remainingAmount: 100m);
        account.ApplyTransaction(Money.FromPositive(100m, CurrencyCode.From("USD")), 1m, 1, TransactionType.Loan);
        _dbContext.Transactions.Add(loan);
        await _dbContext.SaveChangesAsync();

        var command = new BulkRepayCommand(
            account.Id, 40m, "USD", 1m, DateTime.UtcNow, "Pago recibido",
            [new DebtRepayment(participant.Id, 40m, loan.Id)]);

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsError.Should().BeFalse();

        var repayment = await _dbContext.Transactions
            .FirstAsync(t => t.Type == TransactionType.Repayment);

        repayment.Notes.Should().Be("Pago recibido");
        repayment.RepaidParticipants.Should().NotBeNullOrEmpty();
        repayment.RepaidParticipants.Should().Contain(participant.Id.ToString());
    }

    public void Dispose()
    {
        _dbContext.Dispose();
    }
}
