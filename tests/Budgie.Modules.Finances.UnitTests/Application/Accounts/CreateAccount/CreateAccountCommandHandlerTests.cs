using Budgie.Modules.Finances.Application.Accounts.CreateAccount;
using Budgie.Modules.Finances.Infrastructure;
using Budgie.Modules.Finances.Infrastructure.Database;
using Budgie.Shared.Abstractions;
using Budgie.Shared.Domain.ValueObjects;
using Budgie.Shared.Enums;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace Budgie.Modules.Finances.UnitTests.Application.Accounts.CreateAccount;

public class CreateAccountCommandHandlerTests : IDisposable
{
    private readonly FinancesDbContext _dbContext;
    private readonly CreateAccountCommandHandler _handler;
    private readonly ICurrentUserProvider _currentUserProvider = Substitute.For<ICurrentUserProvider>();
    private readonly IUserSettingsProvider _settingsProvider = Substitute.For<IUserSettingsProvider>();
    private readonly ILanguageProvider _languageProvider = Substitute.For<ILanguageProvider>();
    private readonly ITranslationService _translationService = Substitute.For<ITranslationService>();
    private readonly Guid _userId = Guid.NewGuid();

    public CreateAccountCommandHandlerTests()
    {
        _currentUserProvider.UserId.Returns(_userId);
        _currentUserProvider.GetUserId().Returns(_userId);
        _languageProvider.LanguageCode.Returns("en");
        _translationService.Translate(Arg.Any<string>(), Arg.Any<string>()).Returns("translated note");

        var options = new DbContextOptionsBuilder<FinancesDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _dbContext = new FinancesDbContext(options, _currentUserProvider);
        _handler = new CreateAccountCommandHandler(_dbContext, _currentUserProvider, _settingsProvider, _languageProvider, _translationService, NullLogger<CreateAccountCommandHandler>.Instance);
    }

    [Fact]
    public async Task Handle_ShouldCreateAccountInDatabase()
    {
        var command = new CreateAccountCommand("Vacation Fund", "EUR", 500m);

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.Should().NotBeEmpty();

        var account = await _dbContext.Accounts.FindAsync(result.Value);
        account.Should().NotBeNull();
        account!.Name.Value.Should().Be(command.Name);
        account.Balance.Currency.Code.Should().Be(command.Currency);
        account.Balance.Amount.Should().Be(command.InitialBalance);
    }

    [Fact]
    public async Task Handle_CreditAccountWithSettings_ShouldCreateCreditAccount()
    {
        var command = new CreateAccountCommand(
            "Credit Card", "USD", 0m,
            AccountType.Credit, 5000m,
            CreditConsumptionMode.Total, 5, 20, 18.5m);

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.Should().NotBeEmpty();

        var account = await _dbContext.Accounts.FindAsync(result.Value);
        account.Should().NotBeNull();
        account!.Type.Should().Be(AccountType.Credit);
        account.CreditCardSettings.Should().NotBeNull();
        account.CreditCardSettings!.CreditLimit.Amount.Should().Be(5000m);
        account.CreditCardSettings.CreditConsumptionMode.Should().Be(CreditConsumptionMode.Total);
        account.CreditCardSettings.BillingCycleDay.Should().Be(5);
        account.CreditCardSettings.PaymentDueDay.Should().Be(20);

        // Credit line income should be created
        var creditTx = await _dbContext.Transactions
            .FirstOrDefaultAsync(t => t.AccountId == account.Id && t.Type == TransactionType.Income);
        creditTx.Should().NotBeNull();
        creditTx!.Amount.Amount.Should().Be(5000m);
    }

    [Fact]
    public async Task Handle_CreditAccountWithoutConsumptionMode_ShouldUseDefaultFromProvider()
    {
        _settingsProvider.GetDefaultCreditConsumptionModeAsync().Returns(CreditConsumptionMode.Total);

        var command = new CreateAccountCommand(
            "Credit Card", "USD", 1000m,
            AccountType.Credit, 3000m,
            null, 5, 20, 15m);

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsError.Should().BeFalse();
        var account = await _dbContext.Accounts.FindAsync(result.Value);
        account!.CreditCardSettings.Should().NotBeNull();
        account.CreditCardSettings!.CreditConsumptionMode.Should().Be(CreditConsumptionMode.Total);
    }

    [Fact]
    public async Task Handle_CreditAccountWithInvalidBillingCycleDay_ShouldThrow()
    {
        var command = new CreateAccountCommand(
            "Bad Card", "USD", 0m,
            AccountType.Credit, 1000m,
            CreditConsumptionMode.Total, 32, 20, 15m);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            _handler.Handle(command, CancellationToken.None).AsTask());
    }

    [Fact]
    public async Task Handle_ForeignCurrencyAccount_ShouldCreateWithCorrectCurrency()
    {
        var command = new CreateAccountCommand("Euro Wallet", "EUR", 1000m);

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsError.Should().BeFalse();
        var account = await _dbContext.Accounts.FindAsync(result.Value);
        account!.Balance.Currency.Code.Should().Be("EUR");
        account.Balance.Amount.Should().Be(1000m);
    }

    public void Dispose()
    {
        _dbContext.Dispose();
    }
}
