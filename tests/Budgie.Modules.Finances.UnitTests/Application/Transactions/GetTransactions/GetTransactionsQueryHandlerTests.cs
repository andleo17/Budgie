using Budgie.Modules.Finances.Application.Transactions.GetTransactions;
using Budgie.Modules.Finances.Domain.Accounts;
using Budgie.Modules.Finances.Domain.Categories;
using Budgie.Modules.Finances.Domain.Transactions;
using Budgie.Modules.Finances.Infrastructure.Database;
using Budgie.Shared.Abstractions;
using Budgie.Shared.Enums;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Xunit;

namespace Budgie.Modules.Finances.UnitTests.Application.Transactions.GetTransactions;

public class GetTransactionsQueryHandlerTests
{
    private readonly FinancesDbContext _dbContext;
    private readonly GetTransactionsQueryHandler _handler;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly Guid _userId = Guid.NewGuid();

    public GetTransactionsQueryHandlerTests()
    {
        _currentUserProvider = Substitute.For<ICurrentUserProvider>();
        _currentUserProvider.UserId.Returns(_userId);

        var options = new DbContextOptionsBuilder<FinancesDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _dbContext = new FinancesDbContext(options, _currentUserProvider);
        _handler = new GetTransactionsQueryHandler(_dbContext);
    }

