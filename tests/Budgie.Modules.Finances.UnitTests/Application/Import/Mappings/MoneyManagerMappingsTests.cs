using Budgie.Modules.Finances.Application.Import;
using Budgie.Modules.Finances.Application.Import.Mappings;
using Budgie.Shared.Enums;
using FluentAssertions;
using Xunit;

namespace Budgie.Modules.Finances.UnitTests.Application.Import.Mappings;

public class MoneyManagerMappingsTests
{
    // --- MapAssetGroupType ---
    [Theory]
    [InlineData(1, AccountType.Checking)]
    [InlineData(2, AccountType.Credit)]
    [InlineData(3, AccountType.Checking)]
    [InlineData(4, AccountType.Savings)]
    [InlineData(5, AccountType.Debt)]
    [InlineData(6, AccountType.Checking)]
    [InlineData(11, AccountType.Cash)]
    [InlineData(99, AccountType.Checking)] // default
    [InlineData(0, AccountType.Checking)]   // default
    public void MapAssetGroupType_KnownAndUnknownTypes_ReturnsExpected(int groupType, AccountType expected)
    {
        MoneyManagerMappings.MapAssetGroupType(groupType).Should().Be(expected);
    }

    // --- MapTransactionType (int) ---
    [Theory]
    [InlineData(0, TransactionType.Income)]
    [InlineData(1, TransactionType.Expense)]
    [InlineData(3, TransactionType.Transfer)]
    [InlineData(7, TransactionType.Income)] // Income + IsInitialBalance
    public void MapTransactionType_ValidDoTypes_ReturnsType(int doType, TransactionType expected)
    {
        MoneyManagerMappings.MapTransactionType(doType).Should().Be(expected);
    }

    [Theory]
    [InlineData(4)] // espejo transfer
    [InlineData(2)]
    [InlineData(5)]
    [InlineData(99)]
    public void MapTransactionType_SkipDoTypes_ReturnsNull(int doType)
    {
        MoneyManagerMappings.MapTransactionType(doType).Should().BeNull();
    }

    // --- MapTransactionType (string) ---
    [Theory]
    [InlineData("3", TransactionType.Transfer)]
    [InlineData("7", TransactionType.Income)]
    public void MapTransactionType_StringValid_ReturnsType(string doTypeRaw, TransactionType expected)
    {
        MoneyManagerMappings.MapTransactionType(doTypeRaw).Should().Be(expected);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("abc")]
    [InlineData("4")]
    public void MapTransactionType_StringInvalidOrSkip_ReturnsNull(string? doTypeRaw)
    {
        MoneyManagerMappings.MapTransactionType(doTypeRaw).Should().BeNull();
    }

    // --- IsInitialBalance ---
    [Theory]
    [InlineData(7, true)]
    [InlineData(0, false)]
    [InlineData(1, false)]
    [InlineData(3, false)]
    public void IsInitialBalance_OnlyDoType7_IsTrue(int doType, bool expected)
    {
        MoneyManagerMappings.IsInitialBalance(doType).Should().Be(expected);
    }

    [Theory]
    [InlineData("7", true)]
    [InlineData("0", false)]
    [InlineData(null, false)]
    [InlineData("junk", false)]
    public void IsInitialBalance_String_Only7_IsTrue(string? doTypeRaw, bool expected)
    {
        MoneyManagerMappings.IsInitialBalance(doTypeRaw).Should().Be(expected);
    }

    // --- TryParseDate ---
    [Fact]
    public void TryParseDate_ValidWdate_ReturnsDate()
    {
        var ok = MoneyManagerMappings.TryParseDate("2025-01-15", "1735689600000", out var date);
        ok.Should().BeTrue();
        date.Should().Be(new DateTime(2025, 1, 15));
        date.Kind.Should().Be(DateTimeKind.Unspecified);
    }

    [Fact]
    public void TryParseDate_ZdateEpochMs_WhenWdateNull_ReturnsDate()
    {
        var ok = MoneyManagerMappings.TryParseDate(null, "1735689600000", out var date);
        ok.Should().BeTrue();
        date.Should().Be(new DateTime(2025, 1, 1));
        date.Kind.Should().Be(DateTimeKind.Unspecified);
    }

    [Fact]
    public void TryParseDate_BothNull_ReturnsFalse()
    {
        var ok = MoneyManagerMappings.TryParseDate(null, null, out var date);
        ok.Should().BeFalse();
        date.Should().Be(default);
    }

    [Fact]
    public void TryParseDate_BothInvalid_ReturnsFalse()
    {
        var ok = MoneyManagerMappings.TryParseDate("not-a-date", "not-epoch", out var date);
        ok.Should().BeFalse();
        date.Should().Be(default);
    }

    [Fact]
    public void TryParseDate_InvalidWdate_InvalidZdate_FallsBackToGenericZdate()
    {
        var ok = MoneyManagerMappings.TryParseDate("junk", "01/20/2025", out var date);
        ok.Should().BeTrue();
        date.Should().Be(new DateTime(2025, 1, 20));
    }

    [Fact]
    public void TryParseDate_OutOfRangeEpoch_FallsThroughToFalse()
    {
        // > DateTimeOffset max (253402300799999 ms) pero < long.MaxValue
        var ok = MoneyManagerMappings.TryParseDate(null, "999999999999999999", out var date);
        ok.Should().BeFalse();
        date.Should().Be(default);
    }

    // --- MapAmount ---
    [Fact]
    public void MapAmount_PrefersAmountAccount_ReturnsAbsolute()
    {
        var tx = Tx(amountAccount: "-150.5", zMoney: "200", inZMoney: "250");
        MoneyManagerMappings.MapAmount(tx).Should().Be(150.5m);
    }

