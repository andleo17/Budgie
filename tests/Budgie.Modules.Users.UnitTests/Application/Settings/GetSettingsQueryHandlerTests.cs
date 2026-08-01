using Budgie.Modules.Users.Application.Settings;
using Budgie.Modules.Users.Application.Settings.GetSettings;
using Budgie.Modules.Users.Domain;
using Budgie.Modules.Users.Infrastructure.Database;
using Budgie.Shared.Abstractions;
using Budgie.Shared.Enums;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Xunit;

namespace Budgie.Modules.Users.UnitTests.Application.Settings;

public class GetSettingsQueryHandlerTests
{
    private readonly UsersDbContext _dbContext;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly Guid _userId = Guid.NewGuid();

    public GetSettingsQueryHandlerTests()
    {
        _currentUserProvider = Substitute.For<ICurrentUserProvider>();
        _currentUserProvider.UserId.Returns(_userId);
        _currentUserProvider.GetUserId().Returns(_userId);

        var options = new DbContextOptionsBuilder<UsersDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _dbContext = new UsersDbContext(options);
    }

    [Fact]
    public async Task Handle_SettingsExist_ReturnsSettings()
    {
        var settings = UserSettings.Create(_userId);
        settings.Update("EUR", CreditConsumptionMode.Partial, language: "es");
        _dbContext.UserSettings.Add(settings);
        await _dbContext.SaveChangesAsync();

        var handler = new GetSettingsQueryHandler(_dbContext, _currentUserProvider);
        var query = new GetSettingsQuery();

        var result = await handler.Handle(query, CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.MainCurrency.Should().Be("EUR");
        result.Value.DefaultCreditConsumptionMode.Should().Be(CreditConsumptionMode.Partial);
        result.Value.Language.Should().Be("es");
        result.Value.IsConfigured.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_SettingsExistWithSupportedCurrencies_ReturnsThem()
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
    public async Task Handle_SettingsWithEmptySupportedCurrencies_DefaultsToMainCurrency()
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
    public async Task Handle_NoSettings_ReturnsDefaultSettings()
    {
        var handler = new GetSettingsQueryHandler(_dbContext, _currentUserProvider);
        var query = new GetSettingsQuery();

        var result = await handler.Handle(query, CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.MainCurrency.Should().Be("USD");
        result.Value.DefaultCreditConsumptionMode.Should().Be(CreditConsumptionMode.Total);
        result.Value.Language.Should().Be("en");
        result.Value.IsConfigured.Should().BeFalse();
        result.Value.SupportedCurrencies.Should().BeEquivalentTo(["USD"]);
        result.Value.TransactionPageSize.Should().Be(100);
    }
}