    [Fact]
    public async Task Handle_ShouldReturnPagedTransactions()
    {
        var account = Account.Create(_userId, "Bank", "USD", 1000m);
        _dbContext.Accounts.Add(account);
        for (var i = 0; i < 5; i++)
        {
            _dbContext.Transactions.Add(
                Transaction.Create(_userId, account.Id, null, 100m, "USD", 1m,
                    DateTime.UtcNow.AddDays(-i), $"Tx {i}", TransactionType.Expense));
        }
        await _dbContext.SaveChangesAsync();

        var result = await _handler.Handle(
            new GetTransactionsQuery(Page: 1, PageSize: 3), CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.Items.Should().HaveCount(3);
        result.Value.TotalCount.Should().Be(5);
    }

    [Fact]
    public async Task Handle_ShouldFilterByAccountId()
    {
        var account1 = Account.Create(_userId, "Bank1", "USD", 1000m);
        var account2 = Account.Create(_userId, "Bank2", "USD", 1000m);
        _dbContext.Accounts.AddRange(account1, account2);
        _dbContext.Transactions.AddRange(
            Transaction.Create(_userId, account1.Id, null, 100m, "USD", 1m, DateTime.UtcNow, "A1", TransactionType.Expense),
            Transaction.Create(_userId, account2.Id, null, 200m, "USD", 1m, DateTime.UtcNow, "A2", TransactionType.Expense));
        await _dbContext.SaveChangesAsync();

        var result = await _handler.Handle(
            new GetTransactionsQuery(AccountIds: new[] { account1.Id }), CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.Items.Should().ContainSingle(t => t.AccountId == account1.Id);
        result.Value.Items.Should().NotContain(t => t.AccountId == account2.Id);
    }

    [Fact]
    public async Task Handle_ShouldFilterByCategoryId()
    {
        var account = Account.Create(_userId, "Bank", "USD", 1000m);
        _dbContext.Accounts.Add(account);
        var cat1 = Category.Create(_userId, "Food", 1);
        var cat2 = Category.Create(_userId, "Transport", 1);
        _dbContext.Categories.AddRange(cat1, cat2);
        _dbContext.Transactions.AddRange(
            Transaction.Create(_userId, account.Id, cat1.Id, 50m, "USD", 1m, DateTime.UtcNow, "Food", TransactionType.Expense),
            Transaction.Create(_userId, account.Id, cat2.Id, 30m, "USD", 1m, DateTime.UtcNow, "Bus", TransactionType.Expense));
        await _dbContext.SaveChangesAsync();

        var result = await _handler.Handle(
            new GetTransactionsQuery(CategoryIds: new[] { cat1.Id }), CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.Items.Should().ContainSingle(t => t.Notes == "Food");
        result.Value.Items.Should().NotContain(t => t.Notes == "Bus");
    }

    [Fact]
    public async Task Handle_ShouldFilterByTransactionType()
    {
        var account = Account.Create(_userId, "Bank", "USD", 1000m);
        _dbContext.Accounts.Add(account);
        _dbContext.Transactions.AddRange(
            Transaction.Create(_userId, account.Id, null, 1000m, "USD", 1m, DateTime.UtcNow, "Salary", TransactionType.Income),
            Transaction.Create(_userId, account.Id, null, 200m, "USD", 1m, DateTime.UtcNow, "Rent", TransactionType.Expense));
        await _dbContext.SaveChangesAsync();

        var result = await _handler.Handle(
            new GetTransactionsQuery(TransactionTypes: new[] { TransactionType.Income }), CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.Items.Should().ContainSingle(t => t.Type == TransactionType.Income);
        result.Value.Items.Should().NotContain(t => t.Type == TransactionType.Expense);
    }

    [Fact]
    public async Task Handle_ShouldFilterByDateRange()
    {
        var account = Account.Create(_userId, "Bank", "USD", 1000m);
        _dbContext.Accounts.Add(account);
        var now = DateTime.UtcNow;
        var firstDay = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        _dbContext.Transactions.AddRange(
            Transaction.Create(_userId, account.Id, null, 100m, "USD", 1m, firstDay.AddDays(5), "Current", TransactionType.Expense),
            Transaction.Create(_userId, account.Id, null, 200m, "USD", 1m, firstDay.AddMonths(-1), "Old", TransactionType.Expense));
        await _dbContext.SaveChangesAsync();

        var result = await _handler.Handle(
            new GetTransactionsQuery(StartDate: firstDay, EndDate: firstDay.AddMonths(1)), CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.Items.Should().ContainSingle(t => t.Notes == "Current");
        result.Value.Items.Should().NotContain(t => t.Notes == "Old");
    }

    [Fact]
    public async Task Handle_ShouldRespectMultiTenantFilter()
    {
        var otherUserId = Guid.NewGuid();
        var myAccount = Account.Create(_userId, "Mine", "USD", 1000m);
        var theirAccount = Account.Create(otherUserId, "Theirs", "USD", 1000m);
        _dbContext.Accounts.AddRange(myAccount, theirAccount);
        _dbContext.Transactions.AddRange(
            Transaction.Create(_userId, myAccount.Id, null, 100m, "USD", 1m, DateTime.UtcNow, "Mine", TransactionType.Expense),
            Transaction.Create(otherUserId, theirAccount.Id, null, 200m, "USD", 1m, DateTime.UtcNow, "Theirs", TransactionType.Expense));
        await _dbContext.SaveChangesAsync();

        var result = await _handler.Handle(new GetTransactionsQuery(), CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.Items.Should().ContainSingle(t => t.Notes == "Mine");
        result.Value.Items.Should().NotContain(t => t.Notes == "Theirs");
    }

    [Fact]
    public async Task Handle_ShouldReturnPagedResults()
    {
        var account = Account.Create(_userId, "Bank", "USD", 1000m);
        _dbContext.Accounts.Add(account);
        for (var i = 0; i < 20; i++)
        {
            _dbContext.Transactions.Add(
                Transaction.Create(_userId, account.Id, null, 100m, "USD", 1m,
                    DateTime.UtcNow.AddDays(-i), $"Tx {i}", TransactionType.Expense));
        }
        await _dbContext.SaveChangesAsync();

        var result = await _handler.Handle(
            new GetTransactionsQuery(Page: 1, PageSize: 5), CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.Items.Should().HaveCount(5);
        result.Value.TotalCount.Should().Be(20);
    }

    [Fact]
    public async Task Handle_ShouldSearchByNotes()
    {
        var account = Account.Create(_userId, "Bank", "USD", 1000m);
        _dbContext.Accounts.Add(account);
        _dbContext.Transactions.AddRange(
            Transaction.Create(_userId, account.Id, null, 100m, "USD", 1m, DateTime.UtcNow, "Groceries", TransactionType.Expense),
            Transaction.Create(_userId, account.Id, null, 200m, "USD", 1m, DateTime.UtcNow, "Gasoline", TransactionType.Expense));
        await _dbContext.SaveChangesAsync();

        var result = await _handler.Handle(
            new GetTransactionsQuery(Search: "Groceries"), CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.Items.Should().ContainSingle(t => t.Notes == "Groceries");
        result.Value.Items.Should().NotContain(t => t.Notes == "Gasoline");
    }

    [Fact]
    public async Task Handle_ShouldFilterBySearchTerm()
    {
        var account = Account.Create(_userId, "Bank", "USD", 1000m);
        _dbContext.Accounts.Add(account);
        _dbContext.Transactions.AddRange(
            Transaction.Create(_userId, account.Id, null, 100m, "USD", 1m, DateTime.UtcNow, "Gasoline station", TransactionType.Expense),
            Transaction.Create(_userId, account.Id, null, 200m, "USD", 1m, DateTime.UtcNow, "Internet bill", TransactionType.Expense));
        await _dbContext.SaveChangesAsync();

        var result = await _handler.Handle(
            new GetTransactionsQuery(Search: "Gas"), CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.Items.Should().ContainSingle(t => t.Notes == "Gasoline station");
        result.Value.Items.Should().NotContain(t => t.Notes == "Internet bill");
    }

    [Fact]
    public async Task Handle_ShouldFilterByCategoryIds()
    {
        var account = Account.Create(_userId, "Bank", "USD", 1000m);
        _dbContext.Accounts.Add(account);
        var cat1 = Category.Create(_userId, "Food", 1);
        var cat2 = Category.Create(_userId, "Transport", 1);
        var cat3 = Category.Create(_userId, "Entertainment", 1);
        _dbContext.Categories.AddRange(cat1, cat2, cat3);
        _dbContext.Transactions.AddRange(
            Transaction.Create(_userId, account.Id, cat1.Id, 50m, "USD", 1m, DateTime.UtcNow, "Groceries", TransactionType.Expense),
            Transaction.Create(_userId, account.Id, cat2.Id, 30m, "USD", 1m, DateTime.UtcNow, "Bus", TransactionType.Expense),
            Transaction.Create(_userId, account.Id, cat3.Id, 20m, "USD", 1m, DateTime.UtcNow, "Movie", TransactionType.Expense));
        await _dbContext.SaveChangesAsync();

        var result = await _handler.Handle(
            new GetTransactionsQuery(CategoryIds: new[] { cat1.Id, cat2.Id }), CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.Items.Should().HaveCount(2);
        result.Value.Items.Should().NotContain(t => t.Notes == "Movie");
    }

    [Fact]
    public async Task Handle_ShouldFilterByTransactionTypes()
    {
        var account = Account.Create(_userId, "Bank", "USD", 1000m);
        _dbContext.Accounts.Add(account);
        _dbContext.Transactions.AddRange(
            Transaction.Create(_userId, account.Id, null, 1000m, "USD", 1m, DateTime.UtcNow, "Salary", TransactionType.Income),
            Transaction.Create(_userId, account.Id, null, 200m, "USD", 1m, DateTime.UtcNow, "Rent", TransactionType.Expense),
            Transaction.Create(_userId, account.Id, null, 50m, "USD", 1m, DateTime.UtcNow, "Transfer out", TransactionType.Transfer));
        await _dbContext.SaveChangesAsync();

        var result = await _handler.Handle(
            new GetTransactionsQuery(TransactionTypes: new[] { TransactionType.Expense, TransactionType.Transfer }), CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.Items.Should().HaveCount(2);
        result.Value.Items.Should().NotContain(t => t.Notes == "Salary");
    }

    [Fact]
    public async Task Handle_ShouldFilterByPersonId()
    {
        var account = Account.Create(_userId, "Bank", "USD", 1000m);
        _dbContext.Accounts.Add(account);
        var personId = Guid.NewGuid();

        var tx1 = Transaction.Create(_userId, account.Id, null, 100m, "USD", 1m, DateTime.UtcNow, "With person", TransactionType.Expense);
        tx1.CreateParticipant(personId, 50m, remainingAmount: 50m);

        var tx2 = Transaction.Create(_userId, account.Id, null, 200m, "USD", 1m, DateTime.UtcNow, "Without person", TransactionType.Expense);

        _dbContext.Transactions.AddRange(tx1, tx2);
        await _dbContext.SaveChangesAsync();

        var result = await _handler.Handle(
            new GetTransactionsQuery(PersonId: personId), CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.Items.Should().ContainSingle(t => t.Notes == "With person");
        result.Value.Items.Should().NotContain(t => t.Notes == "Without person");
    }
}
