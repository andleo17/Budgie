using Budgie.Modules.Finances.Application.Categories;
using Budgie.Modules.Finances.Domain.Categories;
using FluentAssertions;

namespace Budgie.Modules.Finances.UnitTests.Application.Categories;

public class SystemCategoryNamesTests
{
    // --- Resolve: sistema + idioma español ---
    [Theory]
    [InlineData("A1000000-0000-0000-0000-000000000001", "Transferencia")]
    [InlineData("A1000000-0000-0000-0000-000000000002", "Comisión de transferencia")]
    [InlineData("A1000000-0000-0000-0000-000000000003", "Reembolso de préstamo")]
    [InlineData("A1000000-0000-0000-0000-000000000004", "Ajuste")]
    [InlineData("A1000000-0000-0000-0000-000000000005", "Interés")]
    [InlineData("A1000000-0000-0000-0000-000000000006", "Saldo inicial")]
    public void Resolve_SystemCategory_SpanishLanguage_ReturnsSpanishName(string categoryId, string expected)
    {
        var result = SystemCategoryNames.Resolve(Guid.Parse(categoryId), "Initial Balance", "es");

        result.Should().Be(expected);
    }

    [Theory]
    [InlineData("es-MX")]
    [InlineData("es-419")]
    [InlineData("ES")]
    public void Resolve_SystemCategory_SpanishVariant_ReturnsSpanishName(string language)
    {
        var result = SystemCategoryNames.Resolve(SystemCategoryIds.InitialBalance, "Initial Balance", language);

        result.Should().Be("Saldo inicial");
    }

    // --- Resolve: sistema + idioma no español → inglés (fallback) ---
    [Theory]
    [InlineData("en")]
    [InlineData("fr")]
    [InlineData("pt-BR")]
    public void Resolve_SystemCategory_NonSpanishLanguage_ReturnsEnglishName(string language)
    {
        var result = SystemCategoryNames.Resolve(SystemCategoryIds.InitialBalance, "Initial Balance", language);

        result.Should().Be("Initial Balance");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Resolve_SystemCategory_NullOrBlankLanguage_ReturnsEnglishName(string? language)
    {
        var result = SystemCategoryNames.Resolve(SystemCategoryIds.Transfer, "Transfer", language);

        result.Should().Be("Transfer");
    }

    [Fact]
    public void Resolve_SystemCategory_IgnoresDbName_WhenTranslated()
    {
        // El nombre de BD no importa para categorías de sistema: se resuelve por el diccionario.
        var result = SystemCategoryNames.Resolve(SystemCategoryIds.InitialBalance, "whatever from db", "es");

        result.Should().Be("Saldo inicial");
    }

    // --- Resolve: categorías no-sistema devuelven el nombre de BD tal cual ---
    [Fact]
    public void Resolve_NonSystemCategory_ReturnsDbNameUnchanged()
    {
        var categoryId = Guid.Parse("b0000000-0000-0000-0000-000000000001"); // "Food" (default user category)

        SystemCategoryNames.Resolve(categoryId, "Food", "es").Should().Be("Food");
        SystemCategoryNames.Resolve(categoryId, "Comida", "en").Should().Be("Comida");
    }

    [Theory]
    [InlineData("es")]
    [InlineData("en")]
    public void Resolve_NullCategoryId_ReturnsDbNameUnchanged(string language)
    {
        SystemCategoryNames.Resolve(null, "Custom", language).Should().Be("Custom");
    }

    [Theory]
    [InlineData("es")]
    [InlineData("en")]
    public void Resolve_NullCategoryIdAndNullDbName_ReturnsNull(string language)
    {
        SystemCategoryNames.Resolve(null, null, language).Should().BeNull();
    }

    // --- IsSystemCategory ---
    [Theory]
    [InlineData("A1000000-0000-0000-0000-000000000001")]
    [InlineData("A1000000-0000-0000-0000-000000000002")]
    [InlineData("A1000000-0000-0000-0000-000000000003")]
    [InlineData("A1000000-0000-0000-0000-000000000004")]
    [InlineData("A1000000-0000-0000-0000-000000000005")]
    [InlineData("A1000000-0000-0000-0000-000000000006")]
    public void IsSystemCategory_SystemIds_ReturnsTrue(string categoryId)
    {
        SystemCategoryNames.IsSystemCategory(Guid.Parse(categoryId)).Should().BeTrue();
    }

    [Fact]
    public void IsSystemCategory_NonSystemOrNull_ReturnsFalse()
    {
        SystemCategoryNames.IsSystemCategory(Guid.Parse("b0000000-0000-0000-0000-000000000001")).Should().BeFalse();
        SystemCategoryNames.IsSystemCategory(Guid.Empty).Should().BeFalse();
        SystemCategoryNames.IsSystemCategory(null).Should().BeFalse();
    }
}
