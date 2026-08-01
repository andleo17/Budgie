using Budgie.Modules.Finances.Application.Transactions.CreateTransaction;
using Budgie.Modules.Finances.Domain.Accounts;
using Budgie.Modules.Finances.Domain.Categories;
using Budgie.Modules.Finances.Domain.Transactions;
using Budgie.Modules.Finances.Infrastructure.Database;
using Budgie.Shared.Abstractions;
using Budgie.Shared.Enums;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace Budgie.Modules.Finances.UnitTests.Application.Transactions.CreateTransaction;

public class CreateTransactionCommandHandlerTests : IDisposable
{
    private readonly FinancesDbContext _dbContext;
    private readonly CreateTransactionCommandHandler _handler;
    private readonly ICurrentUserProvider _currentUserProvider = Substitute.For<ICurrentUserProvider>();
    private readonly Guid _userId = Guid.NewGuid();

    public CreateTransactionCommandHandlerTests()
    {
        _currentUserProvider.UserId.Returns(_userId);
        _currentUserProvider.GetUserId().Returns(_userId);

        var options = new DbContextOptionsBuilder<FinancesDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _dbContext = new FinancesDbContext(options, _currentUserProvider);
        _handler = new CreateTransactionCommandHandler(_dbContext, _currentUserProvider, NullLogger<CreateTransactionCommandHandler>.Instance);
    }

    [Fact]
    public async Task Handle_ShouldReturnError_WhenAccountNotFound()
    {
        // Arrange
        var command = new CreateTransactionCommand(
            Guid.NewGuid(), 
            null, 
            100m, 
            "USD",
            1.0m,
            DateTime.UtcNow, 
            "Lunch", 
            TransactionType.Expense);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("Account.NotFound");
    }

    [Fact]
    public async Task Handle_ShouldReturnError_WhenCategoryNotFound()
    {
        // Arrange
        var account = Account.Create(_userId, "Wallet", "USD", 100m);
        _dbContext.Accounts.Add(account);
        await _dbContext.SaveChangesAsync();

        var command = new CreateTransactionCommand(
            account.Id, 
            Guid.NewGuid(), 
            50m, 
            "USD",
            1.0m,
            DateTime.UtcNow, 
            "Gift", 
            TransactionType.Expense);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("Category.NotFound");
    }

    [Fact]
    public async Task Handle_ShouldCreateTransaction_WhenDataIsValid()
    {
        // Arrange
        var account = Account.Create(_userId, "Bank", "USD", 1000m);
        var category = Category.Create(_userId, "Food", 1, "#FF0000", "fast-food");
        _dbContext.Accounts.Add(account);
        _dbContext.Categories.Add(category);
        await _dbContext.SaveChangesAsync();

        var command = new CreateTransactionCommand(
            account.Id, 
            category.Id, 
            25m, 
            "USD",
            1.0m,
            DateTime.UtcNow, 
            "Pizza", 
            TransactionType.Expense);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsError.Should().BeFalse();
        result.Value.Should().NotBeEmpty();

        var transaction = await _dbContext.Transactions.FindAsync(result.Value);
        transaction.Should().NotBeNull();
        transaction!.Amount.Amount.Should().Be(command.Amount);
        transaction.AccountId.Should().Be(account.Id);
        transaction.CategoryId.Should().Be(category.Id);
    }

    [Fact]
    public async Task Handle_ShouldUpdateAccountBalance()
    {
        // Arrange
        var account = Account.Create(_userId, "Savings", "USD", 1000m);
        _dbContext.Accounts.Add(account);
        await _dbContext.SaveChangesAsync();

        var command = new CreateTransactionCommand(
            account.Id, 
            null, 
            200m, 
            "USD",
            1.0m,
            DateTime.UtcNow, 
            "Withdrawal", 
            TransactionType.Expense);

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        var updatedAccount = await _dbContext.Accounts.FindAsync(account.Id);
        updatedAccount!.Balance.Amount.Should().Be(800m);
    }

