using Budgie.Modules.Finances.Application.Debts.GetPersonDebts;
using Budgie.Modules.Finances.Domain.Accounts;
using Budgie.Modules.Finances.Domain.Transactions;
using Budgie.Modules.Finances.Infrastructure.Database;
using Budgie.Shared.Abstractions;
using Budgie.Shared.Enums;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Xunit;

namespace Budgie.Modules.Finances.UnitTests.Application.Debts.GetPersonDebts;

public class GetPersonDebtsQueryHandlerTests
{
    private readonly FinancesDbContext _dbContext;
    private readonly GetPersonDebtsQueryHandler _handler;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly Guid _userId = Guid.NewGuid();
    private readonly Guid _personId = Guid.NewGuid();

    public GetPersonDebtsQueryHandlerTests()
    {
        _currentUserProvider = Substitute.For<ICurrentUserProvider>();
        _currentUserProvider.UserId.Returns(_userId);

        var options = new DbContextOptionsBuilder<FinancesDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _dbContext = new FinancesDbContext(options, _currentUserProvider);
        _handler = new GetPersonDebtsQueryHandler(_dbContext);
    }

    [Fact]
    public async Task Handle_ShouldReturnDebtsForPerson()
    {
        var account = Account.Create(_userId, "Bank", "USD", 1000m);
        _dbContext.Accounts.Add(account);
        var tx = Transaction.Create(_userId, account.Id, null, 500m, "USD", 1m, DateTime.UtcNow, "Loan", TransactionType.Loan);
        tx.CreateParticipant(_personId, 500m, 300m);
        _dbContext.Transactions.Add(tx);
        await _dbContext.SaveChangesAsync();

        var result = await _handler.Handle(new GetPersonDebtsQuery(_personId), CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.Should().HaveCount(1);
        result.Value[0].PersonId.Should().Be(_personId);
        result.Value[0].RemainingAmount.Should().Be(300m);
    }

    [Fact]
    public async Task Handle_ShouldReturnEmpty_WhenNoDebts()
    {
        var result = await _handler.Handle(new GetPersonDebtsQuery(_personId), CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_ShouldOnlyIncludeParticipantsWithRemainingAmount()
    {
        var account = Account.Create(_userId, "Bank", "USD", 1000m);
        _dbContext.Accounts.Add(account);
        var tx1 = Transaction.Create(_userId, account.Id, null, 500m, "USD", 1m, DateTime.UtcNow, "Loan1", TransactionType.Loan);
        tx1.CreateParticipant(_personId, 500m, 0m); // fully repaid
        var tx2 = Transaction.Create(_userId, account.Id, null, 300m, "USD", 1m, DateTime.UtcNow, "Loan2", TransactionType.Loan);
        tx2.CreateParticipant(_personId, 300m, 100m); // partially repaid
        _dbContext.Transactions.AddRange(tx1, tx2);
        await _dbContext.SaveChangesAsync();

        var result = await _handler.Handle(new GetPersonDebtsQuery(_personId), CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.Should().HaveCount(1);
        result.Value[0].RemainingAmount.Should().Be(100m);
    }
}
