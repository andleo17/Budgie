using Budgie.Modules.Finances.Application.RecurringTransactions.GetRecurringTransaction;
using Budgie.Modules.Finances.Domain.Accounts;
using Budgie.Modules.Finances.Domain.RecurringTransactions;
using Budgie.Modules.Finances.Infrastructure.Database;
using Budgie.Shared.Abstractions;
using Budgie.Shared.Enums;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Xunit;

namespace Budgie.Modules.Finances.UnitTests.Application.RecurringTransactions.GetRecurringTransaction;

public class GetRecurringTransactionQueryHandlerTests
{
    private readonly FinancesDbContext _dbContext;
    private readonly GetRecurringTransactionQueryHandler _handler;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly Guid _userId = Guid.NewGuid();

    public GetRecurringTransactionQueryHandlerTests()
    {
        _currentUserProvider = Substitute.For<ICurrentUserProvider>();
        _currentUserProvider.UserId.Returns(_userId);
        _currentUserProvider.GetUserId().Returns(_userId);

        var options = new DbContextOptionsBuilder<FinancesDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _dbContext = new FinancesDbContext(options, _currentUserProvider);
        _handler = new GetRecurringTransactionQueryHandler(_dbContext, _currentUserProvider);
    }

    [Fact]
    public async Task Handle_ShouldReturnRecurringTransactionById()
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

        var result = await _handler.Handle(new GetRecurringTransactionQuery(rt.Id), CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.Id.Should().Be(rt.Id);
        result.Value.Name.Should().Be("Rent");
        result.Value.Amount.Should().Be(1200m);
        result.Value.Frequency.Should().Be(RecurringFrequency.Monthly);
    }

    [Fact]
    public async Task Handle_ShouldReturnNotFound_WhenNotExists()
    {
        var result = await _handler.Handle(new GetRecurringTransactionQuery(Guid.NewGuid()), CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Type.Should().Be(ErrorOr.ErrorType.NotFound);
        result.FirstError.Code.Should().Be("RecurringTransaction.NotFound");
    }
}
