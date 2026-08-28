using System.Globalization;
using System.Security.Cryptography;
using Budgie.Modules.Finances.Application.Import.Mappings;
using Budgie.Modules.Finances.Infrastructure.Logging;
using Budgie.Shared.Abstractions;
using Budgie.Shared.Enums;
using ErrorOr;
using Mediator;

namespace Budgie.Modules.Finances.Application.Import.PreviewMoneyManagerImport;

// Dry-run Preview del .mmbak: valida archivo, parsea readonly y reporta
// conteos/warnings/errors SIN escribir en DB (solo lecturas de validación).
internal sealed class PreviewMoneyManagerImportCommandHandler(
    IMoneyManagerParser parser,
    IImportRunRepository importRunRepo,
    ICurrentUserProvider currentUserProvider,
    ILogger<PreviewMoneyManagerImportCommandHandler> logger)
    : IRequestHandler<PreviewMoneyManagerImportCommand, ErrorOr<PreviewMoneyManagerImportResponse>>
{
    private const long MaxFileSizeBytes = 10 * 1024 * 1024; // 10 MB
    private const int MaxAccounts = 100;
    private const int MaxCategories = 200;
    private const int MaxTransactions = 5000;

    private static readonly string[] AllowedExtensions = { ".mmbak", ".db", ".sqlite" };

    public async ValueTask<ErrorOr<PreviewMoneyManagerImportResponse>> Handle(
        PreviewMoneyManagerImportCommand request, CancellationToken ct)
    {
        var userId = currentUserProvider.GetUserId();

        var validation = ValidateFile(request.FileName, request.ContentType, GetLength(request.FileStream));
        if (validation.IsError)
        {
            LogMessages.MoneyManagerValidationFailed(logger, userId, request.FileName, validation.FirstError.Code);
            return validation.FirstError;
        }

        LogMessages.MoneyManagerPreviewStarted(logger, userId, request.FileName);

        var tempPath = Path.GetTempFileName();
        try
        {
            await CopyToTempAsync(request.FileStream, tempPath, ct);
            // Normalizado a lowercase: ImportRun.Create persiste el hash en minúsculas,
            // por lo que la comprobación de idempotencia debe usar el mismo formato.
            var fileHash = (await ComputeSha256Async(tempPath, ct)).ToLowerInvariant();

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

            var preview = await BuildPreview(data, fileHash, userId, ct);
            LogMessages.MoneyManagerPreviewCompleted(logger, userId, request.FileName, fileHash);
            return preview;
        }
        finally
        {
            if (File.Exists(tempPath))
                File.Delete(tempPath);
        }
    }

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

    private async Task<PreviewMoneyManagerImportResponse> BuildPreview(
        MmImportData data, string fileHash, Guid userId, CancellationToken ct)
    {
        var warnings = new List<string>();
        var errors = new List<string>();

        // --- CURRENCY ---
        var currencies = data.Currencies
            .Select(c => new PreviewCurrency(c.Iso, c.Rate, c.IsMain))
            .ToList();

        // --- ASSETS → cuentas (filtra placeholders/ZDATA=1) ---
        var skippedPlaceholders = 0;
        var accounts = new List<PreviewAccount>();
        var assetCurrencyByUid = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var asset in data.Assets)
        {
            if (MoneyManagerMappings.ShouldSkipAsset(asset))
            {
                skippedPlaceholders++;
                continue;
            }

            var currencyIso = MoneyManagerMappings.ExtractIso(asset.CurrencyUid) ?? "USD";
            assetCurrencyByUid[asset.Uid] = currencyIso;
            accounts.Add(new PreviewAccount(
                asset.NicName.Trim(),
                MoneyManagerMappings.MapAssetGroupType(asset.GroupType).ToString(),
                currencyIso,
                asset.Uid));
        }

        // --- ZCATEGORY → categorías (2 pasadas para resolver ParentId) ---
        var activeCategories = data.Categories
            .Where(c => !MoneyManagerMappings.ShouldSkipCategory(c))
            .ToList();

        var categoryUidSet = new HashSet<string>(activeCategories.Select(c => c.Uid), StringComparer.OrdinalIgnoreCase);
        var monthlyBudgetByCat = BuildMonthlyBudgets(data, warnings);

        var categories = new List<PreviewCategory>();
        foreach (var cat in activeCategories)
        {
            string? parentUid = cat.PUid;
            if (!string.IsNullOrWhiteSpace(cat.PUid) && cat.PUid != "-4" && !categoryUidSet.Contains(cat.PUid))
            {
                warnings.Add($"Category '{cat.Name}' references missing parent '{cat.PUid}' — parent left empty.");
                parentUid = null; // pUid huérfano → ParentId null
            }

            decimal? budget = monthlyBudgetByCat.TryGetValue(cat.Uid, out var b) ? b : null;
            categories.Add(new PreviewCategory(cat.Name, cat.Type.ToString(), parentUid, cat.Uid, budget));
        }

        // --- INOUTCOME → transacciones (dedup transfer) ---
        var summary = BuildTransactionsSummary(data, assetCurrencyByUid, warnings, errors);

        // --- BUDGETS ---
        var budgets = monthlyBudgetByCat
            .Select(kv => new PreviewBudget(kv.Key, kv.Value))
            .OrderBy(b => b.CategoryUid)
            .ToList();

        // --- REPEATTRANSACTION → recurrencias ---
        var recurrings = new List<PreviewRecurring>();
        foreach (var r in data.RepeatTransactions)
        {
            var (freq, ambiguous) = MapFrequency(r.RepeatType);
            if (ambiguous)
                warnings.Add($"Recurring transaction '{r.Uid}' has ambiguous REPEAT_TYPE {r.RepeatType} — using Monthly.");
            recurrings.Add(new PreviewRecurring(freq.ToString(), r.ZContent));
        }

        // --- Idempotencia: hash ya importado (warning, no error) ---
        var alreadyImported = await importRunRepo.ExistsByHashAsync(userId, fileHash, ct);
        if (alreadyImported)
            warnings.Add("This backup has already been imported before. Re-importing may duplicate or skip entries.");

        return new PreviewMoneyManagerImportResponse(
            currencies,
            accounts,
            categories,
            summary,
            budgets,
            recurrings,
            warnings,
            errors,
            skippedPlaceholders,
            fileHash,
            alreadyImported);
    }

    private static PreviewTransactionsSummary BuildTransactionsSummary(
        MmImportData data,
        Dictionary<string, string> assetCurrencyByUid,
        List<string> warnings,
        List<string> errors)
    {
        int income = 0, expense = 0, transfer = 0, initialBalance = 0;
        var dedupedTransfers = 0;
        var dateMin = DateTime.MaxValue;
        var dateMax = DateTime.MinValue;
        var today = DateTime.Today;

        var allAssetUids = new HashSet<string>(data.Assets.Select(a => a.Uid), StringComparer.OrdinalIgnoreCase);

        // Espejos DO_TYPE 4: clave invertida (WDATE, ZMONEY, toAssetUid, assetUid)
        var mirrorKeys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var tx in data.Transactions)
        {
            if (tx.DoType != 4 || tx.ToAssetUid == "-4") continue;
            var amount = MoneyManagerMappings.MapAmount(tx);
            mirrorKeys.Add(BuildTransferKey(tx.WDate, amount, tx.ToAssetUid, tx.AssetUid));
        }

        foreach (var tx in data.Transactions)
        {
            if (tx.DoType == 4)
            {
                // Espejo de transfer → se deduplica (solo si es un transfer real)
                if (tx.ToAssetUid != "-4") dedupedTransfers++;
                continue;
            }

            var type = MoneyManagerMappings.MapTransactionType(tx.DoType);
            if (type is null) continue;

            // Cuenta huérfana → error no bloqueante (preview reporta)
            if (!allAssetUids.Contains(tx.AssetUid))
                errors.Add($"Transaction '{tx.Uid}' references missing account '{tx.AssetUid}'.");

            if (MoneyManagerMappings.TryParseDate(tx.WDate, tx.ZDate, out var date))
            {
                if (date < dateMin) dateMin = date;
                if (date > dateMax) dateMax = date;
                if (date.Date > today)
                    warnings.Add($"Transaction '{tx.Uid}' has future date {date:yyyy-MM-dd}.");
            }

            // ExchangeRate fallback warning (currency mismatch sin rate derivable)
            var accountIso = assetCurrencyByUid.TryGetValue(tx.AssetUid, out var iso) ? iso : null;
            var txIso = MoneyManagerMappings.ExtractIso(tx.CurrencyUid);
            if (!string.IsNullOrWhiteSpace(txIso) && !string.IsNullOrWhiteSpace(accountIso)
                && !string.Equals(txIso, accountIso, StringComparison.OrdinalIgnoreCase)
                && MoneyManagerMappings.GetExchangeRate(tx, accountIso) <= 1m)
            {
                warnings.Add($"Transaction '{tx.Uid}' uses currency {txIso} but its account is {accountIso} — exchange rate could not be derived, using 1.");
            }

            switch (type)
            {
                case TransactionType.Income:
                    income++;
                    if (MoneyManagerMappings.IsInitialBalance(tx.DoType)) initialBalance++;
                    break;
                case TransactionType.Expense:
                    expense++;
                    break;
                case TransactionType.Transfer:
                    if (tx.ToAssetUid == "-4")
                    {
                        // '-4' no es una transferencia real → se cuenta como gasto
                        expense++;
                    }
                    else
                    {
                        var amount = MoneyManagerMappings.MapAmount(tx);
                        var key = BuildTransferKey(tx.WDate, amount, tx.AssetUid, tx.ToAssetUid);
                        if (mirrorKeys.Contains(key))
                        {
                            // Par espejo encontrado → no duplicar (ya contado vía DO_TYPE 4 dedup)
                            transfer++;
                        }
                        else
                        {
                            transfer++;
                        }
                    }
                    break;
            }
        }

        var total = income + expense + transfer;
        var dateRange = (dateMin == DateTime.MaxValue || dateMax == DateTime.MinValue)
            ? string.Empty
            : $"{dateMin:yyyy-MM-dd} → {dateMax:yyyy-MM-dd}";

        return new PreviewTransactionsSummary(income, expense, transfer, initialBalance, total, dedupedTransfers, dateRange);
    }

    // Clave de agrupación de transfer (WDATE, ZMONEY, assetUid, toAssetUid)
    private static string BuildTransferKey(string? wdate, decimal amount, string assetUid, string toAssetUid)
        => $"{wdate?.Trim()}|{amount.ToString(CultureInfo.InvariantCulture)}|{assetUid}|{toAssetUid}";

    // BUDGET_AMOUNT join BUDGET.targetUid → MonthlyBudget max por categoría (warning si múltiple)
    private static Dictionary<string, decimal> BuildMonthlyBudgets(MmImportData data, List<string> warnings)
    {
        var budgetTarget = data.Budgets
            .GroupBy(b => b.Uid)
            .ToDictionary(g => g.Key, g => g.First().TargetUid, StringComparer.OrdinalIgnoreCase);

        var amounts = new Dictionary<string, (decimal Max, int Count)>(StringComparer.OrdinalIgnoreCase);
        foreach (var ba in data.BudgetAmounts)
        {
            if (!budgetTarget.TryGetValue(ba.BudgetUid, out var targetUid) || targetUid == "-4")
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