    [Fact]
    public async Task Handle_ShouldUpdateAccountBalanceWithExchangeRate_WhenCurrenciesAreDifferent()
    {
        // Arrange
        var account = Account.Create(_userId, "Savings PEN", "PEN", 1000m);
        _dbContext.Accounts.Add(account);
        await _dbContext.SaveChangesAsync();

        var command = new CreateTransactionCommand(
            account.Id, 
            null, 
            100m, 
            "USD",
            3.8m,
            DateTime.UtcNow, 
            "Online Purchase", 
            TransactionType.Expense);

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        var updatedAccount = await _dbContext.Accounts.FindAsync(account.Id);
        updatedAccount!.Balance.Amount.Should().Be(1000m - (100m * 3.8m)); // 620m
    }

    [Fact]
    public async Task Handle_WithCreditParticipant_ShouldCreateChildLoan()
    {
        var account = Account.Create(_userId, "Wallet", "PEN", 1000m);
        _dbContext.Accounts.Add(account);
        await _dbContext.SaveChangesAsync();

        var personId = Guid.NewGuid();
        var command = new CreateTransactionCommand(
            account.Id, null, 100m, "PEN", 1.0m, DateTime.UtcNow, "Shared dinner", TransactionType.Expense,
            [new TransactionParticipantDto(personId, 50m, 50m, false, null)]);

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsError.Should().BeFalse();
        var tx = await _dbContext.Transactions
            .Include(t => t.Participants)
            .Include(t => t.ChildTransactions)
                .ThenInclude(ct => ct.Participants)
            .FirstAsync(t => t.Id == result.Value);

        // Parent participant keeps RemainingAmount > 0 (UI shows "credit")
        tx.Participants.Should().HaveCount(1);
        tx.Participants.First().RemainingAmount.Amount.Should().Be(50m);
        tx.Participants.First().IsTrackingOnly.Should().BeFalse();

        // Child LOAN transaction created
        tx.ChildTransactions.Should().HaveCount(1);
        var loan = tx.ChildTransactions.First();
        loan.Type.Should().Be(TransactionType.Loan);
        loan.Amount.Amount.Should().Be(50m);
        loan.AccountId.Should().Be(account.Id);
        loan.Participants.Should().HaveCount(1);
        loan.Participants.First().PersonId.Should().Be(personId);
        loan.Participants.First().RemainingAmount.Amount.Should().Be(50m);

        // Account debited the full amount (parent transaction amount)
        var updatedAccount = await _dbContext.Accounts.FindAsync(account.Id);
        updatedAccount!.Balance.Amount.Should().Be(900m);
    }

    [Fact]
    public async Task Handle_WithCashParticipant_ShouldCreateChildIncome()
    {
        var account = Account.Create(_userId, "Wallet", "PEN", 1000m);
        _dbContext.Accounts.Add(account);
        await _dbContext.SaveChangesAsync();

        var personId = Guid.NewGuid();
        var command = new CreateTransactionCommand(
            account.Id, null, 100m, "PEN", 1.0m, DateTime.UtcNow, "Shared paid cash", TransactionType.Expense,
            [new TransactionParticipantDto(personId, 50m, 0m, false, Guid.NewGuid())]);

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsError.Should().BeFalse();
        var tx = await _dbContext.Transactions
            .Include(t => t.Participants)
            .Include(t => t.ChildTransactions)
            .FirstAsync(t => t.Id == result.Value);

        tx.Participants.Should().HaveCount(1);
        tx.Participants.First().RemainingAmount.Amount.Should().Be(0);
        tx.ChildTransactions.Should().HaveCount(1);
        tx.ChildTransactions.First().Type.Should().Be(TransactionType.Income);
        tx.ChildTransactions.First().Amount.Amount.Should().Be(50m);
        tx.ChildTransactions.First().AccountId.Should().Be(account.Id);
    }

