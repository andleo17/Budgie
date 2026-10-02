namespace Budgie.Modules.Finances.Application.Import;

// CURRENCY → UserSettings.SupportedCurrencies (§6.2)
public sealed record MmCurrency(string Iso, decimal Rate, bool IsMain);

// ASSETGROUP — grupos de cuenta (TYPE → AccountType)
public sealed record MmAssetGroup(int Type, string Name);

// ASSETS — cuentas reales (ZDATA=0) + placeholders/contenedores (ZDATA=1) filtrados en mappings.
// CardDayFin/CardDayPay: ASSETS.CARD_DAY_FIN / CARD_DAY_PAY (solo cuentas de crédito; ausentes en
// backups antiguos → null y el caller aplica el default).
public sealed record MmAsset(
    string Uid,
    string GroupUid,
    int GroupType,
    string CurrencyUid,
    string NicName,
    int ZData,
    string? A_Utime,
    int? CardDayFin = null,
    int? CardDayPay = null);

// ZCATEGORY — categorías jerárquicas.
// C_Is_Del es tri-estado como en Money Manager: NULL y 0 = categoría activa, 1 = borrada.
// El parser preserva el NULL (no lo normaliza a 0) para que ShouldSkipCategory solo excluya
// explícitamente las borradas.
public sealed record MmCategory(
    string Uid,
    string Name,
    int Type,
    string? PUid,
    int? C_Is_Del,
    int Status);

// INOUTCOME — transacciones (DO_TYPE 0/1/3/4/7).
// TxUidTrans: INOUTCOME.TX_UID_TRANS — enlaza el par de transferencia DO_TYPE=3 (salida) /
// DO_TYPE=4 (espejo de entrada). Ausente en backups antiguos → null (matching heurístico).
public sealed record MmTransaction(
    string Uid,
    string AssetUid,
    string ToAssetUid,
    string CtgUid,
    string? ZDate,
    string? WDate,
    string? ZMoney,
    string? InZMoney,
    string? AmountAccount,
    string? CurrencyUid,
    string? ZContent,
    int DoType,
    string? Mark,
    string? TxUidTrans = null);

// BUDGET — cabecera (uid = "{targetUid}_1_0_0_6", PERIOD_TYPE=6 mensual)
public sealed record MmBudget(string Uid, string TargetUid);

// BUDGET_AMOUNT — importes por presupuesto (IS_DEL=0, join BUDGET.targetUid = ZCATEGORY.uid)
public sealed record MmBudgetAmount(string BudgetUid, decimal Amount);

// REPEATTRANSACTION — recurrencias (REPEAT_TYPE 1 Daily / 8 Monthly / 16 Yearly)
public sealed record MmRepeatTransaction(
    string Uid,
    string AssetUid,
    string? ToAssetUid,
    string? CtgUid,
    string? ZMoney,
    string? AmountAccount,
    string? CurrencyUid,
    int DoType,
    int RepeatType,
    string? NextDate,
    string? ZContent);

// Agregado de datos parseados del .mmbak (SQLite readonly)
public sealed record MmImportData(
    IReadOnlyList<MmCurrency> Currencies,
    IReadOnlyList<MmAssetGroup> AssetGroups,
    IReadOnlyList<MmAsset> Assets,
    IReadOnlyList<MmCategory> Categories,
    IReadOnlyList<MmTransaction> Transactions,
    IReadOnlyList<MmBudget> Budgets,
    IReadOnlyList<MmBudgetAmount> BudgetAmounts,
    IReadOnlyList<MmRepeatTransaction> RepeatTransactions);
