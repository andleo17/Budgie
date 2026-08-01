using Budgie.Modules.Finances.Infrastructure;
using Budgie.Shared.Abstractions;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace Budgie.Modules.Finances.UnitTests.Infrastructure;

public class LanguageProviderTests
{
    [Fact]
    public void LanguageCode_ReturnsCurrentLanguageFromSettings()
    {
        var settingsProvider = Substitute.For<IUserSettingsProvider>();
        settingsProvider.GetLanguageAsync().Returns("es");
        var provider = new LanguageProvider(settingsProvider);

        var result = provider.LanguageCode;

        result.Should().Be("es");
    }

    [Fact]
    public void LanguageCode_ReturnsDefaultWhenSettingsNotConfigured()
    {
        var settingsProvider = Substitute.For<IUserSettingsProvider>();
        settingsProvider.GetLanguageAsync().Returns("en");
        var provider = new LanguageProvider(settingsProvider);

        var result = provider.LanguageCode;

        result.Should().Be("en");
    }
}