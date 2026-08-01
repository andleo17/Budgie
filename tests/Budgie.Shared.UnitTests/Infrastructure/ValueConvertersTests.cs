using Budgie.Shared.Domain.ValueObjects;
using Budgie.Shared.Infrastructure;
using FluentAssertions;
using Xunit;

namespace Budgie.Shared.UnitTests.Infrastructure;

public class ValueConvertersTests
{
    [Fact]
    public void CurrencyCodeConverter_ConvertsToProvider()
    {
        var currency = CurrencyCode.From("USD");
        var result = ValueConverters.CurrencyCodeConverter.ConvertToProvider.Invoke(currency);
        result.Should().Be("USD");
    }

    [Fact]
    public void CurrencyCodeConverter_ConvertsFromProvider()
    {
        var result = ValueConverters.CurrencyCodeConverter.ConvertFromProvider.Invoke("USD");
        result.Should().Be(CurrencyCode.From("USD"));
    }

    [Fact]
    public void EmailAddressConverter_ConvertsToProvider()
    {
        var email = EmailAddress.From("test@example.com");
        var result = ValueConverters.EmailAddressConverter.ConvertToProvider.Invoke(email);
        result.Should().Be("test@example.com");
    }

    [Fact]
    public void EmailAddressConverter_ConvertsFromProvider()
    {
        var result = ValueConverters.EmailAddressConverter.ConvertFromProvider.Invoke("test@example.com");
        result.Should().Be(EmailAddress.From("test@example.com"));
    }

    [Fact]
    public void NameConverter_ConvertsToProvider()
    {
        var name = Name.From("John Doe");
        var result = ValueConverters.NameConverter.ConvertToProvider.Invoke(name);
        result.Should().Be("John Doe");
    }

    [Fact]
    public void NameConverter_ConvertsFromProvider()
    {
        var result = ValueConverters.NameConverter.ConvertFromProvider.Invoke("John Doe");
        result.Should().Be(Name.From("John Doe"));
    }

    [Fact]
    public void PercentageConverter_ConvertsToProvider()
    {
        var pct = Percentage.From(25);
        var result = ValueConverters.PercentageConverter.ConvertToProvider.Invoke(pct);
        result.Should().Be(25m);
    }

    [Fact]
    public void PercentageConverter_ConvertsFromProvider()
    {
        var result = ValueConverters.PercentageConverter.ConvertFromProvider.Invoke(25m);
        result.Should().Be(Percentage.From(25));
    }

    [Fact]
    public void HexColorConverter_ConvertsToProvider()
    {
        var color = HexColor.From("#FF0000");
        var result = ValueConverters.HexColorConverter.ConvertToProvider.Invoke(color);
        result.Should().Be("#FF0000");
    }

    [Fact]
    public void HexColorConverter_ConvertsFromProvider()
    {
        var result = ValueConverters.HexColorConverter.ConvertFromProvider.Invoke("#FF0000");
        result.Should().Be(HexColor.From("#FF0000"));
    }
}