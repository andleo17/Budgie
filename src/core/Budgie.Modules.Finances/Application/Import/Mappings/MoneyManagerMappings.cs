using System.Globalization;
using Budgie.Modules.Finances.Application.Import;
using Budgie.Shared.Enums;

namespace Budgie.Modules.Finances.Application.Import.Mappings;

// Mapeos puros Money Manager → Budgie (§6.2 spec + plan §2).
// Sin SQL, sin Dapper, sin I/O. Testeables unitariamente.
public static class MoneyManagerMappings
{
    // ASSETGROUP.TYPE → AccountType
    // 1 Cuentas→Checking, 2 Tarj.crédito→Credit, 3 Débito→Checking,
    // 4 Ahorros→Savings, 5 Préstamo→Debt, 6 Prepago→Checking, 11 Efectivo→Cash, otros→Checking
    public static AccountType MapAssetGroupType(int groupType) => groupType switch
    {
        1 => AccountType.Checking,
        2 => AccountType.Credit,
        3 => AccountType.Checking,
        4 => AccountType.Savings,
        5 => AccountType.Debt,
        6 => AccountType.Checking,
        11 => AccountType.Cash,
        _ => AccountType.Checking
    };

    // INOUTCOME DO_TYPE → TransactionType? (null = skip)
    // 0 Income, 1 Expense, 3 Transfer, 7 Income+IsInitialBalance, 4→null skip (deduplicado)
    public static TransactionType? MapTransactionType(int doType) => doType switch
    {
        0 => TransactionType.Income,
        1 => TransactionType.Expense,
        3 => TransactionType.Transfer,
        7 => TransactionType.Income,
        4 => null,
        _ => null
    };

    public static TransactionType? MapTransactionType(string? doTypeRaw)
    {
        if (!int.TryParse(doTypeRaw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var doType))
            return null;

        return MapTransactionType(doType);
    }

    // DO_TYPE 7 es saldo inicial (IsInitialBalance=true)
    public static bool IsInitialBalance(int doType) => doType == 7;

    public static bool IsInitialBalance(string? doTypeRaw)
    {
        if (!int.TryParse(doTypeRaw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var doType))
            return false;

        return IsInitialBalance(doType);
    }

    // WDATE "yyyy-MM-dd" → DateTime, fallback ZDATE epoch ms string
    public static bool TryParseDate(string? wdate, string? zdate, out DateTime date)
    {
        if (!string.IsNullOrWhiteSpace(wdate))
        {
            var trimmed = wdate.Trim();
            if (DateTime.TryParseExact(trimmed, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var wParsed))
            {
                date = DateTime.SpecifyKind(wParsed, DateTimeKind.Unspecified);
                return true;
            }

            // Fallback: TryParse genérico por si WDATE viene con otro formato
            if (DateTime.TryParse(trimmed, CultureInfo.InvariantCulture, DateTimeStyles.None, out var wGeneric))
            {
                date = DateTime.SpecifyKind(wGeneric.Date, DateTimeKind.Unspecified);
                return true;
            }
        }

        if (!string.IsNullOrWhiteSpace(zdate))
        {
            var zTrimmed = zdate.Trim();
            if (long.TryParse(zTrimmed, NumberStyles.Integer, CultureInfo.InvariantCulture, out var epochMs))
            {
                try
                {
                    var dto = DateTimeOffset.FromUnixTimeMilliseconds(epochMs);
                    date = DateTime.SpecifyKind(dto.UtcDateTime, DateTimeKind.Unspecified);
                    return true;
                }
                catch (ArgumentOutOfRangeException)
                {
                    // epoch fuera de rango → fall through
                }
            }

            if (DateTime.TryParse(zTrimmed, CultureInfo.InvariantCulture, DateTimeStyles.None, out var zGeneric))
            {
                date = DateTime.SpecifyKind(zGeneric, DateTimeKind.Unspecified);
                return true;
            }
        }

        date = default;
        return false;
    }

