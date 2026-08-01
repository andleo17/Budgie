using Budgie.Modules.Users.Application.Settings.GetSettings;
using Budgie.Modules.Users.Application.Settings.UpdateSettings;
using Budgie.Modules.Users.Domain;
using Budgie.Modules.Users.Infrastructure.Database;
using Budgie.Shared.Abstractions;
using Budgie.Shared.Enums;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace Budgie.Modules.Users.UnitTests.Application.Settings;

public class SettingsHandlersTests
{
    private readonly UsersDbContext _dbContext;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly ICurrencyUsageChecker _currencyUsageChecker;
    private readonly Guid _userId = Guid.NewGuid();

    public SettingsHandlersTests()
    {
        _currentUserProvider = Substitute.For<ICurrentUserProvider>();
        _currentUserProvider.UserId.Returns(_userId);
        _currentUserProvider.GetUserId().Returns(_userId);

        _currencyUsageChecker = Substitute.For<ICurrencyUsageChecker>();

        var options = new DbContextOptionsBuilder<UsersDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _dbContext = new UsersDbContext(options);
    }

    // ── UpdateSettingsCommandHandler ──────────────────────────────

    [Fact]
    public async Task Handle_UpdateSettings_ShouldCreateIfNotExist()
    {
        var handler = new UpdateSettingsCommandHandler(_dbContext, _currentUserProvider, _currencyUsageChecker, NullLogger<UpdateSettingsCommandHandler>.Instance);
        var command = new UpdateSettingsCommand("EUR", CreditConsumptionMode.Partial);

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsError.Should().BeFalse();
        var settings = await _dbContext.UserSettings.FirstOrDefaultAsync(s => s.UserId == _userId);
        settings.Should().NotBeNull();
        settings!.MainCurrency.Code.Should().Be("EUR");
        settings.DefaultCreditConsumptionMode.Should().Be(CreditConsumptionMode.Partial);
        settings.IsConfigured.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_UpdateSettings_WithSupportedCurrencies_ShouldSetThem()
    {
        var handler = new UpdateSettingsCommandHandler(_dbContext, _currentUserProvider, _currencyUsageChecker, NullLogger<UpdateSettingsCommandHandler>.Instance);
        var command = new UpdateSettingsCommand("USD", CreditConsumptionMode.Total, SupportedCurrencies: ["USD", "EUR", "GBP"]);

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsError.Should().BeFalse();
        var settings = await _dbContext.UserSettings.FirstOrDefaultAsync(s => s.UserId == _userId);
        settings!.SupportedCurrencies.Should().BeEquivalentTo(["USD", "EUR", "GBP"]);
    }

    [Fact]
    public async Task Handle_UpdateSettings_WithSupportedCurrencies_ShouldFailIfMissingMainCurrency()
    {
        var handler = new UpdateSettingsCommandHandler(_dbContext, _currentUserProvider, _currencyUsageChecker, NullLogger<UpdateSettingsCommandHandler>.Instance);
        var command = new UpdateSettingsCommand("USD", CreditConsumptionMode.Total, SupportedCurrencies: ["EUR", "GBP"]);

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("Currency.IsMainCurrency");
    }

    [Fact]
    public async Task Handle_UpdateSettings_WhenRemovingMainCurrency_ShouldFail()
    {
        var settings = UserSettings.Create(_userId);
        settings.Update("USD", CreditConsumptionMode.Total, supportedCurrencies: ["USD", "EUR"]);
        _dbContext.UserSettings.Add(settings);
        await _dbContext.SaveChangesAsync();

        var handler = new UpdateSettingsCommandHandler(_dbContext, _currentUserProvider, _currencyUsageChecker, NullLogger<UpdateSettingsCommandHandler>.Instance);
        var command = new UpdateSettingsCommand("USD", CreditConsumptionMode.Total, SupportedCurrencies: ["EUR"]);

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("Currency.IsMainCurrency");
    }

    [Fact]
    public async Task Handle_UpdateSettings_WhenRemovingCurrencyWithActiveAccounts_ShouldFail()
    {
        var settings = UserSettings.Create(_userId);
        settings.Update("USD", CreditConsumptionMode.Total, supportedCurrencies: ["USD", "EUR"]);
        _dbContext.UserSettings.Add(settings);
        await _dbContext.SaveChangesAsync();

        _currencyUsageChecker
            .GetBlockingAccountNamesAsync(_userId, "EUR")
            .Returns(["Main Savings"]);

        var handler = new UpdateSettingsCommandHandler(_dbContext, _currentUserProvider, _currencyUsageChecker, NullLogger<UpdateSettingsCommandHandler>.Instance);
        var command = new UpdateSettingsCommand("USD", CreditConsumptionMode.Total, SupportedCurrencies: ["USD"]);

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("Currency.HasActiveAccounts");
    }

    [Fact]
    public async Task Handle_UpdateSettings_WhenRemovingCurrencyWithoutActiveAccounts_ShouldSucceed()
    {
        var settings = UserSettings.Create(_userId);
        settings.Update("USD", CreditConsumptionMode.Total, supportedCurrencies: ["USD", "EUR"]);
        _dbContext.UserSettings.Add(settings);
        await _dbContext.SaveChangesAsync();

        _currencyUsageChecker
            .GetBlockingAccountNamesAsync(_userId, "EUR")
            .Returns([]);

        var handler = new UpdateSettingsCommandHandler(_dbContext, _currentUserProvider, _currencyUsageChecker, NullLogger<UpdateSettingsCommandHandler>.Instance);
        var command = new UpdateSettingsCommand("USD", CreditConsumptionMode.Total, SupportedCurrencies: ["USD"]);

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsError.Should().BeFalse();
        var updated = await _dbContext.UserSettings.FirstAsync(s => s.UserId == _userId);
        updated.SupportedCurrencies.Should().BeEquivalentTo(["USD"]);
    }

    [Fact]
    public async Task Handle_UpdateSettings_WithoutSupportedCurrencies_ShouldAutoAddMainCurrency()
    {
        var settings = UserSettings.Create(_userId);
        _dbContext.UserSettings.Add(settings);
        await _dbContext.SaveChangesAsync();

        var handler = new UpdateSettingsCommandHandler(_dbContext, _currentUserProvider, _currencyUsageChecker, NullLogger<UpdateSettingsCommandHandler>.Instance);
        var command = new UpdateSettingsCommand("PEN", CreditConsumptionMode.Total);

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsError.Should().BeFalse();
        var updated = await _dbContext.UserSettings.FirstAsync(s => s.UserId == _userId);
        updated.SupportedCurrencies.Should().Contain("PEN");
    }

    // ── GetSettingsQueryHandler ───────────────────────────────────

    [Fact]
    public async Task Handle_GetSettings_ShouldReturnSettings()
    {
        var settings = UserSettings.Create(_userId);
        settings.Update("PEN", CreditConsumptionMode.Total);
        _dbContext.UserSettings.Add(settings);
        await _dbContext.SaveChangesAsync();

        var handler = new GetSettingsQueryHandler(_dbContext, _currentUserProvider);
        var query = new GetSettingsQuery();

        var result = await handler.Handle(query, CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.MainCurrency.Should().Be("PEN");
        result.Value.IsConfigured.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_GetSettings_ShouldReturnSupportedCurrencies()
    {
        var settings = UserSettings.Create(_userId);
        settings.Update("USD", CreditConsumptionMode.Total, supportedCurrencies: ["USD", "EUR", "GBP"]);
        _dbContext.UserSettings.Add(settings);
        await _dbContext.SaveChangesAsync();

        var handler = new GetSettingsQueryHandler(_dbContext, _currentUserProvider);
        var query = new GetSettingsQuery();

        var result = await handler.Handle(query, CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.SupportedCurrencies.Should().BeEquivalentTo(["USD", "EUR", "GBP"]);
    }

    [Fact]
    public async Task Handle_GetSettings_WhenSupportedCurrenciesEmpty_ShouldDefaultToMainCurrency()
    {
        var settings = UserSettings.Create(_userId);
        _dbContext.UserSettings.Add(settings);
        await _dbContext.SaveChangesAsync();

        _dbContext.Entry(settings).Property("SupportedCurrencies").CurrentValue = new List<string>();
        await _dbContext.SaveChangesAsync();

        var handler = new GetSettingsQueryHandler(_dbContext, _currentUserProvider);
        var query = new GetSettingsQuery();

        var result = await handler.Handle(query, CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.SupportedCurrencies.Should().BeEquivalentTo(["USD"]);
    }

    [Fact]
    public async Task Handle_GetSettings_WhenNoSettings_ShouldReturnDefaults()
    {
        var handler = new GetSettingsQueryHandler(_dbContext, _currentUserProvider);
        var query = new GetSettingsQuery();

        var result = await handler.Handle(query, CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.MainCurrency.Should().Be("USD");
        result.Value.SupportedCurrencies.Should().BeEquivalentTo(["USD"]);
    }
}
