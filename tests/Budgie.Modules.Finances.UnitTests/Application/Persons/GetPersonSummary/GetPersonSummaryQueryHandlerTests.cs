using Budgie.Modules.Finances.Application.Persons.GetPersonSummary;
using Budgie.Modules.Finances.Domain.Accounts;
using Budgie.Modules.Finances.Domain.Transactions;
using Budgie.Modules.Finances.Infrastructure.Database;
using Budgie.Shared.Abstractions;
using Budgie.Shared.Enums;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Xunit;

namespace Budgie.Modules.Finances.UnitTests.Application.Persons.GetPersonSummary;

public class GetPersonSummaryQueryHandlerTests
{
    private readonly FinancesDbContext _dbContext;
    private readonly GetPersonSummaryQueryHandler _handler;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly Guid _userId = Guid.NewGuid();
    private readonly Guid _personId = Guid.NewGuid();

    public GetPersonSummaryQueryHandlerTests()
    {
        _currentUserProvider = Substitute.For<ICurrentUserProvider>();
        _currentUserProvider.UserId.Returns(_userId);

        var options = new DbContextOptionsBuilder<FinancesDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _dbContext = new FinancesDbContext(options, _currentUserProvider);
        _handler = new GetPersonSummaryQueryHandler(_dbContext);
    }

    [Fact]
    public async Task Handle_ShouldReturnPersonSummaryWithFinancialData()
    {
        var account = Account.Create(_userId, "Bank", "USD", 1000m);
        _dbContext.Accounts.Add(account);
        var tx1 = Transaction.Create(_userId, account.Id, null, 500m, "USD", 1m, DateTime.UtcNow, "Loan1", TransactionType.Loan);
        tx1.CreateParticipant(_personId, 500m, 200m);
        var tx2 = Transaction.Create(_userId, account.Id, null, 300m, "USD", 1m, DateTime.UtcNow, "Loan2", TransactionType.Loan);
        tx2.CreateParticipant(_personId, 300m, 0m);
        _dbContext.Transactions.AddRange(tx1, tx2);
        await _dbContext.SaveChangesAsync();

        var result = await _handler.Handle(new GetPersonSummaryQuery(_personId), CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.TotalTransactions.Should().Be(2);
        result.Value.TotalContributed.Should().Be(800m);
        result.Value.TotalRepaid.Should().Be(300m);
        result.Value.TotalPending.Should().Be(500m);
    }

    [Fact]
    public async Task Handle_ShouldReturnZeroSummary_WhenNoTransactions()
    {
        var result = await _handler.Handle(new GetPersonSummaryQuery(_personId), CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.TotalTransactions.Should().Be(0);
        result.Value.TotalContributed.Should().Be(0);
        result.Value.TotalRepaid.Should().Be(0);
        result.Value.TotalPending.Should().Be(0);
    }
}
