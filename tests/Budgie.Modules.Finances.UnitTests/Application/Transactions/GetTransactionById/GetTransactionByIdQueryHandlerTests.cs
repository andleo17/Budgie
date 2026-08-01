using Budgie.Modules.Finances.Application.Transactions;
using Budgie.Modules.Finances.Application.Transactions.GetTransactionById;
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

namespace Budgie.Modules.Finances.UnitTests.Application.Transactions.GetTransactionById;

public class GetTransactionByIdQueryHandlerTests
{
    private readonly FinancesDbContext _dbContext;
    private readonly GetTransactionByIdQueryHandler _handler;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly Guid _userId = Guid.NewGuid();

    public GetTransactionByIdQueryHandlerTests()
    {
        _currentUserProvider = Substitute.For<ICurrentUserProvider>();
        _currentUserProvider.UserId.Returns(_userId);

        var options = new DbContextOptionsBuilder<FinancesDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _dbContext = new FinancesDbContext(options, _currentUserProvider);
        _handler = new GetTransactionByIdQueryHandler(_dbContext);
    }

    [Fact]
    public async Task Handle_ShouldReturnTransactionById()
    {
        var account = Account.Create(_userId, "Bank", "USD", 1000m);
        _dbContext.Accounts.Add(account);
        var tx = Transaction.Create(_userId, account.Id, null, 200m, "USD", 1m, DateTime.UtcNow, "Rent", TransactionType.Expense);
        _dbContext.Transactions.Add(tx);
        await _dbContext.SaveChangesAsync();

        var result = await _handler.Handle(new GetTransactionByIdQuery(tx.Id), CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.Id.Should().Be(tx.Id);
        result.Value.Amount.Should().Be(200m);
        result.Value.Notes.Should().Be("Rent");
    }

    [Fact]
    public async Task Handle_ShouldReturnNotFound_WhenTransactionDoesNotExist()
    {
        var result = await _handler.Handle(new GetTransactionByIdQuery(Guid.NewGuid()), CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Type.Should().Be(ErrorOr.ErrorType.NotFound);
        result.FirstError.Code.Should().Be("Transaction.NotFound");
    }

    [Fact]
    public async Task Handle_ShouldMapParticipantsCorrectly()
    {
        var account = Account.Create(_userId, "Bank", "USD", 1000m);
        _dbContext.Accounts.Add(account);
        var personId = Guid.NewGuid();

        var tx = Transaction.Create(_userId, account.Id, null, 200m, "USD", 1m, DateTime.UtcNow, "Dinner", TransactionType.Expense);
        tx.CreateParticipant(personId, 50m, remainingAmount: 25m);
        _dbContext.Transactions.Add(tx);
        await _dbContext.SaveChangesAsync();

        var result = await _handler.Handle(new GetTransactionByIdQuery(tx.Id), CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.Participants.Should().HaveCount(1);
        result.Value.Participants![0].PersonId.Should().Be(personId);
        result.Value.Participants[0].ContributedAmount.Should().Be(50m);
        result.Value.Participants[0].RemainingAmount.Should().Be(25m);
    }

    [Fact]
    public async Task Handle_ShouldMapCategoryName_WhenCategoryExists()
    {
        var account = Account.Create(_userId, "Bank", "USD", 1000m);
        _dbContext.Accounts.Add(account);
        var category = Category.Create(_userId, "Food", 1);
        _dbContext.Categories.Add(category);
        await _dbContext.SaveChangesAsync();

        var tx = Transaction.Create(_userId, account.Id, category.Id, 50m, "USD", 1m, DateTime.UtcNow, "Pizza", TransactionType.Expense);
        _dbContext.Transactions.Add(tx);
        await _dbContext.SaveChangesAsync();

        var result = await _handler.Handle(new GetTransactionByIdQuery(tx.Id), CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.CategoryId.Should().Be(category.Id);
        result.Value.CategoryName.Should().Be("Food");
    }
}
