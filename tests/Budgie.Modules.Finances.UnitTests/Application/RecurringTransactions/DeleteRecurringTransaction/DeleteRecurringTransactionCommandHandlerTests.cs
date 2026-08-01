using Budgie.Modules.Finances.Application.RecurringTransactions.DeleteRecurringTransaction;
using Budgie.Modules.Finances.Domain.Accounts;
using Budgie.Modules.Finances.Domain.RecurringTransactions;
using Budgie.Modules.Finances.Infrastructure.Database;
using Budgie.Shared.Abstractions;
using Budgie.Shared.Enums;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Xunit;

namespace Budgie.Modules.Finances.UnitTests.Application.RecurringTransactions.DeleteRecurringTransaction;

public class DeleteRecurringTransactionCommandHandlerTests
{
    private readonly FinancesDbContext _dbContext;
    private readonly DeleteRecurringTransactionCommandHandler _handler;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly Guid _userId = Guid.NewGuid();

    public DeleteRecurringTransactionCommandHandlerTests()
    {
        _currentUserProvider = Substitute.For<ICurrentUserProvider>();
        _currentUserProvider.UserId.Returns(_userId);
        _currentUserProvider.GetUserId().Returns(_userId);

        var options = new DbContextOptionsBuilder<FinancesDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _dbContext = new FinancesDbContext(options, _currentUserProvider);
        _handler = new DeleteRecurringTransactionCommandHandler(_dbContext, _currentUserProvider);
    }

    [Fact]
    public async Task Handle_ShouldDeleteRecurringTransaction()
    {
        var account = Account.Create(_userId, "Checking", "USD", 1000m);
        _dbContext.Accounts.Add(account);
        var rt = RecurringTransaction.Create(
            _userId, "Rent", account.Id, null, 1200m, "USD", 1m,
            TransactionType.Expense, null, 1, true,
            RecurringApplicability.Automatic, RecurringFrequency.Monthly,
            null, RecurringEndType.OnDate, null, DateTime.UtcNow.AddYears(1),
            DateTime.UtcNow);
        _dbContext.RecurringTransactions.Add(rt);
        await _dbContext.SaveChangesAsync();

        var result = await _handler.Handle(new DeleteRecurringTransactionCommand(rt.Id), CancellationToken.None);

        result.IsError.Should().BeFalse();
        var exists = await _dbContext.RecurringTransactions.AnyAsync(r => r.Id == rt.Id);
        exists.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_ShouldReturnNotFound_WhenNotExists()
    {
        var result = await _handler.Handle(new DeleteRecurringTransactionCommand(Guid.NewGuid()), CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Type.Should().Be(ErrorOr.ErrorType.NotFound);
        result.FirstError.Code.Should().Be("RecurringTransaction.NotFound");
    }
}
