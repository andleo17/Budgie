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
    // 0 Income, 1 Expense, 3 Transfer, 7 Income (saldo inicial; en cuentas Debt el handler
    // lo re-mapea a Loan — ver GetBalanceSign/BUG 5), 4→null skip (espejo de transferencia:
    // no es fila independiente, su AMOUNT_ACCOUNT alimenta la pata de entrada del par).
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

    // Amount a importar: AMOUNT_ACCOUNT (ya convertido a moneda de cuenta) ?? ZMONEY ?? IN_ZMONEY.
    // El importe de la transacción es AMOUNT_ACCOUNT en la moneda de la cuenta → la transacción
    // se importa SIEMPRE en la moneda de la cuenta con ExchangeRate=1 (BUG 4: sin doble conversión;
    // no se deriva ninguna tasa de AMOUNT_ACCOUNT/ZMONEY porque AMOUNT_ACCOUNT ya está convertido).
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

    // Solo AMOUNT_ACCOUNT (moneda de la cuenta); null si no parseable/cero para que el caller
    // decida el fallback. Se usa para el importe de la pata de ENTRADA de una transferencia
    // (AMOUNT_ACCOUNT del espejo DO_TYPE=4 ya viene en la moneda de la cuenta destino).
    public static decimal? TryMapAccountAmount(MmTransaction tx)
        => TryParseDecimal(tx.AmountAccount, out var amount) && amount != 0 ? Math.Abs(amount) : null;

    // Filtro placeholders ASSETS: el discriminante de Money Manager es ZDATA.
    // ZDATA=1 → cuenta contenedor/placeholder ("Cuentas", "Tarjetas de crédito", "Test"…);
    // ZDATA=0 → cuenta REAL, aunque el nombre coincida con un placeholder ("Efectivo", uid '11',
    // es una cuenta real con transacciones y SÍ se importa). También se descartan nombres vacíos.
    public static bool ShouldSkipAsset(MmAsset asset)
    {
        if (asset.ZData != 0)
            return true;

        return string.IsNullOrWhiteSpace(asset.NicName);
    }

    // ZCATEGORY C_IS_DEL (tri-estado): NULL y 0 = activa, solo 1 = borrada.
    public static bool ShouldSkipCategory(MmCategory category) => category.C_Is_Del == 1;

    // pUid de categoría raíz en Money Manager: '', '0', '-4' o null. Las subcategorías con
    // estos valores deben crearse como raíces (no colgarse de una categoría inexistente).
    public static bool IsRootCategory(string? pUid)
        => string.IsNullOrWhiteSpace(pUid) || pUid.Trim() is "0" or "-4";

    // INOUTCOME DO_TYPE 4 es el espejo de una transferencia DO_TYPE 3 (misma txUidTrans o
    // cuentas invertidas). No se importa como fila independiente: su AMOUNT_ACCOUNT se usa como
    // importe de la pata de ENTRADA del par (BUG 1).
    public static bool ShouldSkipTransaction(MmTransaction tx) => tx.DoType == 4;

    // Busca la fila espejo DO_TYPE=4 de una transferencia DO_TYPE=3.
    // Orden de matching:
    //  1) txUidTrans del espejo == uid de la salida (el espejo apunta a la salida), o
    //  2) txUidTrans compartido (ambas filas del par comparten el mismo valor), o
    //  3) fallback heurístico: misma WDATE, mismo importe y cuentas invertidas.
    public static MmTransaction? FindTransferMirror(MmTransaction outgoing, IReadOnlyList<MmTransaction> candidates)
    {
        MmTransaction? byLink = null;
        MmTransaction? heuristic = null;
        var outgoingAmount = MapAmount(outgoing);

        foreach (var candidate in candidates)
        {
            if (candidate.DoType != 4)
                continue;

            if (!string.IsNullOrWhiteSpace(candidate.TxUidTrans)
                && (string.Equals(candidate.TxUidTrans, outgoing.Uid, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(candidate.TxUidTrans, outgoing.TxUidTrans, StringComparison.OrdinalIgnoreCase)))
            {
                byLink ??= candidate;
                continue;
            }

            if (heuristic is null
                && string.Equals(candidate.AssetUid, outgoing.ToAssetUid, StringComparison.OrdinalIgnoreCase)
                && string.Equals(candidate.ToAssetUid, outgoing.AssetUid, StringComparison.OrdinalIgnoreCase)
                && string.Equals(candidate.WDate?.Trim(), outgoing.WDate?.Trim(), StringComparison.Ordinal)
                && MapAmount(candidate) == outgoingAmount)
            {
                heuristic = candidate;
            }
        }

        return byLink ?? heuristic;
    }

    // Días de cierre/pago de tarjeta de crédito (ASSETS.CARD_DAY_FIN / CARD_DAY_PAY).
    // CreditCardSettings exige 1..31; valores ausentes o fuera de rango → null (caller usa default).
    public static int? MapCreditCardDay(int? day) => day is > 0 and <= 31 ? day : null;

    // Signo del balance al importar una transacción según el tipo de cuenta:
    // - Cuentas normales: Income/Repayment suman; Expense/Transfer/Loan restan
    //   (misma convención que Account.ApplyTransaction).
    // - Cuentas Debt (BUG 5): el balance representa "lo que se debe" en positivo → el signo se
    //   invierte respecto de las normales: una transferencia ENTRANTE (pago de cuota, importada
    //   como Repayment) REDUCE la deuda y tomar prestado (Transfer de salida) la aumenta.
    // - Saldo inicial (DO_TYPE 7) en cuentas Debt: se registra como importe POSITIVO ("lo que
    //   se debe"), aunque en MM venga negativo.
    public static decimal GetBalanceSign(AccountType accountType, TransactionType type, bool isInitialBalance)
    {
        if (isInitialBalance && accountType == AccountType.Debt)
            return +1m;

        var normalSign = type is TransactionType.Income or TransactionType.Repayment ? +1m : -1m;
        return accountType == AccountType.Debt ? -normalSign : normalSign;
    }

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
