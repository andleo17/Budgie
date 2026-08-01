using Budgie.Modules.Finances.Application.Accounts.UpdateAccount;
using Budgie.Modules.Finances.Domain.Accounts;
using Budgie.Shared.Domain.ValueObjects;
using Budgie.Shared.Enums;
using Budgie.Modules.Finances.Infrastructure.Database;
using Budgie.Shared.Abstractions;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace Budgie.Modules.Finances.UnitTests.Application.Accounts.UpdateAccount;

public class UpdateAccountCommandHandlerTests
{
    private readonly FinancesDbContext _dbContext;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly UpdateAccountCommandHandler _handler;
    private readonly Guid _userId = Guid.NewGuid();

    public UpdateAccountCommandHandlerTests()
    {
        _currentUserProvider = Substitute.For<ICurrentUserProvider>();
        _currentUserProvider.UserId.Returns(_userId);
        _currentUserProvider.GetUserId().Returns(_userId);

        var options = new DbContextOptionsBuilder<FinancesDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _dbContext = new FinancesDbContext(options, _currentUserProvider);
        _handler = new UpdateAccountCommandHandler(_dbContext, _currentUserProvider, NullLogger<UpdateAccountCommandHandler>.Instance);
    }

    [Fact]
    public async Task Handle_WhenAccountExists_ShouldUpdateAccount()
    {
        // Arrange
        var account = Account.Create(_userId, "Old Name", "USD", 0, AccountType.Checking);
        _dbContext.Accounts.Add(account);
        await _dbContext.SaveChangesAsync();

        var command = new UpdateAccountCommand(account.Id, "New Name", null, null, null, null);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsError.Should().BeFalse();
        var updatedAccount = await _dbContext.Accounts.FirstAsync();
        updatedAccount.Name.Value.Should().Be("New Name");
    }

    [Fact]
    public async Task Handle_WhenAccountDoesNotExist_ShouldReturnNotFound()
    {
        // Arrange
        var command = new UpdateAccountCommand(Guid.NewGuid(), "New Name", null, null, null, null);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsError.Should().BeTrue();
        result.FirstError.Type.Should().Be(ErrorOr.ErrorType.NotFound);
    }

    [Fact]
    public async Task Handle_CreditLimitIncrease_ShouldCreateTransaction()
    {
        var settings = new CreditCardSettings(1000m, CreditConsumptionMode.Total, 1, 10, 0m, true, CurrencyCode.From("USD"));
        var account = Account.Create(_userId, "Card", "USD", 0, AccountType.Credit, settings);
        _dbContext.Accounts.Add(account);
        await _dbContext.SaveChangesAsync();

        var command = new UpdateAccountCommand(account.Id, "Card", CreditLimit: 2000m, CreditConsumptionMode.Total);

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsError.Should().BeFalse();

        var updated = await _dbContext.Accounts.FindAsync(account.Id);
        updated!.CreditCardSettings.Should().NotBeNull();
        updated.CreditCardSettings!.CreditLimit.Amount.Should().Be(2000m);

        var all = await _dbContext.Transactions.ToListAsync();
        all.Should().ContainSingle();
        all[0].Type.Should().Be(TransactionType.Loan);
        all[0].Amount.Amount.Should().Be(1000m);
    }

    [Fact]
    public async Task Handle_WithCreditAccount_ShouldCreateCreditCardSettings()
    {
        var settings = new CreditCardSettings(1000m, CreditConsumptionMode.Total, 1, 10, 0m, true, CurrencyCode.From("USD"));
        var account = Account.Create(_userId, "Card", "USD", 0, AccountType.Credit, settings);
        _dbContext.Accounts.Add(account);
        await _dbContext.SaveChangesAsync();

        var command = new UpdateAccountCommand(
            account.Id, "Card",
            CreditLimit: 2000m,
            CreditConsumptionMode: CreditConsumptionMode.Total,
            BillingCycleDay: 15,
            PaymentDueDay: 5,
            AnnualInterestRate: 15m);

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsError.Should().BeFalse();
        var updated = await _dbContext.Accounts.FindAsync(account.Id);
        updated!.CreditCardSettings.Should().NotBeNull();
        updated.CreditCardSettings!.BillingCycleDay.Should().Be(15);
        updated.CreditCardSettings!.PaymentDueDay.Should().Be(5);
        updated.CreditCardSettings!.CreditLimit.Amount.Should().Be(2000m);
        updated.CreditCardSettings!.AnnualInterestRate.Value.Should().Be(15m);
    }
}
