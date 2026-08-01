using Budgie.Modules.Users.Domain;
using Budgie.Modules.Users.Infrastructure;
using Budgie.Modules.Users.Infrastructure.Database;
using Budgie.Shared.Abstractions;
using Budgie.Shared.Enums;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Xunit;

namespace Budgie.Modules.Users.UnitTests.Infrastructure;

public class UserSettingsProviderTests
{
    private readonly UsersDbContext _dbContext;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly Guid _userId = Guid.NewGuid();

    public UserSettingsProviderTests()
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
    public async Task IsConfiguredAsync_WhenConfigured_ReturnsTrue()
    {
        var settings = UserSettings.Create(_userId);
        settings.Update("USD", CreditConsumptionMode.Total);
        _dbContext.UserSettings.Add(settings);
        await _dbContext.SaveChangesAsync();

        var provider = new UserSettingsProvider(_dbContext, _currentUserProvider);

        var result = await provider.IsConfiguredAsync();

        result.Should().BeTrue();
    }

    [Fact]
    public async Task IsConfiguredAsync_WhenNotConfigured_ReturnsFalse()
    {
        var settings = UserSettings.Create(_userId);
        _dbContext.UserSettings.Add(settings);
        await _dbContext.SaveChangesAsync();

        var provider = new UserSettingsProvider(_dbContext, _currentUserProvider);

        var result = await provider.IsConfiguredAsync();

        result.Should().BeFalse();
    }

    [Fact]
    public async Task IsConfiguredAsync_NoSettings_ReturnsFalse()
    {
        var provider = new UserSettingsProvider(_dbContext, _currentUserProvider);

        var result = await provider.IsConfiguredAsync();

        result.Should().BeFalse();
    }

    [Fact]
    public async Task GetMainCurrencyAsync_ReturnsMainCurrency()
    {
        var settings = UserSettings.Create(_userId);
        settings.Update("EUR", CreditConsumptionMode.Partial);
        _dbContext.UserSettings.Add(settings);
        await _dbContext.SaveChangesAsync();

        var provider = new UserSettingsProvider(_dbContext, _currentUserProvider);

        var result = await provider.GetMainCurrencyAsync();

        result.Should().Be("EUR");
    }

    [Fact]
    public async Task GetMainCurrencyAsync_NoSettings_ReturnsDefaultUsd()
    {
        var provider = new UserSettingsProvider(_dbContext, _currentUserProvider);

        var result = await provider.GetMainCurrencyAsync();

        result.Should().Be("USD");
    }

    [Fact]
    public async Task GetLanguageAsync_ReturnsLanguage()
    {
        var settings = UserSettings.Create(_userId);
        settings.Update("USD", CreditConsumptionMode.Total, language: "fr");
        _dbContext.UserSettings.Add(settings);
        await _dbContext.SaveChangesAsync();

        var provider = new UserSettingsProvider(_dbContext, _currentUserProvider);

        var result = await provider.GetLanguageAsync();

        result.Should().Be("fr");
    }

    [Fact]
    public async Task GetLanguageAsync_NoSettings_ReturnsDefaultEn()
    {
        var provider = new UserSettingsProvider(_dbContext, _currentUserProvider);

        var result = await provider.GetLanguageAsync();

        result.Should().Be("en");
    }
}
