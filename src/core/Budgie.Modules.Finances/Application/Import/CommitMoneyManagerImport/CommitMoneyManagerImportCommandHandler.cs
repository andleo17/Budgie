using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using Budgie.Modules.Finances.Application.Import.Mappings;
using Budgie.Modules.Finances.Domain.Accounts;
using Budgie.Modules.Finances.Domain.Categories;
using Budgie.Modules.Finances.Domain.Import;
using Budgie.Modules.Finances.Domain.RecurringTransactions;
using Budgie.Modules.Finances.Domain.Transactions;
using Budgie.Modules.Finances.Infrastructure.Logging;
using Budgie.Shared.Abstractions;
using Budgie.Shared.Domain.ValueObjects;
using Budgie.Shared.Enums;
using Budgie.Shared.Infrastructure;
using ErrorOr;
using Mediator;

namespace Budgie.Modules.Finances.Application.Import.CommitMoneyManagerImport;

// Commit transaccional del .mmbak (plan §6).
// Abre una transacción explícita (via IDbConnectionFactory) y delega TODO el SQL a los
// repositorios (Infrastructure/Database) pasando el IDbTransaction. Este handler NO contiene
// SQL: solo orquesta entidades de dominio y coordina commit/rollback.
//
// NOTA de atomicidad: los repositorios de escritura aceptan un IDbTransaction? opcional como
// último parámetro. Cuando se proporciona, ejecutan sus comandos dentro de la transacción del
// handler (usando tx.Connection); cuando es null (resto de la app) usan su conexión propia.
internal sealed class CommitMoneyManagerImportCommandHandler(
    IMoneyManagerParser parser,
    IAccountRepository accountRepo,
    ICategoryRepository categoryRepo,
    ITransactionRepository txRepo,
    IRecurringTransactionRepository recurringRepo,
    IImportRunRepository importRunRepo,
    ICurrentUserProvider currentUserProvider,
    IDbConnectionFactory factory,
    ILogger<CommitMoneyManagerImportCommandHandler> logger)
    : IRequestHandler<CommitMoneyManagerImportCommand, ErrorOr<CommitMoneyManagerImportResponse>>
{
    private const long MaxFileSizeBytes = 10 * 1024 * 1024; // 10 MB
    private const int MaxAccounts = 100;
    private const int MaxCategories = 200;
    private const int MaxTransactions = 5000;
    private const int MaxNotesLength = 500;

    private static readonly string[] AllowedExtensions = { ".mmbak", ".db", ".sqlite" };

    public async ValueTask<ErrorOr<CommitMoneyManagerImportResponse>> Handle(
        CommitMoneyManagerImportCommand request, CancellationToken ct)
    {
        var userId = currentUserProvider.GetUserId();

        var validation = ValidateFile(request.FileName, request.ContentType, GetLength(request.FileStream));
        if (validation.IsError)
        {
            LogMessages.MoneyManagerValidationFailed(logger, userId, request.FileName, validation.FirstError.Code);
            return validation.FirstError;
        }

        LogMessages.MoneyManagerCommitStarted(logger, userId, request.FileName, request.Overwrite);

        var tempPath = Path.GetTempFileName();
        try
        {
            await CopyToTempAsync(request.FileStream, tempPath, ct);
            // Normalizado a lowercase: ImportRun.Create persiste el hash en minúsculas,
            // por lo que la comprobación de idempotencia debe usar el mismo formato.
            var fileHash = (await ComputeSha256Async(tempPath, ct)).ToLowerInvariant();

            // Idempotencia por FileHash: bloquea re-import salvo overwrite=true
            if (await importRunRepo.ExistsByHashAsync(userId, fileHash, ct) && !request.Overwrite)
            {
                return Error.Conflict("Import.AlreadyImported",
                    CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "en"
                        ? "This backup has already been imported. Re-import with overwrite=true to skip existing entries."
                        : "Esta copia de seguridad ya fue importada. Re-importa con overwrite=true para omitir entradas existentes.");
            }

            MmImportData data;
            try
            {
                data = await parser.ParseAsync(tempPath, ct);
            }
            catch (InvalidImportFileException)
            {
                return Error.Validation("Import.InvalidFile",
                    CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "en"
                        ? "The file is not a valid Money Manager backup."
                        : "El archivo no es una copia de seguridad válida de Money Manager.");
            }

            var limitError = ValidateLimits(data);
            if (limitError.IsError)
                return limitError.FirstError;

            var result = await CommitAsync(userId, data, fileHash, request.FileName, ct);
            LogMessages.MoneyManagerCommitCompleted(logger, userId, request.FileName, fileHash);
            return result;
        }
        finally
        {
            if (File.Exists(tempPath))
                File.Delete(tempPath);
        }
    }

    private async Task<ErrorOr<CommitMoneyManagerImportResponse>> CommitAsync(
        Guid userId, MmImportData data, string fileHash, string fileName, CancellationToken ct)
    {
        var warnings = new List<string>();
        var createdAccounts = 0;
        var skippedAccounts = 0;
        var createdCategories = 0;
        var skippedCategories = 0;
        var createdTransactions = 0;
        var skippedTransactions = 0;
        var createdRecurring = 0;

        // Mapas de idempotencia (ExternalUid → Id) ya persistidos en DB (re-import)
        var existingAccountMap = await accountRepo.GetExternalUidMapAsync(userId, ct);
        var existingCategoryMap = await categoryRepo.GetExternalUidMapAsync(userId, ct);
        var recurringUidToId = await recurringRepo.GetExternalUidMapAsync(userId, ct);

        using var connection = factory.CreateConnection();
        await connection.OpenAsync(ct);
        await using var tx = await connection.BeginTransactionAsync(ct);

        try
        {
            // (a) Merge SupportedCurrencies en UserSettings (jsonb UNION)
            var isos = data.Currencies
                .Select(c => c.Iso)
                .Where(i => !string.IsNullOrWhiteSpace(i))
                .Select(i => i.Trim().ToUpperInvariant())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            await importRunRepo.MergeSupportedCurrenciesAsync(userId, isos, tx, ct);

            // (b) Accounts (filtrar placeholders, ordenar por A_UTIME)
            var assetIsoByUid = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var accountUidToId = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);
            foreach (var (uid, id) in existingAccountMap)
                accountUidToId[uid] = id;
            var accountBalances = new Dictionary<Guid, decimal>();

            var assets = data.Assets
                .Where(a => !MoneyManagerMappings.ShouldSkipAsset(a))
                .OrderBy(a => ParseEpoch(a.A_Utime) ?? DateTime.MaxValue)
                .ToList();

            // Mapa ISO por uid para todas las cuentas (moneda de las transacciones) + tipo de
            // cuenta (necesario para el signo de balance de cuentas Debt — BUG 5).
            var assetTypeByUid = new Dictionary<string, AccountType>(StringComparer.OrdinalIgnoreCase);
            foreach (var asset in assets)
            {
                assetIsoByUid[asset.Uid] = MoneyManagerMappings.ExtractIso(asset.CurrencyUid) ?? "USD";
                assetTypeByUid[asset.Uid] = MoneyManagerMappings.MapAssetGroupType(asset.GroupType);
            }

            foreach (var asset in assets)
            {
                if (accountUidToId.ContainsKey(asset.Uid))
                {
                    skippedAccounts++;
                    continue;
                }

                var currencyIso = assetIsoByUid[asset.Uid];

                var type = assetTypeByUid[asset.Uid];
                var createdAt = ParseEpoch(asset.A_Utime) ?? DateTime.UtcNow;
                var controlStart = ComputeControlStartDate(asset.Uid, data, createdAt);

                // BUG 6: los días de cierre/pago vienen en ASSETS.CARD_DAY_FIN / CARD_DAY_PAY
                // (CMR Visa cierra 20/paga 30, iO Visa 25/31…). Si el backup no los trae o están
                // fuera de rango (CreditCardSettings exige 1..31) se mantiene el default 1/1.
                var billingCycleDay = MoneyManagerMappings.MapCreditCardDay(asset.CardDayFin) ?? 1;
                var paymentDueDay = MoneyManagerMappings.MapCreditCardDay(asset.CardDayPay) ?? 1;

                CreditCardSettings? creditSettings = type == AccountType.Credit
                    ? new CreditCardSettings(0m, CreditConsumptionMode.Total, billingCycleDay, paymentDueDay, 0m, true, CurrencyCode.From(currencyIso))
                    : null;

                var account = Account.Create(
                    userId, asset.NicName.Trim(), currencyIso, 0, type, creditSettings, createdAt, controlStart);
                account.ExternalUid = asset.Uid;

                await accountRepo.AddAsync(account, ct, tx);
                accountUidToId[asset.Uid] = account.Id;
                accountBalances[account.Id] = 0m;
                createdAccounts++;
            }

            // (c) Categories en dos pasadas (padres → hijos) + MonthlyBudget
            var categoryUidToId = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);
            foreach (var (uid, id) in existingCategoryMap)
                categoryUidToId[uid] = id;

            var monthlyBudgetByCat = BuildMonthlyBudgets(data, warnings);
            var activeCategories = data.Categories
                .Where(c => !MoneyManagerMappings.ShouldSkipCategory(c))
                .ToList();

            // Pasada 1: padres (pUid raíz: null / '' / '0' / '-4' / no presente en el lote)
            foreach (var cat in activeCategories)
            {
                if (categoryUidToId.ContainsKey(cat.Uid))
                {
                    skippedCategories++;
                    continue;
                }

                var isParent = MoneyManagerMappings.IsRootCategory(cat.PUid)
                    || !activeCategories.Any(c => string.Equals(c.Uid, cat.PUid, StringComparison.OrdinalIgnoreCase));
                if (!isParent)
                    continue; // se crea en la pasada 2

                var budget = monthlyBudgetByCat.TryGetValue(cat.Uid, out var b) ? (decimal?)b : null;
                var category = Category.Create(userId, cat.Name.Trim(), cat.Type, null, null, null, budget);
                category.ExternalUid = cat.Uid;

                await categoryRepo.AddAsync(category, ct, tx);
                categoryUidToId[cat.Uid] = category.Id;
                createdCategories++;
            }

            // Pasada 2: hijos resolviendo ParentId via mapa ExternalUid → Id
            foreach (var cat in activeCategories)
            {
                // Ya contado/existente en la pasada 1 (padres): no volver a incrementar skipped
                if (categoryUidToId.ContainsKey(cat.Uid))
                    continue;

                Guid? parentId = null;
                if (!MoneyManagerMappings.IsRootCategory(cat.PUid))
                {
                    if (categoryUidToId.TryGetValue(cat.PUid!, out var pid))
                        parentId = pid;
                    else
                        warnings.Add($"Category '{cat.Name}' references missing parent '{cat.PUid}' — parent left empty.");
                }

                var budget = monthlyBudgetByCat.TryGetValue(cat.Uid, out var b) ? (decimal?)b : null;
                var category = Category.Create(userId, cat.Name.Trim(), cat.Type, null, null, parentId, budget);
                category.ExternalUid = cat.Uid;

                await categoryRepo.AddAsync(category, ct, tx);
                categoryUidToId[cat.Uid] = category.Id;
                createdCategories++;
            }

            // (d) Transactions — BUG 1: cada transferencia DO_TYPE 3 genera DOS transacciones
            // enlazadas (salida Type=Transfer + entrada Type=Income con SystemCategoryIds.Transfer).
            // Las filas DO_TYPE 4 son los espejos: no se importan como fila independiente, pero su
            // AMOUNT_ACCOUNT (ya en la moneda de la cuenta destino) alimenta la pata de ENTRADA.
            var seenTxUids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var txsToInsert = new List<Transaction>();
            var mirrors = data.Transactions.Where(t => t.DoType == 4).ToList();
            var ordered = data.Transactions
                .Where(t => !MoneyManagerMappings.ShouldSkipTransaction(t))
                .OrderBy(t => TryParseDateOrMax(t.WDate, t.ZDate))
                .ThenBy(t => t.WDate)
                .ToList();

            foreach (var mmTx in ordered)
            {
                // Pattern match: type no-nullable a partir de aquí
                if (MoneyManagerMappings.MapTransactionType(mmTx.DoType) is not { } type)
                {
                    skippedTransactions++;
                    continue;
                }

                // '-4' en transfer → gasto (consistente con preview)
                if (type == TransactionType.Transfer && string.Equals(mmTx.ToAssetUid, "-4", StringComparison.Ordinal))
                    type = TransactionType.Expense;

                if (string.IsNullOrWhiteSpace(mmTx.Uid))
                {
                    skippedTransactions++;
                    continue;
                }

                if (await txRepo.ExistsByExternalUidAsync(userId, mmTx.Uid, ct) || seenTxUids.Contains(mmTx.Uid))
                {
                    skippedTransactions++;
                    continue;
                }

                if (!accountUidToId.TryGetValue(mmTx.AssetUid, out var accountId))
                {
                    warnings.Add($"Transaction '{mmTx.Uid}' references missing account '{mmTx.AssetUid}' — skipped.");
                    skippedTransactions++;
                    continue;
                }

                Guid? categoryId = null;
                if (!string.IsNullOrWhiteSpace(mmTx.CtgUid) && mmTx.CtgUid != "-4")
                {
                    if (categoryUidToId.TryGetValue(mmTx.CtgUid, out var cid))
                        categoryId = cid;
                    else
                        warnings.Add($"Transaction '{mmTx.Uid}' references missing category '{mmTx.CtgUid}' — category left empty.");
                }

                var amount = MoneyManagerMappings.MapAmount(mmTx);
                if (amount <= 0)
                {
                    warnings.Add($"Transaction '{mmTx.Uid}' has a non-positive amount — skipped.");
                    skippedTransactions++;
                    continue;
                }

                if (!MoneyManagerMappings.TryParseDate(mmTx.WDate, mmTx.ZDate, out var date))
                {
                    warnings.Add($"Transaction '{mmTx.Uid}' has an unparseable date — skipped.");
                    skippedTransactions++;
                    continue;
                }

                // BUG 4: AMOUNT_ACCOUNT ya está expresado en la moneda de la cuenta → la
                // transacción se importa SIEMPRE en la moneda de la cuenta con ExchangeRate=1
                // (antes se aplicaba una tasa derivada de AMOUNT_ACCOUNT/ZMONEY: doble conversión).
                var accountIso = assetIsoByUid[mmTx.AssetUid];
                var accountType = assetTypeByUid.TryGetValue(mmTx.AssetUid, out var originType)
                    ? originType
                    : AccountType.Checking;
                var isInitialBalance = MoneyManagerMappings.IsInitialBalance(mmTx.DoType);
                var notes = Truncate(mmTx.ZContent?.Trim(), MaxNotesLength);

                // BUG 5: el saldo inicial (DO_TYPE 7) de una cuenta Debt viene NEGATIVO en MM
                // (cuenta normal con saldo negativo); en Budgie la cuenta Debt guarda en positivo
                // "lo que se debe" → se registra como Loan con importe positivo, no como ingreso.
                if (isInitialBalance && accountType == AccountType.Debt)
                    type = TransactionType.Loan;

                if (type == TransactionType.Transfer)
                {
                    // Transferencia real: DOS patas enlazadas con el MISMO LinkedTransactionId.
                    var linkedId = Guid.NewGuid();

                    TrackTransaction(txsToInsert, seenTxUids, accountBalances,
                        Transaction.Create(userId, accountId, SystemCategoryIds.Transfer, amount, accountIso, 1m,
                            date, notes, TransactionType.Transfer, installments: 1, linkedTransactionId: linkedId),
                        mmTx.Uid, accountType);

                    if (accountUidToId.TryGetValue(mmTx.ToAssetUid, out var toAccountId))
                    {
                        var mirror = MoneyManagerMappings.FindTransferMirror(mmTx, mirrors);
                        var toIso = assetIsoByUid[mmTx.ToAssetUid];
                        var toType = assetTypeByUid.TryGetValue(mmTx.ToAssetUid, out var destinationType)
                            ? destinationType
                            : AccountType.Checking;

                        // Importe de la ENTRADA: AMOUNT_ACCOUNT del espejo DO_TYPE=4 (ya en la
                        // moneda de la cuenta destino), no IN_ZMONEY (va en la moneda de la
                        // transacción). Sin espejo → importe de la salida como mejor esfuerzo.
                        var incomingAmount = mirror is not null ? MoneyManagerMappings.TryMapAccountAmount(mirror) : null;
                        if (incomingAmount is null)
                        {
                            incomingAmount = amount;
                            warnings.Add($"Transfer '{mmTx.Uid}' has no usable mirror row — incoming leg uses the outgoing amount.");
                        }

                        // BUG 5: una transferencia ENTRANTE a una cuenta de deuda (pago de cuota)
                        // debe REDUCIR el saldo de la deuda → Repayment (signo invertido en Debt),
                        // no Income, que la crecería.
                        var incomingType = toType == AccountType.Debt ? TransactionType.Repayment : TransactionType.Income;

                        var incomingExternalUid = mirror is not null && !string.IsNullOrWhiteSpace(mirror.Uid)
                            ? mirror.Uid
                            : $"{mmTx.Uid}:in";

                        TrackTransaction(txsToInsert, seenTxUids, accountBalances,
                            Transaction.Create(userId, toAccountId, SystemCategoryIds.Transfer, incomingAmount.Value, toIso, 1m,
                                date, notes, incomingType, installments: 1, linkedTransactionId: linkedId),
                            incomingExternalUid, toType);
                    }
                    else
                    {
                        warnings.Add($"Transfer '{mmTx.Uid}' references missing account '{mmTx.ToAssetUid}' — incoming leg skipped.");
                    }

                    continue;
                }

                // Transacción única (income / expense / saldo inicial)
                TrackTransaction(txsToInsert, seenTxUids, accountBalances,
                    Transaction.Create(userId, accountId, categoryId, amount, accountIso, 1m,
                        date, notes, type, installments: 1, isInitialBalance: isInitialBalance),
                    mmTx.Uid, accountType);
            }

            createdTransactions = txsToInsert.Count;
            await txRepo.AddRangeAsync(txsToInsert, ct, tx);

            // (e) Recurring (REPEATTRANSACTION → RecurringTransaction, Frequency conservadora).
            // Idempotente por ExternalUid (re-import con overwrite=true no duplica).
            foreach (var r in data.RepeatTransactions)
            {
                if (!string.IsNullOrWhiteSpace(r.Uid) && recurringUidToId.ContainsKey(r.Uid))
                    continue;

                if (!accountUidToId.TryGetValue(r.AssetUid, out var accountId))
                {
                    warnings.Add($"Recurring transaction '{r.Uid}' references missing account '{r.AssetUid}' — skipped.");
                    continue;
                }

                var type = MoneyManagerMappings.MapTransactionType(r.DoType) ?? TransactionType.Expense;
                if (type == TransactionType.Transfer && string.Equals(r.ToAssetUid, "-4", StringComparison.Ordinal))
                    type = TransactionType.Expense;

                Guid? categoryId = null;
                if (!string.IsNullOrWhiteSpace(r.CtgUid) && r.CtgUid != "-4"
                    && categoryUidToId.TryGetValue(r.CtgUid, out var cid))
                    categoryId = cid;

                var amount = ParseRepeatAmount(r);
                if (amount <= 0)
                {
                    warnings.Add($"Recurring transaction '{r.Uid}' has a non-positive amount — skipped.");
                    continue;
                }

                var accountIso = assetIsoByUid.TryGetValue(r.AssetUid, out var iso) ? iso : null;
                var currency = MoneyManagerMappings.ExtractIso(r.CurrencyUid) ?? accountIso ?? "USD";

                var (freq, ambiguous) = MapFrequency(r.RepeatType);
                if (ambiguous)
                    warnings.Add($"Recurring transaction '{r.Uid}' has ambiguous REPEAT_TYPE {r.RepeatType} — using Monthly.");

                var name = !string.IsNullOrWhiteSpace(r.ZContent) ? r.ZContent!.Trim() : "Recurring transaction";
                var startDate = ParseRepeatDate(r.NextDate) ?? DateTime.UtcNow;

                var recurring = RecurringTransaction.Create(
                    userId, name, accountId, categoryId, amount, currency, 1m, type,
                    r.ZContent, 1, true, RecurringApplicability.Automatic, freq, null, null, null, null, startDate);
                recurring.ExternalUid = r.Uid;

                await recurringRepo.AddAsync(recurring, ct, tx);
                recurringUidToId[r.Uid] = recurring.Id;
                createdRecurring++;
            }

            // (f) Balances: recalcular por cuenta (solo cuentas creadas en este commit)
            foreach (var (accountId, balance) in accountBalances)
                await accountRepo.UpdateBalanceAsync(userId, accountId, balance, ct, tx);

            // (g) ImportRuns (trazabilidad + idempotencia FileHash)
            // En re-import con overwrite=true el run ya existe (UNIQUE UserId+FileHash):
            // se omite el INSERT para no violar la restricción ni duplicar trazabilidad.
            if (!await importRunRepo.ExistsByHashAsync(userId, fileHash, ct))
            {
                var counts = new ImportRunCounts(createdAccounts, createdCategories, createdTransactions, createdRecurring);
                var countsJson = JsonSerializer.Serialize(counts, ImportRunCountsJsonContext.Default.ImportRunCounts);
                var run = ImportRun.Create(userId, fileHash, fileName, countsJson);
                await importRunRepo.AddAsync(run, ct, tx);
            }

            await tx.CommitAsync(ct);

            return new CommitMoneyManagerImportResponse(
                createdAccounts, skippedAccounts,
                createdCategories, skippedCategories,
                createdTransactions, skippedTransactions,
                createdRecurring, warnings, fileHash);
        }
        catch (Exception ex)
        {
            await tx.RollbackAsync(ct);
            LogMessages.MoneyManagerCommitFailed(logger, ex, userId, fileName);
            return Error.Failure("Import.CommitFailed",
                CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "en"
                    ? "The import could not be completed. No changes were saved."
                    : "La importación no pudo completarse. No se guardaron cambios.");
        }
    }

    // --- helpers de validación / parsing (reutilizados de Preview) ---

    private static long GetLength(Stream stream)
    {
        try
        {
            return stream.CanSeek ? stream.Length : 0;
        }
        catch (NotSupportedException)
        {
            return 0;
        }
    }

    private static async Task CopyToTempAsync(Stream source, string tempPath, CancellationToken ct)
    {
        await using var fs = new FileStream(tempPath, FileMode.Create, FileAccess.Write);
        await source.CopyToAsync(fs, ct);
    }

    private static async Task<string> ComputeSha256Async(string tempPath, CancellationToken ct)
    {
        using var sha = SHA256.Create();
        await using var fs = new FileStream(tempPath, FileMode.Open, FileAccess.Read);
        var hashBytes = await sha.ComputeHashAsync(fs, ct);
        return Convert.ToHexString(hashBytes);
    }

    private static ErrorOr<Success> ValidateFile(string fileName, string contentType, long length)
    {
        if (length <= 0 || length > MaxFileSizeBytes)
            return CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "en"
                ? Error.Validation("Import.FileTooLarge", "The file must be between 1 byte and 10 MB.")
                : Error.Validation("Import.FileTooLarge", "El archivo debe tener entre 1 byte y 10 MB.");

        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        if (!AllowedExtensions.Contains(ext))
            return Error.Validation("Import.InvalidFile",
                CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "en"
                    ? "Only .mmbak, .db or .sqlite files are supported."
                    : "Solo se admiten archivos .mmbak, .db o .sqlite.");

        var ct = contentType?.ToLowerInvariant();
        if (!string.IsNullOrWhiteSpace(ct) && ct != "application/octet-stream" && !ct.Contains("sqlite"))
            return Error.Validation("Import.InvalidFile",
                CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "en"
                    ? "Unsupported file content type."
                    : "Tipo de contenido de archivo no soportado.");

        return Result.Success;
    }

    private static ErrorOr<Success> ValidateLimits(MmImportData data)
    {
        if (data.Assets.Count > MaxAccounts || data.Categories.Count > MaxCategories || data.Transactions.Count > MaxTransactions)
            return Error.Validation("Import.LimitExceeded",
                CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "en"
                    ? "The backup exceeds the import limits (100 accounts / 200 categories / 5000 transactions)."
                    : "La copia excede los límites de importación (100 cuentas / 200 categorías / 5000 transacciones).");

        return Result.Success;
    }

    // A_UTIME epoch ms string → DateTime? (unspecified kind, como el resto del dominio)
    private static DateTime? ParseEpoch(string? epochMs)
    {
        if (string.IsNullOrWhiteSpace(epochMs)
            || !long.TryParse(epochMs.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var ms))
            return null;

        try
        {
            return DateTime.SpecifyKind(DateTimeOffset.FromUnixTimeMilliseconds(ms).UtcDateTime, DateTimeKind.Unspecified);
        }
        catch (ArgumentOutOfRangeException)
        {
            return null;
        }
    }

    // ControlStartDate = min WDATE de las transacciones de la cuenta; si no hay → fecha de creación
    private static DateTime ComputeControlStartDate(string assetUid, MmImportData data, DateTime fallback)
    {
        var min = DateTime.MaxValue;
        var found = false;
        foreach (var tx in data.Transactions)
        {
            if (!string.Equals(tx.AssetUid, assetUid, StringComparison.OrdinalIgnoreCase))
                continue;
            if (MoneyManagerMappings.TryParseDate(tx.WDate, tx.ZDate, out var date) && date < min)
            {
                min = date;
                found = true;
            }
        }

        return found ? min : fallback.Date;
    }

    private static DateTime TryParseDateOrMax(string? wdate, string? zdate)
        => MoneyManagerMappings.TryParseDate(wdate, zdate, out var date) ? date : DateTime.MaxValue;

    private static DateTime? ParseRepeatDate(string? raw)
        => string.IsNullOrWhiteSpace(raw)
            ? null
            : MoneyManagerMappings.TryParseDate(raw, null, out var date) ? date : null;

    private static decimal ParseRepeatAmount(MmRepeatTransaction r)
    {
        if (TryParseDecimal(r.AmountAccount, out var amountAccount) && amountAccount != 0)
            return Math.Abs(amountAccount);
        if (TryParseDecimal(r.ZMoney, out var zMoney) && zMoney != 0)
            return Math.Abs(zMoney);
        return 0m;
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

    // Añade una transacción importada al lote, registra su ExternalUid para idempotencia y
    // aplica el signo de balance según el tipo de cuenta (cuentas Debt invierten el signo —
    // BUG 5; saldo inicial en Debt siempre suma "lo que se debe").
    private static void TrackTransaction(
        List<Transaction> txsToInsert,
        HashSet<string> seenTxUids,
        Dictionary<Guid, decimal> accountBalances,
        Transaction tx,
        string externalUid,
        AccountType accountType)
    {
        tx.ExternalUid = externalUid;
        txsToInsert.Add(tx);
        seenTxUids.Add(externalUid);

        var sign = MoneyManagerMappings.GetBalanceSign(accountType, tx.Type, tx.IsInitialBalance);
        if (accountBalances.ContainsKey(tx.AccountId))
            accountBalances[tx.AccountId] += tx.Amount.Amount * sign;
    }

    private static string? Truncate(string? value, int max)
        => value is null ? null : (value.Length <= max ? value : value[..max]);

    // BUDGET_AMOUNT join BUDGET.targetUid → MonthlyBudget max por categoría (warning si múltiple)
    private static Dictionary<string, decimal> BuildMonthlyBudgets(MmImportData data, List<string> warnings)
    {
        var budgetTarget = data.Budgets
            .GroupBy(b => b.Uid)
            .ToDictionary(g => g.Key, g => g.First().TargetUid, StringComparer.OrdinalIgnoreCase);

        var amounts = new Dictionary<string, (decimal Max, int Count)>(StringComparer.OrdinalIgnoreCase);
        foreach (var ba in data.BudgetAmounts)
        {
            if (!budgetTarget.TryGetValue(ba.BudgetUid, out var targetUid) || MoneyManagerMappings.IsRootCategory(targetUid))
                continue;

            if (!amounts.TryGetValue(targetUid, out var cur))
                cur = (0m, 0);
            amounts[targetUid] = (Math.Max(cur.Max, ba.Amount), cur.Count + 1);
        }

        var result = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
        foreach (var (catUid, (max, count)) in amounts)
        {
            result[catUid] = max;
            if (count > 1)
                warnings.Add($"Category '{catUid}' has {count} budgets — using the maximum.");
        }

        return result;
    }

    // REPEAT_TYPE: 1 Daily / 8 Monthly / 16 Yearly (guess); ambiguo → Monthly + warning
    private static (RecurringFrequency Frequency, bool Ambiguous) MapFrequency(int repeatType) => repeatType switch
    {
        1 => (RecurringFrequency.Daily, false),
        8 => (RecurringFrequency.Monthly, false),
        16 => (RecurringFrequency.Yearly, false),
        _ => (RecurringFrequency.Monthly, true)
    };
}
