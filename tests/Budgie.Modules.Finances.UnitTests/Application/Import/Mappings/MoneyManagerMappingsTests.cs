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
    [InlineData(7, TransactionType.Income)] // Income + IsInitialBalance (en Debt el handler re-mapea a Loan)
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

    // --- MapAmount (BUG 4: AMOUNT_ACCOUNT ya está en la moneda de la cuenta; sin doble conversión) ---
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

    // --- TryMapAccountAmount (solo AMOUNT_ACCOUNT, para la pata de entrada de transferencias) ---
    [Fact]
    public void TryMapAccountAmount_Present_ReturnsAbsolute()
    {
        var tx = Tx(amountAccount: "-200.25", zMoney: "999");
        MoneyManagerMappings.TryMapAccountAmount(tx).Should().Be(200.25m);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("0")]
    public void TryMapAccountAmount_MissingOrZero_ReturnsNull(string? amountAccount)
    {
        var tx = Tx(amountAccount: amountAccount);
        MoneyManagerMappings.TryMapAccountAmount(tx).Should().BeNull();
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

    // --- ShouldSkipAsset (BUG 2: el discriminante es ZDATA, no la lista de nombres) ---
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
    [InlineData("Cuentas", 1)]      // placeholder contenedor (ZDATA=1)
    [InlineData("Tarjetas", 1)]
    [InlineData("11,Cuentas,2 Tarjetas", 1)]
    public void ShouldSkipAsset_PlaceholderNameWithZData1_ReturnsTrue(string name, int zData)
    {
        var asset = new MmAsset("a", "1", 1, "PEN_PEN", name, zData, null);
        MoneyManagerMappings.ShouldSkipAsset(asset).Should().BeTrue();
    }

    [Theory]
    [InlineData("Efectivo")]   // cuenta REAL (uid '11' en MM): ZDATA=0 → se importa
    [InlineData("Cuentas")]    // nombre de placeholder pero ZDATA=0 → cuenta real
    [InlineData("11")]         // nombre numérico pero ZDATA=0 → cuenta real
    public void ShouldSkipAsset_RealAccountWithZData0_ReturnsFalse(string name)
    {
        var asset = new MmAsset("a", "1", 11, "PEN_PEN", name, 0, null);
        MoneyManagerMappings.ShouldSkipAsset(asset).Should().BeFalse();
    }

    [Fact]
    public void ShouldSkipAsset_RealAccount_ReturnsFalse()
    {
        var asset = new MmAsset("a", "1", 1, "PEN_PEN", "Cuenta Corriente", 0, null);
        MoneyManagerMappings.ShouldSkipAsset(asset).Should().BeFalse();
    }

    // --- ShouldSkipCategory (BUG 3: NULL/0 = activa, solo 1 = borrada) ---
    [Fact]
    public void ShouldSkipCategory_Deleted_ReturnsTrue()
    {
        var cat = new MmCategory("c", "X", 1, null, 1, 0);
        MoneyManagerMappings.ShouldSkipCategory(cat).Should().BeTrue();
    }

    [Fact]
    public void ShouldSkipCategory_ActiveZero_ReturnsFalse()
    {
        var cat = new MmCategory("c", "X", 1, null, 0, 0);
        MoneyManagerMappings.ShouldSkipCategory(cat).Should().BeFalse();
    }

    [Fact]
    public void ShouldSkipCategory_ActiveNull_ReturnsFalse()
    {
        // Money Manager guarda C_IS_DEL = NULL para categorías activas
        var cat = new MmCategory("c", "X", 1, null, null, 0);
        MoneyManagerMappings.ShouldSkipCategory(cat).Should().BeFalse();
    }

    // --- IsRootCategory (BUG 3: '' / '0' / '-4' / null son raíz) ---
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("0")]
    [InlineData("-4")]
    public void IsRootCategory_RootValues_ReturnsTrue(string? pUid)
    {
        MoneyManagerMappings.IsRootCategory(pUid).Should().BeTrue();
    }

    [Theory]
    [InlineData("cat2")]
    [InlineData("  cat2  ")]
    public void IsRootCategory_ChildParentUid_ReturnsFalse(string pUid)
    {
        MoneyManagerMappings.IsRootCategory(pUid).Should().BeFalse();
    }

    // --- ShouldSkipTransaction ---
    [Fact]
    public void ShouldSkipTransaction_DoType4_ReturnsTrue()
    {
        // El espejo no es fila independiente: su AMOUNT_ACCOUNT alimenta la pata de entrada
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

    // --- FindTransferMirror (BUG 1: emparejar DO_TYPE 3 con su espejo DO_TYPE 4) ---
    [Fact]
    public void FindTransferMirror_MirrorPointsToOutgoing_ByTxUidTrans()
    {
        var outgoing = Tx(doType: 3, assetUid: "acc1", toAssetUid: "acc2", wDate: "2025-01-10", amountAccount: "200");
        var mirror = Tx(uid: "m1", doType: 4, assetUid: "acc2", toAssetUid: "acc1", wDate: "2025-01-10", amountAccount: "200", txUidTrans: "uid");
        var other = Tx(uid: "noise", doType: 1, assetUid: "acc9", toAssetUid: "-4");

        var found = MoneyManagerMappings.FindTransferMirror(outgoing, [mirror, other]);

        found.Should().Be(mirror);
    }

    [Fact]
    public void FindTransferMirror_SharedTxUidTrans_Matches()
    {
        var outgoing = Tx(doType: 3, assetUid: "acc1", toAssetUid: "acc2", wDate: "2025-01-10", amountAccount: "200", txUidTrans: "pair-1");
        var mirror = Tx(uid: "m1", doType: 4, assetUid: "acc2", toAssetUid: "acc1", wDate: "2025-01-10", amountAccount: "350", txUidTrans: "pair-1");

        var found = MoneyManagerMappings.FindTransferMirror(outgoing, [mirror]);

        found.Should().Be(mirror);
    }

    [Fact]
    public void FindTransferMirror_HeuristicFallback_MatchesInvertedPair()
    {
        // Backups antiguos sin TX_UID_TRANS: misma fecha, mismo importe y cuentas invertidas
        var outgoing = Tx(doType: 3, assetUid: "acc1", toAssetUid: "acc2", wDate: "2025-01-10", amountAccount: "200");
        var mirror = Tx(uid: "m1", doType: 4, assetUid: "acc2", toAssetUid: "acc1", wDate: "2025-01-10", amountAccount: "200");

        var found = MoneyManagerMappings.FindTransferMirror(outgoing, [mirror]);

        found.Should().Be(mirror);
    }

    [Fact]
    public void FindTransferMirror_NoCandidate_ReturnsNull()
    {
        var outgoing = Tx(doType: 3, assetUid: "acc1", toAssetUid: "acc2", wDate: "2025-01-10", amountAccount: "200");

        MoneyManagerMappings.FindTransferMirror(outgoing, []).Should().BeNull();
    }

    // --- MapCreditCardDay (BUG 6: CARD_DAY_FIN / CARD_DAY_PAY → CreditCardSettings 1..31) ---
    [Theory]
    [InlineData(20, 20)]
    [InlineData(1, 1)]
    [InlineData(31, 31)]
    public void MapCreditCardDay_ValidDays_ReturnsDay(int day, int expected)
    {
        MoneyManagerMappings.MapCreditCardDay(day).Should().Be(expected);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(32)]
    public void MapCreditCardDay_InvalidDays_ReturnsNull(int? day)
    {
        MoneyManagerMappings.MapCreditCardDay(day).Should().BeNull();
    }

    // --- GetBalanceSign (BUG 4/5: cuentas normales vs cuentas Debt) ---
    [Theory]
    // Cuentas normales: Income/Repayment suman; Expense/Transfer/Loan restan
    [InlineData(AccountType.Checking, TransactionType.Income, false, 1)]
    [InlineData(AccountType.Checking, TransactionType.Repayment, false, 1)]
    [InlineData(AccountType.Checking, TransactionType.Expense, false, -1)]
    [InlineData(AccountType.Checking, TransactionType.Transfer, false, -1)]
    [InlineData(AccountType.Credit, TransactionType.Expense, false, -1)]
    [InlineData(AccountType.Cash, TransactionType.Income, false, 1)]
    // Saldo inicial (DO_TYPE 7) en cuenta normal: Income → suma
    [InlineData(AccountType.Checking, TransactionType.Income, true, 1)]
    // BUG 5: cuentas Debt — el signo se invierte (balance = "lo que se debe" en positivo):
    // pago entrante (Repayment) REDUCE la deuda; Transfer de salida (tomar prestado) la aumenta;
    // Income reduciría (MM: el ingreso acerca el saldo negativo a cero); Expense la aumentaría.
    [InlineData(AccountType.Debt, TransactionType.Repayment, false, -1)]
    [InlineData(AccountType.Debt, TransactionType.Income, false, -1)]
    [InlineData(AccountType.Debt, TransactionType.Transfer, false, 1)]
    [InlineData(AccountType.Debt, TransactionType.Expense, false, 1)]
    // BUG 5: saldo inicial en cuenta Debt → "lo que se debe" siempre en positivo
    [InlineData(AccountType.Debt, TransactionType.Loan, true, 1)]
    [InlineData(AccountType.Debt, TransactionType.Income, true, 1)]
    public void GetBalanceSign_AccountTypeAndType_ReturnsExpected(
        AccountType accountType, TransactionType type, bool isInitialBalance, int expected)
    {
        MoneyManagerMappings.GetBalanceSign(accountType, type, isInitialBalance).Should().Be(expected);
    }

    private static MmTransaction Tx(
        string? uid = "uid",
        int doType = 1,
        string assetUid = "acc1",
        string toAssetUid = "-4",
        string? wDate = "2025-01-01",
        string? amountAccount = "100",
        string? zMoney = "100",
        string? inZMoney = "100",
        string? currencyUid = "PEN_PEN",
        string? txUidTrans = null)
        => new(uid, assetUid, toAssetUid, "-4", "1735689600000", wDate,
            zMoney, inZMoney, amountAccount, currencyUid, "notes", doType, "0", txUidTrans);
}