    // Amount a importar: AMOUNT_ACCOUNT (ya convertido a moneda de cuenta) ?? ZMONEY
    public static decimal MapAmount(MmTransaction tx)
    {
        if (TryParseDecimal(tx.AmountAccount, out var amountAccount) && amountAccount != 0)
            return Math.Abs(amountAccount);

        if (TryParseDecimal(tx.ZMoney, out var zMoney) && zMoney != 0)
            return Math.Abs(zMoney);

        if (TryParseDecimal(tx.InZMoney, out var inZMoney) && inZMoney != 0)
            return Math.Abs(inZMoney);

        return 0m;
    }

    // ExchangeRate derivado §6.2:
    // Si currencyUid coincide con Account.Currency → 1
    // Si diverge → AMOUNT_ACCOUNT / ZMONEY (si calculable) else 1
    public static decimal GetExchangeRate(MmTransaction tx, string accountCurrencyIso)
    {
        var txIso = ExtractIso(tx.CurrencyUid);
        if (string.IsNullOrWhiteSpace(txIso) || string.IsNullOrWhiteSpace(accountCurrencyIso))
            return 1m;

        if (string.Equals(txIso.Trim(), accountCurrencyIso.Trim(), StringComparison.OrdinalIgnoreCase))
            return 1m;

        if (TryParseDecimal(tx.AmountAccount, out var amountAccount)
            && TryParseDecimal(tx.ZMoney, out var zMoney)
            && zMoney != 0)
        {
            var rate = amountAccount / zMoney;
            if (rate > 0 && rate < 1000 && rate > 0.0001m)
                return Math.Abs(rate);
        }

        // InZMoney fallback para casos multi-moneda (ej PEN_USD 4.87→1.32 rate 0.27)
        if (TryParseDecimal(tx.InZMoney, out var inZ) && TryParseDecimal(tx.ZMoney, out var z2) && z2 != 0)
        {
            var rate = inZ / z2;
            if (rate > 0 && rate < 1000 && rate > 0.0001m)
                return Math.Abs(rate);
        }

        return 1m;
    }

    // Filtro placeholders ASSETS: ZDATA=1 o NIC_NAME placeholder → skip
    private static readonly HashSet<string> PlaceholderNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "Cuentas",
        "Tarjetas",
        "Tarjetas de crédito",
        "Ahorros",
        "Efectivo",
        "Préstamo",
        "Préstamos",
        "Prepago",
        "Débito",
        "11",
        "2",
        "4",
        "5",
        "6",
        "11,Cuentas,2 Tarjetas"
    };

    public static bool ShouldSkipAsset(MmAsset asset)
    {
        if (asset.ZData != 0)
            return true;

        var name = asset.NicName?.Trim();
        if (string.IsNullOrWhiteSpace(name))
            return true;

        if (PlaceholderNames.Contains(name))
            return true;

        // MM placeholder abreviado numérico ("11" etc.) ya cubierto, pero por si viene con espacios
        if (name.Length <= 2 && int.TryParse(name, NumberStyles.Integer, CultureInfo.InvariantCulture, out _))
            return true;

        return false;
    }

    // ZCATEGORY C_IS_DEL=1 → skip (borradas)
    public static bool ShouldSkipCategory(MmCategory category) => category.C_Is_Del != 0;

    // INOUTCOME DO_TYPE 4 es espejo de transfer → skip (se importa solo DO_TYPE 3 canónico)
    public static bool ShouldSkipTransaction(MmTransaction tx) => tx.DoType == 4;

    // Helper iso: currencyUid "PEN_PEN" → "PEN" (parte tras último '_'), si no contiene '_' → tal cual
    public static string? ExtractIso(string? currencyUid)
    {
        if (string.IsNullOrWhiteSpace(currencyUid))
            return null;

        var trimmed = currencyUid.Trim();
        var idx = trimmed.LastIndexOf('_');
        if (idx >= 0 && idx < trimmed.Length - 1)
            return trimmed[(idx + 1)..].ToUpperInvariant();

        return trimmed.ToUpperInvariant();
    }

    private static bool TryParseDecimal(string? raw, out decimal value)
    {
        if (!string.IsNullOrWhiteSpace(raw)
            && decimal.TryParse(raw.Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed))
        {
            value = parsed;
            return true;
        }

        value = 0m;
        return false;
    }
}
