using Budgie.Modules.Finances.Infrastructure;
using FluentAssertions;
using Xunit;

namespace Budgie.Modules.Finances.UnitTests.Infrastructure;

public class TranslationServiceTests
{
    private readonly TranslationService _service = new();

    [Fact]
    public void Translate_ReturnsTranslatedTextForSupportedLanguage()
    {
        var result = _service.Translate("transfer.from_account", "es", "Cuenta A");

        result.Should().Be("Transferencia desde Cuenta A");
    }

    [Fact]
    public void Translate_FallsBackToDefaultForUnsupportedLanguage()
    {
        var result = _service.Translate("adjustment", "fr");

        result.Should().Be("Adjustment");
    }

    [Fact]
    public void Translate_ReturnsKeyWhenTranslationNotFound()
    {
        var result = _service.Translate("nonexistent.key", "en");

        result.Should().Be("nonexistent.key");
    }
}