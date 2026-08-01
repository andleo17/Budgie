using Budgie.Modules.Finances.Application.Transactions.CreateTransaction;
using Budgie.Modules.Finances.Application.Transactions.DeleteTransaction;
using Budgie.Modules.Finances.Application.Transactions.UpdateTransaction;
using Budgie.Modules.Finances.Domain.Accounts;
using Budgie.Modules.Finances.Domain.Transactions;
using Budgie.Modules.Finances.Infrastructure.Database;
using Budgie.Shared.Abstractions;
using Budgie.Shared.Domain.ValueObjects;
using Budgie.Shared.Enums;
using ErrorOr;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace Budgie.Modules.Finances.UnitTests.Application.Transactions;

public class TransactionCrudHandlersTests
{
    private readonly FinancesDbContext _dbContext;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly Guid _userId = Guid.NewGuid();

    public TransactionCrudHandlersTests()
    {
        _currentUserProvider = Substitute.For<ICurrentUserProvider>();
        _currentUserProvider.UserId.Returns(_userId);

        var options = new DbContextOptionsBuilder<FinancesDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _dbContext = new FinancesDbContext(options, _currentUserProvider);
    }

    [Fact]
    public async Task Handle_Update_AddParticipants_ShouldSucceed()
    {
        var account = Account.Create(_userId, "Test", "USD", 1000m, AccountType.Checking);
        _dbContext.Accounts.Add(account);

        var transaction = Transaction.Create(_userId, account.Id, null, 120m, "USD", 1.0m, DateTime.UtcNow, "Test", TransactionType.Expense, 1);
        account.ApplyTransaction(Money.FromPositive(120m, CurrencyCode.From("USD")), 1.0m, 1, TransactionType.Expense);

        _dbContext.Transactions.Add(transaction);
        await _dbContext.SaveChangesAsync();

        var personsAccount = Account.Create(_userId, "Persons", "USD", 500m, AccountType.Checking);
        _dbContext.Accounts.Add(personsAccount);
        await _dbContext.SaveChangesAsync();

        var personId = Guid.NewGuid();

        var handler = new UpdateTransactionCommandHandler(_dbContext, NullLogger<UpdateTransactionCommandHandler>.Instance);
        var command = new UpdateTransactionCommand(
            transaction.Id, null, 120m, "USD", 1.0m, DateTime.UtcNow, "Test", TransactionType.Expense, 1,
            [new TransactionParticipantDto(personId, 20m, 0m, false, personsAccount.Id)]);

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsError.Should().BeFalse();

        var updated = await _dbContext.Transactions.Include(t => t.Participants).FirstAsync(t => t.Id == transaction.Id);
        updated.Participants.Should().HaveCount(1);
        account.Balance.Amount.Should().Be(900m); // 1000 - 120 (expense) + 20 (child INCOME cash)
        personsAccount.Balance.Amount.Should().Be(500m);
    }

    [Fact]
    public async Task Handle_Update_ShouldRevertOldAndApplyNewBalance()
    {
        var account = Account.Create(_userId, "Test", "USD", 1000m, AccountType.Checking);
        _dbContext.Accounts.Add(account);
        
        var transaction = Transaction.Create(_userId, account.Id, null, 100m, "USD", 1.0m, DateTime.UtcNow, "Old", TransactionType.Expense, 1);
        account.ApplyTransaction(Money.FromPositive(100m, CurrencyCode.From("USD")), 1.0m, 1, TransactionType.Expense);
        
        _dbContext.Transactions.Add(transaction);
        await _dbContext.SaveChangesAsync();

        var handler = new UpdateTransactionCommandHandler(_dbContext, NullLogger<UpdateTransactionCommandHandler>.Instance);
        var command = new UpdateTransactionCommand(transaction.Id, null, 200m, "USD", 1.0m, DateTime.UtcNow, "New", TransactionType.Expense, 1);
        var result = await handler.Handle(command, CancellationToken.None);

        result.IsError.Should().BeFalse();
        account.Balance.Amount.Should().Be(800m);
        var updated = await _dbContext.Transactions.FirstAsync();
        updated.Amount.Amount.Should().Be(200m);
    }

    [Fact]
    public async Task Handle_Delete_ShouldRevertBalance()
    {
        var account = Account.Create(_userId, "Test", "USD", 1000m, AccountType.Checking);
        _dbContext.Accounts.Add(account);
        
        var transaction = Transaction.Create(_userId, account.Id, null, 100m, "USD", 1.0m, DateTime.UtcNow, "To delete", TransactionType.Expense, 1);
        account.ApplyTransaction(Money.FromPositive(100m, CurrencyCode.From("USD")), 1.0m, 1, TransactionType.Expense);
        
        _dbContext.Transactions.Add(transaction);
        await _dbContext.SaveChangesAsync();

        var handler = new DeleteTransactionCommandHandler(_dbContext, NullLogger<DeleteTransactionCommandHandler>.Instance);
        var command = new DeleteTransactionCommand(transaction.Id);
        var result = await handler.Handle(command, CancellationToken.None);

        result.IsError.Should().BeFalse();
        account.Balance.Amount.Should().Be(1000m);
        var exists = await _dbContext.Transactions.AnyAsync(t => t.Id == transaction.Id);
        exists.Should().BeFalse();
    }
}
