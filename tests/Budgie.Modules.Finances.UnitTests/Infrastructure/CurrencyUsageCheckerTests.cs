using Budgie.Modules.Finances.Domain.Accounts;
using Budgie.Modules.Finances.Infrastructure;
using Budgie.Modules.Finances.Infrastructure.Database;
using Budgie.Shared.Abstractions;
using Budgie.Shared.Enums;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Xunit;

namespace Budgie.Modules.Finances.UnitTests.Infrastructure;

public class CurrencyUsageCheckerTests
{
    private readonly FinancesDbContext _dbContext;
    private readonly CurrencyUsageChecker _checker;
    private readonly Guid _userId = Guid.NewGuid();

    public CurrencyUsageCheckerTests()
    {
        var currentUserProvider = Substitute.For<ICurrentUserProvider>();
        currentUserProvider.UserId.Returns(_userId);

        var options = new DbContextOptionsBuilder<FinancesDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _dbContext = new FinancesDbContext(options, currentUserProvider);
        _checker = new CurrencyUsageChecker(_dbContext);
    }

    [Fact]
    public async Task GetBlockingAccountNamesAsync_WhenNoAccountsUseCurrency_ReturnsEmpty()
    {
        var account = Account.Create(_userId, "My Account", "USD", 0);
        _dbContext.Accounts.Add(account);
        await _dbContext.SaveChangesAsync();

        var result = await _checker.GetBlockingAccountNamesAsync(_userId, "EUR");

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetBlockingAccountNamesAsync_WhenAccountsUseCurrency_ReturnsBlockingNames()
    {
        _dbContext.Accounts.Add(Account.Create(_userId, "Savings", "USD", 500));
        _dbContext.Accounts.Add(Account.Create(_userId, "Checking", "USD", 100));
        _dbContext.Accounts.Add(Account.Create(_userId, "EUR Account", "EUR", 200));
        await _dbContext.SaveChangesAsync();

        var result = await _checker.GetBlockingAccountNamesAsync(_userId, "USD");

        result.Should().BeEquivalentTo(new[] { "Savings", "Checking" });
    }

    [Fact]
    public async Task GetBlockingAccountNamesAsync_WhenBalanceIsZero_DoesNotBlock()
    {
        _dbContext.Accounts.Add(Account.Create(_userId, "Empty USD", "USD", 0));
        await _dbContext.SaveChangesAsync();

        var result = await _checker.GetBlockingAccountNamesAsync(_userId, "USD");

        result.Should().BeEmpty();
    }
}