    [Fact]
    public void MapAmount_AmountAccountZero_FallsBackToZMoney()
    {
        var tx = Tx(amountAccount: "0", zMoney: "-75.25", inZMoney: "100");
        MoneyManagerMappings.MapAmount(tx).Should().Be(75.25m);
    }

    [Fact]
    public void MapAmount_AllZero_ReturnsZero()
    {
        var tx = Tx(amountAccount: "0", zMoney: "0", inZMoney: "0");
        MoneyManagerMappings.MapAmount(tx).Should().Be(0m);
    }

    // --- GetExchangeRate ---
    [Fact]
    public void GetExchangeRate_SameCurrency_ReturnsOne()
    {
        var tx = Tx(amountAccount: "100", zMoney: "100", currencyUid: "PEN_PEN");
        MoneyManagerMappings.GetExchangeRate(tx, "PEN").Should().Be(1m);
    }

    [Fact]
    public void GetExchangeRate_MissingIso_ReturnsOne()
    {
        var tx = Tx(amountAccount: "100", zMoney: "100", currencyUid: null);
        MoneyManagerMappings.GetExchangeRate(tx, "PEN").Should().Be(1m);
    }

    [Fact]
    public void GetExchangeRate_DifferentCurrency_DerivesRate()
    {
        // AMOUNT_ACCOUNT 1.32 / ZMONEY 4.87925592 => rate ~0.27
        var tx = Tx(amountAccount: "1.32", zMoney: "4.87925592", currencyUid: "PEN_USD");
        var rate = MoneyManagerMappings.GetExchangeRate(tx, "PEN");
        rate.Should().BeGreaterThan(0.2m);
        rate.Should().BeLessThan(0.3m);
    }

    [Fact]
    public void GetExchangeRate_DifferentCurrency_NonDerivable_ReturnsOne()
    {
        var tx = Tx(amountAccount: "0", zMoney: "0", currencyUid: "PEN_USD");
        MoneyManagerMappings.GetExchangeRate(tx, "PEN").Should().Be(1m);
    }

    // --- ExtractIso ---
    [Theory]
    [InlineData("PEN_PEN", "PEN")]
    [InlineData("PEN_USD", "USD")]
    [InlineData("USD", "USD")]
    [InlineData("pen_usd", "USD")]
    public void ExtractIso_VariousUids_ReturnsLastSegment(string uid, string expected)
    {
        MoneyManagerMappings.ExtractIso(uid).Should().Be(expected);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ExtractIso_Blank_ReturnsNull(string? uid)
    {
        MoneyManagerMappings.ExtractIso(uid).Should().BeNull();
    }

    // --- ShouldSkipAsset ---
    [Fact]
    public void ShouldSkipAsset_ZDataNonZero_ReturnsTrue()
    {
        var asset = new MmAsset("a", "1", 1, "PEN_PEN", "Cuenta", 1, null);
        MoneyManagerMappings.ShouldSkipAsset(asset).Should().BeTrue();
    }

    [Fact]
    public void ShouldSkipAsset_BlankName_ReturnsTrue()
    {
        var asset = new MmAsset("a", "1", 1, "PEN_PEN", "   ", 0, null);
        MoneyManagerMappings.ShouldSkipAsset(asset).Should().BeTrue();
    }

    [Theory]
    [InlineData("Cuentas")]
    [InlineData("Tarjetas")]
    [InlineData("Efectivo")]
    [InlineData("11,Cuentas,2 Tarjetas")]
    [InlineData("11")]
    public void ShouldSkipAsset_PlaceholderName_ReturnsTrue(string name)
    {
        var asset = new MmAsset("a", "1", 1, "PEN_PEN", name, 0, null);
        MoneyManagerMappings.ShouldSkipAsset(asset).Should().BeTrue();
    }

    [Fact]
    public void ShouldSkipAsset_ShortNumericName_ReturnsTrue()
    {
        var asset = new MmAsset("a", "1", 1, "PEN_PEN", "2", 0, null);
        MoneyManagerMappings.ShouldSkipAsset(asset).Should().BeTrue();
    }

    [Fact]
    public void ShouldSkipAsset_RealAccount_ReturnsFalse()
    {
        var asset = new MmAsset("a", "1", 1, "PEN_PEN", "Cuenta Corriente", 0, null);
        MoneyManagerMappings.ShouldSkipAsset(asset).Should().BeFalse();
    }

    // --- ShouldSkipCategory ---
    [Fact]
    public void ShouldSkipCategory_Deleted_ReturnsTrue()
    {
        var cat = new MmCategory("c", "X", 1, null, 1, 0);
        MoneyManagerMappings.ShouldSkipCategory(cat).Should().BeTrue();
    }

    [Fact]
    public void ShouldSkipCategory_Active_ReturnsFalse()
    {
        var cat = new MmCategory("c", "X", 1, null, 0, 0);
        MoneyManagerMappings.ShouldSkipCategory(cat).Should().BeFalse();
    }

    // --- ShouldSkipTransaction ---
    [Fact]
    public void ShouldSkipTransaction_DoType4_ReturnsTrue()
    {
        var tx = Tx(doType: 4);
        MoneyManagerMappings.ShouldSkipTransaction(tx).Should().BeTrue();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(7)]
    public void ShouldSkipTransaction_Non4_ReturnsFalse(int doType)
    {
        var tx = Tx(doType: doType);
        MoneyManagerMappings.ShouldSkipTransaction(tx).Should().BeFalse();
    }

    private static MmTransaction Tx(
        string? amountAccount = "100", string? zMoney = "100", string? inZMoney = "100",
        string? currencyUid = "PEN_PEN", int doType = 1)
        => new("uid", "acc1", "-4", "-4", "1735689600000", "2025-01-01",
            zMoney, inZMoney, amountAccount, currencyUid, "notes", doType, "0");
}
