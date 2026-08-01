using Budgie.Shared.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Budgie.Shared.Infrastructure;

public static class ValueConverters
{
    public static readonly ValueConverter<CurrencyCode, string> CurrencyCodeConverter =
        new(c => c.Code, s => (s == null || s.Length == 0) ? CurrencyCode.From("USD") : CurrencyCode.From(s));

    public static readonly ValueConverter<CurrencyCode?, string?> NullableCurrencyCodeConverter =
        new(c => c == null ? null : c.Code, s => CurrencyCode.FromNullable(s));

    public static readonly ValueConverter<EmailAddress, string> EmailAddressConverter =
        new(e => e.Value, s => EmailAddress.From(s));

    public static readonly ValueConverter<Name, string> NameConverter =
        new(n => n.Value, s => Name.From(s));

    public static readonly ValueConverter<Percentage, decimal> PercentageConverter =
        new(p => p.Value, d => Percentage.From(d));

    public static readonly ValueConverter<HexColor, string> HexColorConverter =
        new(c => c.Value, s => HexColor.From(s));

    public static readonly ValueConverter<HexColor?, string?> NullableHexColorConverter =
        new(c => c == null ? null : c.Value, s => HexColor.TryFrom(s));
}
