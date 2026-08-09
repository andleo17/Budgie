using Budgie.Modules.Finances.Application.RecurringTransactions.CreateRecurringTransaction;
using Budgie.Modules.Finances.Application.RecurringTransactions.GetRecurringTransactions;
using Budgie.Modules.Finances.Domain.Accounts;
using Budgie.Modules.Finances.Infrastructure.Database;
using Budgie.Shared.Abstractions;
using Budgie.Shared.Domain.ValueObjects;
using Budgie.Shared.Enums;
using ErrorOr;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Xunit;

namespace Budgie.Modules.Finances.UnitTests.Application.RecurringTransactions;

public class RecurringTransactionCrudHandlerTests
{
    private readonly FinancesDbContext _dbContext;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly Guid _userId = Guid.NewGuid();

    public RecurringTransactionCrudHandlerTests()
    {
        _currentUserProvider = Substitute.For<ICurrentUserProvider>();
        _currentUserProvider.GetUserId().Returns(_userId);
        _currentUserProvider.UserId.Returns(_userId);

        var options = new DbContextOptionsBuilder<FinancesDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _dbContext = new FinancesDbContext(options, _currentUserProvider);
    }

    [Fact]
    public async Task Handle_Create_ShouldCreateRecurringTransaction()
    {
        var account = Account.Create(_userId, "Checking", "USD", 1000m);
        _dbContext.Accounts.Add(account);
        await _dbContext.SaveChangesAsync();

        var handler = new CreateRecurringTransactionCommandHandler(_dbContext, _currentUserProvider);
        var command = new CreateRecurringTransactionCommand(
            "Rent", account.Id, null, 1200m, "USD", 1.0m,
            TransactionType.Expense, null, 1, true,
            RecurringApplicability.Automatic, RecurringFrequency.Monthly,
            null, RecurringEndType.OnDate, null, DateTime.UtcNow.AddYears(1),
            DateTime.UtcNow);

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsError.Should().BeFalse();
        var rt = await _dbContext.RecurringTransactions.FirstAsync();
        rt.Name.Value.Should().Be("Rent");
        rt.Frequency.Should().Be(RecurringFrequency.Monthly);
        rt.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_Create_Monthly_Works()
    {
        var account = Account.Create(_userId, "Checking", "USD", 1000m);
        _dbContext.Accounts.Add(account);
        await _dbContext.SaveChangesAsync();

        var handler = new CreateRecurringTransactionCommandHandler(_dbContext, _currentUserProvider);
        var start = DateTime.UtcNow;
        var command = new CreateRecurringTransactionCommand(
            "Sub", account.Id, null, 10m, "USD", 1.0m,
            TransactionType.Expense, null, 1, true,
            RecurringApplicability.Automatic, RecurringFrequency.Monthly,
            null, RecurringEndType.AfterCount, 12, DateTime.UtcNow.AddYears(2),
            start);

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsError.Should().BeFalse();
        var rt = await _dbContext.RecurringTransactions.FirstAsync();
        rt.Frequency.Should().Be(RecurringFrequency.Monthly);
        rt.EndType.Should().Be(RecurringEndType.AfterCount);
        rt.EndAfterCount.Should().Be(12);
        rt.StartDate.Should().NotBeNull();
        rt.NextDueDate.Should().Be(rt.StartDate);
    }

    [Fact]
    public async Task Handle_Create_Template_NoFrequencyRequired()
    {
        var account = Account.Create(_userId, "Checking", "USD", 1000m);
        _dbContext.Accounts.Add(account);
        await _dbContext.SaveChangesAsync();

        var handler = new CreateRecurringTransactionCommandHandler(_dbContext, _currentUserProvider);
        var command = new CreateRecurringTransactionCommand(
            "Template", account.Id, null, 50m, "USD", 1.0m,
            TransactionType.Expense, null, 1, true,
            RecurringApplicability.Template, null,
            null, null, null, null, null);

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsError.Should().BeFalse();
        var rt = await _dbContext.RecurringTransactions.FirstAsync();
        rt.Applicability.Should().Be(RecurringApplicability.Template);
        rt.Frequency.Should().BeNull();
        rt.EndType.Should().BeNull();
    }

    [Fact]
    public async Task Handle_Create_NonTemplateWithoutFrequency_ReturnsError()
    {
        var account = Account.Create(_userId, "Checking", "USD", 1000m);
        _dbContext.Accounts.Add(account);
        await _dbContext.SaveChangesAsync();

        var handler = new CreateRecurringTransactionCommandHandler(_dbContext, _currentUserProvider);
        var command = new CreateRecurringTransactionCommand(
            "Bad", account.Id, null, 50m, "USD", 1.0m,
            TransactionType.Expense, null, 1, true,
            RecurringApplicability.Automatic, null,
            null, RecurringEndType.Never, null, DateTime.UtcNow.AddYears(1),
            DateTime.UtcNow);

        var act = () => handler.Handle(command, CancellationToken.None).AsTask();

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task Handle_Get_ShouldReturnResults()
    {
        var account = Account.Create(_userId, "Checking", "USD", 1000m);
        _dbContext.Accounts.Add(account);

        for (var i = 0; i < 3; i++)
        {
            var rt = Budgie.Modules.Finances.Domain.RecurringTransactions.RecurringTransaction.Create(
                _userId, $"RT {i}", account.Id, null, 100m, "USD", 1.0m,
                TransactionType.Expense, null, 1, true,
                RecurringApplicability.Automatic, RecurringFrequency.Monthly,
                null, RecurringEndType.OnDate, null, DateTime.UtcNow.AddYears(1),
                DateTime.UtcNow);
            _dbContext.RecurringTransactions.Add(rt);
        }
        await _dbContext.SaveChangesAsync();

        var handler = new GetRecurringTransactionsQueryHandler(_dbContext, _currentUserProvider);
        var query = new GetRecurringTransactionsQuery();
        var result = await handler.Handle(query, CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.Should().HaveCount(3);
    }
}
