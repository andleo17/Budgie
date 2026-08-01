using Budgie.Modules.Finances.Application.Accounts.GetAccounts;
using Budgie.Modules.Finances.Domain.Accounts;
using Budgie.Modules.Finances.Infrastructure.Database;
using Budgie.Shared.Abstractions;
using Budgie.Shared.Enums;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Xunit;

namespace Budgie.Modules.Finances.UnitTests.Application.Accounts.GetAccounts;

public class GetAccountsQueryHandlerTests
{
    private readonly FinancesDbContext _dbContext;
    private readonly GetAccountsQueryHandler _handler;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly Guid _userId = Guid.NewGuid();

    public GetAccountsQueryHandlerTests()
    {
        _currentUserProvider = Substitute.For<ICurrentUserProvider>();
        _currentUserProvider.UserId.Returns(_userId);

        var options = new DbContextOptionsBuilder<FinancesDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _dbContext = new FinancesDbContext(options, _currentUserProvider);
        _handler = new GetAccountsQueryHandler(_dbContext);
    }

    [Fact]
    public async Task Handle_ShouldReturnAllAccountsForUser()
    {
        _dbContext.Accounts.AddRange(
            Account.Create(_userId, "Wallet", "USD", 500m),
            Account.Create(_userId, "Savings", "USD", 1500m));
        await _dbContext.SaveChangesAsync();

        var result = await _handler.Handle(new GetAccountsQuery(), CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.Should().HaveCount(2);
    }

    [Fact]
    public async Task Handle_ShouldReturnEmptyList_WhenNoAccounts()
    {
        var result = await _handler.Handle(new GetAccountsQuery(), CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_ShouldNotReturnOtherUsersAccounts()
    {
        var otherUserId = Guid.NewGuid();
        _dbContext.Accounts.AddRange(
            Account.Create(_userId, "Mine", "USD", 100m),
            Account.Create(otherUserId, "Theirs", "USD", 200m));
        await _dbContext.SaveChangesAsync();

        var result = await _handler.Handle(new GetAccountsQuery(), CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.Should().ContainSingle(a => a.Name == "Mine");
        result.Value.Should().NotContain(a => a.Name == "Theirs");
    }
}
