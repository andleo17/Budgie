using Budgie.Modules.Finances.Application.Transactions.Transfer;
using Budgie.Modules.Finances.Infrastructure;
using Budgie.Modules.Finances.Domain.Accounts;
using Budgie.Modules.Finances.Domain.Transactions;
using Budgie.Modules.Finances.Infrastructure.Database;
using Budgie.Shared.Abstractions;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace Budgie.Modules.Finances.UnitTests.Application.Transactions.Transfer;

public class TransferCommandHandlerTests : IDisposable
{
    private readonly FinancesDbContext _dbContext;
    private readonly TransferCommandHandler _handler;
    private readonly ICurrentUserProvider _currentUserProvider = Substitute.For<ICurrentUserProvider>();
    private readonly ILanguageProvider _languageProvider = Substitute.For<ILanguageProvider>();
    private readonly ITranslationService _translationService = Substitute.For<ITranslationService>();
    private readonly Guid _userId = Guid.NewGuid();

    public TransferCommandHandlerTests()
    {
        _currentUserProvider.UserId.Returns(_userId);
        _currentUserProvider.GetUserId().Returns(_userId);
        _languageProvider.LanguageCode.Returns("en");
        _translationService.Translate(Arg.Any<string>(), Arg.Any<string>()).Returns("translated note");

        var options = new DbContextOptionsBuilder<FinancesDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _dbContext = new FinancesDbContext(options, _currentUserProvider);
        _handler = new TransferCommandHandler(_dbContext, _currentUserProvider, _languageProvider, _translationService, NullLogger<TransferCommandHandler>.Instance);
    }

    [Fact]
    public async Task Handle_ShouldTransferMoneyAndDeductFee()
    {
        // Arrange
        var fromAccount = Account.Create(_userId, "Bank A", "USD", 1000m);
        var toAccount = Account.Create(_userId, "Bank B", "USD", 500m);
        _dbContext.Accounts.AddRange(fromAccount, toAccount);
        await _dbContext.SaveChangesAsync();

        var command = new TransferCommand(
            fromAccount.Id, 
            toAccount.Id, 
            200m, 
            5m, 
            "USD", 
            1.0m, 
            DateTime.UtcNow, 
            "Rent Payment");

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsError.Should().BeFalse();
        
        var updatedFrom = await _dbContext.Accounts.FindAsync(fromAccount.Id);
        var updatedTo = await _dbContext.Accounts.FindAsync(toAccount.Id);
        
        updatedFrom!.Balance.Amount.Should().Be(795m); // 1000 - 200 - 5
        updatedTo!.Balance.Amount.Should().Be(700m);   // 500 + 200
        
        var transactions = await _dbContext.Transactions.ToListAsync();
        transactions.Should().HaveCount(3); // Outgoing, Incoming, Fee
    }

    [Fact]
    public async Task Handle_ShouldFail_WhenSameAccount()
    {
        var account = Account.Create(_userId, "Bank A", "USD", 1000m);
        _dbContext.Accounts.Add(account);
        await _dbContext.SaveChangesAsync();

        var command = new TransferCommand(
            account.Id, account.Id, 200m, 0m, "USD", 1.0m, DateTime.UtcNow, "Self transfer");

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsError.Should().BeFalse();
        var transactions = await _dbContext.Transactions.ToListAsync();
        transactions.Should().HaveCount(2);
        var updated = await _dbContext.Accounts.FindAsync(account.Id);
        updated!.Balance.Amount.Should().Be(1000m);
    }

    [Fact]
    public async Task Handle_ShouldHandleCurrencyConversion()
    {
        var fromAccount = Account.Create(_userId, "USD Account", "USD", 1000m);
        var toAccount = Account.Create(_userId, "EUR Account", "EUR", 500m);
        _dbContext.Accounts.AddRange(fromAccount, toAccount);
        await _dbContext.SaveChangesAsync();

        var command = new TransferCommand(
            fromAccount.Id, toAccount.Id, 100m, 0m, "USD", 0.85m, DateTime.UtcNow, "Currency conversion");

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsError.Should().BeFalse();
        var updatedFrom = await _dbContext.Accounts.FindAsync(fromAccount.Id);
        var updatedTo = await _dbContext.Accounts.FindAsync(toAccount.Id);
        updatedFrom!.Balance.Amount.Should().Be(900m);
        updatedTo!.Balance.Amount.Should().Be(585m);
    }

    public void Dispose()
    {
        _dbContext.Dispose();
    }
}