    [Fact]
    public async Task Handle_WithTagOnlyParticipant_ShouldNotCreateChildRepayment()
    {
        var account = Account.Create(_userId, "Wallet", "PEN", 1000m);
        _dbContext.Accounts.Add(account);
        await _dbContext.SaveChangesAsync();

        var personId = Guid.NewGuid();
        var command = new CreateTransactionCommand(
            account.Id, null, 100m, "PEN", 1.0m, DateTime.UtcNow, "Dinner with friend", TransactionType.Expense,
            [new TransactionParticipantDto(personId, 0m, 0m, true, null)]);

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsError.Should().BeFalse();
        var tx = await _dbContext.Transactions
            .Include(t => t.Participants)
            .Include(t => t.ChildTransactions)
            .FirstAsync(t => t.Id == result.Value);

        tx.Participants.Should().HaveCount(1);
        tx.Participants.First().IsTrackingOnly.Should().BeTrue();
        tx.Participants.First().RemainingAmount.Amount.Should().Be(0);
        tx.ChildTransactions.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_WithCreditParticipant_ShouldCreateReferenceLoan()
    {
        // Scenario: account with 100 balance, expense of 50 with credit participant 25
        // Full amount debited from account, child LOAN as reference
        var account = Account.Create(_userId, "Wallet", "PEN", 100m);
        _dbContext.Accounts.Add(account);
        await _dbContext.SaveChangesAsync();

        var personId = Guid.NewGuid();
        var command = new CreateTransactionCommand(
            account.Id, null, 50m, "PEN", 1.0m, DateTime.UtcNow, "Shared lunch", TransactionType.Expense,
            [new TransactionParticipantDto(personId, 25m, 25m, false, null)]);

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsError.Should().BeFalse();

        var updatedAccount = await _dbContext.Accounts.FindAsync(account.Id);
        updatedAccount!.Balance.Amount.Should().Be(50m); // 100 - 50 (full amount)

        // Verify only one child LOAN (no duplicate debt tracking)
        var tx = await _dbContext.Transactions
            .Include(t => t.ChildTransactions)
            .FirstAsync(t => t.Id == result.Value);
        tx.ChildTransactions.Should().HaveCount(1);
        tx.ChildTransactions.First().Type.Should().Be(TransactionType.Loan);
        tx.ChildTransactions.First().Amount.Amount.Should().Be(25m);

        // Debts endpoint should return exactly 1 debt
        var debts = await _dbContext.TransactionParticipants
            .Where(tp => tp.PersonId == personId && tp.RemainingAmount.Amount > 0 && !tp.IsTrackingOnly
                && tp.Transaction.Type == TransactionType.Loan)
            .ToListAsync();
        debts.Should().HaveCount(1);
        debts[0].RemainingAmount.Amount.Should().Be(25m);
    }

    [Fact]
    public async Task Handle_ShouldReturnError_WhenDateBeforeAccountCreation()
    {
        var account = Account.Create(_userId, "Wallet", "USD", 100m);
        _dbContext.Accounts.Add(account);
        await _dbContext.SaveChangesAsync();

        var command = new CreateTransactionCommand(
            account.Id, null, 50m, "USD", 1.0m,
            account.CreatedAt.AddDays(-1), "Past", TransactionType.Expense);

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("Transaction.DateBeforeAccountCreation");
    }

    [Fact]
    public async Task Handle_ShouldUseInitialBalanceCategory_WhenIsInitialBalance()
    {
        var account = Account.Create(_userId, "Wallet", "USD", 100m);
        _dbContext.Accounts.Add(account);
        await _dbContext.SaveChangesAsync();

        var command = new CreateTransactionCommand(
            account.Id, null, 100m, "USD", 1.0m,
            DateTime.UtcNow, "Initial", TransactionType.Income,
            IsInitialBalance: true);

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsError.Should().BeFalse();
        var transaction = await _dbContext.Transactions.FindAsync(result.Value);
        transaction!.CategoryId.Should().Be(SystemCategoryIds.InitialBalance);
    }

    public void Dispose()
    {
        _dbContext.Dispose();
    }
}
