using System.Globalization;
using Budgie.Modules.Finances.Application.Import;
using Budgie.Modules.Finances.Infrastructure.Logging;
using Microsoft.Data.Sqlite;

namespace Budgie.Modules.Finances.Infrastructure.Import;

internal sealed class MoneyManagerSqliteParser(ILogger<MoneyManagerSqliteParser> logger) : IMoneyManagerParser
{
    private const long MaxFileSizeBytes = 10 * 1024 * 1024;
    private static readonly byte[] SqliteMagic = "SQLite format 3\0"u8.ToArray();
    private static readonly HashSet<string> RequiredTables = new(StringComparer.OrdinalIgnoreCase)
    {
        "INOUTCOME",
        "ASSETS",
        "ZCATEGORY"
    };

    public async Task<MmImportData> ParseAsync(string tempFilePath, CancellationToken ct)
    {
        ValidateFilePath(tempFilePath);
        ValidateFileSizeAndMagic(tempFilePath);

        var connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = tempFilePath,
            Mode = SqliteOpenMode.ReadOnly,
            Cache = SqliteCacheMode.Shared
        }.ToString();

        await using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync(ct);

        try
        {
            await ValidateSchemaAsync(connection, ct);

            var currencies = await ReadCurrenciesAsync(connection, ct);
            var assetGroups = await ReadAssetGroupsAsync(connection, ct);
            var assets = await ReadAssetsAsync(connection, assetGroups, ct);
            var categories = await ReadCategoriesAsync(connection, ct);
            var transactions = await ReadTransactionsAsync(connection, ct);
            var budgets = await ReadBudgetsAsync(connection, ct);
            var budgetAmounts = await ReadBudgetAmountsAsync(connection, ct);
            var repeatTransactions = await ReadRepeatTransactionsAsync(connection, ct);

            LogMessages.MoneyManagerParseCompleted(logger,
                assetGroups.Count, assets.Count, categories.Count, transactions.Count);

            return new MmImportData(
                currencies,
                assetGroups,
                assets,
                categories,
                transactions,
                budgets,
                budgetAmounts,
                repeatTransactions);
        }
        catch (SqliteException ex)
        {
            // No fugue el tipo de infraestructura a Application: envolver como excepción neutra.
            throw new InvalidImportFileException($"Invalid Money Manager database: {ex.Message}");
        }
        finally
        {
            await connection.CloseAsync();
            await connection.DisposeAsync();
        }
    }

    public async Task<MmImportData> ParseAsync(Stream stream, CancellationToken ct)
    {
        if (stream is null)
            throw new InvalidOperationException("Import stream is null.");

        if (!stream.CanRead)
            throw new InvalidOperationException("Import stream is not readable.");

        var tempPath = Path.Combine(Path.GetTempPath(), $"budgie-mm-{Guid.NewGuid():N}.mmbak");

        try
        {
            await using (var fileStream = new FileStream(tempPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 8192, useAsync: true))
            {
                if (stream.CanSeek)
                    stream.Seek(0, SeekOrigin.Begin);

                await stream.CopyToAsync(fileStream, ct);
            }

            return await ParseAsync(tempPath, ct);
        }
        finally
        {
            TryDeleteFile(tempPath);
        }
    }

    private static void ValidateFilePath(string tempFilePath)
    {
        if (string.IsNullOrWhiteSpace(tempFilePath))
            throw new InvalidImportFileException("Import file path is empty.");

        if (!File.Exists(tempFilePath))
            throw new InvalidImportFileException($"Import file not found: {tempFilePath}");
    }

    private static void ValidateFileSizeAndMagic(string tempFilePath)
    {
        var fileInfo = new FileInfo(tempFilePath);

        if (fileInfo.Length == 0)
            throw new InvalidImportFileException("Import file is empty.");

        if (fileInfo.Length > MaxFileSizeBytes)
            throw new InvalidImportFileException($"Import file exceeds maximum size of 10 MB (actual: {fileInfo.Length} bytes).");

        byte[] header = new byte[16];
        using var fs = new FileStream(tempFilePath, FileMode.Open, FileAccess.Read, FileShare.Read, 16, useAsync: false);
        var read = fs.Read(header, 0, 16);
        if (read < 16 || !header.AsSpan(0, 16).SequenceEqual(SqliteMagic))
            throw new InvalidImportFileException("Invalid SQLite file: missing 'SQLite format 3' header.");
    }

    private static async Task ValidateSchemaAsync(SqliteConnection connection, CancellationToken ct)
    {
        var tables = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT name FROM sqlite_master WHERE type='table'";

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            var name = reader.IsDBNull(0) ? null : reader.GetString(0);
            if (!string.IsNullOrWhiteSpace(name))
                tables.Add(name);
        }

        foreach (var required in RequiredTables)
        {
            if (!tables.Contains(required))
                throw new InvalidImportFileException($"Invalid Money Manager database: required table '{required}' not found.");
        }
    }

    private static async Task<IReadOnlyList<MmCurrency>> ReadCurrenciesAsync(SqliteConnection connection, CancellationToken ct)
    {
        var result = new List<MmCurrency>();
        try
        {
            await using var cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT \"ISO\", \"RATE\", \"IS_MAIN_CURRENCY\", \"IS_DEL\" FROM \"CURRENCY\"";

            await using var reader = await cmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                var isDel = SafeGetInt(reader, 3);
                if (isDel != 0)
                    continue;

                var iso = SafeGetString(reader, 0);
                if (string.IsNullOrWhiteSpace(iso))
                    continue;

                var rate = SafeGetDouble(reader, 1);
                var isMain = SafeGetInt(reader, 2) == 1;

                result.Add(new MmCurrency(iso.Trim().ToUpperInvariant(), (decimal)rate, isMain));
            }
        }
        catch (SqliteException)
        {
            // Table missing or column mismatch — fallback to minimal query
            try
            {
                result.Clear();
                await using var fallback = connection.CreateCommand();
                fallback.CommandText = "SELECT \"ISO\", \"RATE\", \"IS_MAIN_CURRENCY\" FROM \"CURRENCY\"";
                await using var reader = await fallback.ExecuteReaderAsync(ct);
                while (await reader.ReadAsync(ct))
                {
                    var iso = SafeGetString(reader, 0);
                    if (string.IsNullOrWhiteSpace(iso))
                        continue;
                    var rate = SafeGetDouble(reader, 1);
                    var isMain = SafeGetInt(reader, 2) == 1;
                    result.Add(new MmCurrency(iso.Trim().ToUpperInvariant(), (decimal)rate, isMain));
                }
            }
            catch (SqliteException)
            {
                // Return empty if still failing
            }
        }

        return result;
    }

    private static async Task<IReadOnlyList<MmAssetGroup>> ReadAssetGroupsAsync(SqliteConnection connection, CancellationToken ct)
    {
        var result = new List<MmAssetGroup>();
        try
        {
            await using var cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT \"uid\", \"ACC_GROUP_NAME\", \"TYPE\", \"IS_DEL\" FROM \"ASSETGROUP\"";

            await using var reader = await cmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                var isDel = SafeGetInt(reader, 3);
                if (isDel != 0)
                    continue;

                var uid = SafeGetString(reader, 0);
                var name = SafeGetString(reader, 1) ?? string.Empty;
                var type = SafeGetInt(reader, 2);
                result.Add(new MmAssetGroup(type, name));
                // uid retained only for join; MmAssetGroup currently holds Type+Name.
                _ = uid;
            }
        }
        catch (SqliteException)
        {
            try
            {
                result.Clear();
                await using var fallback = connection.CreateCommand();
                fallback.CommandText = "SELECT \"ACC_GROUP_NAME\", \"TYPE\" FROM \"ASSETGROUP\"";
                await using var reader = await fallback.ExecuteReaderAsync(ct);
                while (await reader.ReadAsync(ct))
                {
                    var name = SafeGetString(reader, 0) ?? string.Empty;
                    var type = SafeGetInt(reader, 1);
                    result.Add(new MmAssetGroup(type, name));
                }
            }
            catch (SqliteException)
            {
            }
        }

        return result;
    }

    private static async Task<IReadOnlyList<MmAsset>> ReadAssetsAsync(
        SqliteConnection connection,
        IReadOnlyList<MmAssetGroup> assetGroups,
        CancellationToken ct)
    {
        // Build uid -> TYPE map for GroupType resolution
        var groupTypeMap = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        try
        {
            await using var grpCmd = connection.CreateCommand();
            grpCmd.CommandText = "SELECT \"uid\", \"TYPE\" FROM \"ASSETGROUP\"";
            await using var grpReader = await grpCmd.ExecuteReaderAsync(ct);
            while (await grpReader.ReadAsync(ct))
            {
                var gUid = SafeGetString(grpReader, 0);
                if (string.IsNullOrWhiteSpace(gUid))
                    continue;
                var gType = SafeGetInt(grpReader, 1);
                groupTypeMap[gUid] = gType;
            }
        }
        catch (SqliteException)
        {
            // Fallback: use assetGroups order if map unavailable — still need something
            // Leave map empty, will default to Checking (0)
        }

        var result = new List<MmAsset>();
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT \"uid\", \"groupUid\", \"currencyUid\", \"NIC_NAME\", \"ZDATA\", \"A_UTIME\" FROM \"ASSETS\"";

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            var uid = SafeGetString(reader, 0);
            if (string.IsNullOrWhiteSpace(uid))
                continue;

            var groupUid = SafeGetString(reader, 1) ?? string.Empty;
            var currencyUid = SafeGetString(reader, 2) ?? string.Empty;
            var nicName = SafeGetString(reader, 3) ?? string.Empty;
            var zdataRaw = SafeGetString(reader, 4);
            var aUtimeRaw = reader.IsDBNull(5) ? null : Convert.ToString(reader.GetValue(5), CultureInfo.InvariantCulture);

            var zdata = 0;
            if (!string.IsNullOrWhiteSpace(zdataRaw) && int.TryParse(zdataRaw.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var zd))
                zdata = zd;
            else if (!reader.IsDBNull(4))
            {
                // Try numeric read
                var val = reader.GetValue(4);
                if (val is long l) zdata = (int)l;
                else if (val is int i) zdata = i;
                else if (val is string s && int.TryParse(s, out var si)) zdata = si;
            }

            var groupType = groupTypeMap.TryGetValue(groupUid, out var gt) ? gt : 0;

            result.Add(new MmAsset(uid, groupUid, groupType, currencyUid, nicName, zdata, aUtimeRaw));
        }

        return result;
    }

    private static async Task<IReadOnlyList<MmCategory>> ReadCategoriesAsync(SqliteConnection connection, CancellationToken ct)
    {
        var result = new List<MmCategory>();
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT \"uid\", \"NAME\", \"TYPE\", \"pUid\", \"C_IS_DEL\", \"STATUS\" FROM \"ZCATEGORY\"";

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            var uid = SafeGetString(reader, 0);
            if (string.IsNullOrWhiteSpace(uid))
                continue;

            var name = SafeGetString(reader, 1) ?? string.Empty;
            var type = SafeGetInt(reader, 2);
            var pUid = SafeGetString(reader, 3);
            if (string.IsNullOrWhiteSpace(pUid))
                pUid = null;
            var cIsDel = SafeGetInt(reader, 4);
            var status = SafeGetInt(reader, 5);

            result.Add(new MmCategory(uid, name, type, pUid, cIsDel, status));
        }

        return result;
    }

    private static async Task<IReadOnlyList<MmTransaction>> ReadTransactionsAsync(SqliteConnection connection, CancellationToken ct)
    {
        var result = new List<MmTransaction>();
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT \"uid\", \"assetUid\", \"toAssetUid\", \"ctgUid\", \"ZDATE\", \"WDATE\", \"ZMONEY\", \"IN_ZMONEY\", \"AMOUNT_ACCOUNT\", \"currencyUid\", \"ZCONTENT\", \"DO_TYPE\", \"IS_DEL\", \"MARK\" FROM \"INOUTCOME\" WHERE \"IS_DEL\" = 0";

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            var uid = SafeGetString(reader, 0);
            if (string.IsNullOrWhiteSpace(uid))
                continue;

            var assetUid = SafeGetString(reader, 1) ?? string.Empty;
            var toAssetUid = SafeGetString(reader, 2) ?? "-4";
            var ctgUid = SafeGetString(reader, 3) ?? "-4";
            var zdate = SafeGetString(reader, 4);
            var wdate = SafeGetString(reader, 5);
            var zmoney = SafeGetString(reader, 6);
            var inZMoney = SafeGetString(reader, 7);
            var amountAccount = reader.IsDBNull(8) ? null : Convert.ToString(reader.GetValue(8), CultureInfo.InvariantCulture);
            var currencyUid = SafeGetString(reader, 9);
            var zcontent = SafeGetString(reader, 10);
            var doTypeRaw = SafeGetString(reader, 11);
            var doType = 0;
            if (!string.IsNullOrWhiteSpace(doTypeRaw) && int.TryParse(doTypeRaw.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var dt))
                doType = dt;
            else if (!reader.IsDBNull(11))
            {
                var v = reader.GetValue(11);
                if (v is long ll) doType = (int)ll;
                else if (v is int ii) doType = ii;
            }

            var mark = SafeGetString(reader, 13);

            result.Add(new MmTransaction(uid, assetUid, toAssetUid, ctgUid, zdate, wdate, zmoney, inZMoney, amountAccount, currencyUid, zcontent, doType, mark));
        }

        return result;
    }

    private static async Task<IReadOnlyList<MmBudget>> ReadBudgetsAsync(SqliteConnection connection, CancellationToken ct)
    {
        var result = new List<MmBudget>();
        try
        {
            await using var cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT \"uid\", \"targetUid\", \"IS_DEL\" FROM \"BUDGET\"";

            await using var reader = await cmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                var isDel = SafeGetInt(reader, 2);
                if (isDel != 0)
                    continue;

                var uid = SafeGetString(reader, 0);
                var targetUid = SafeGetString(reader, 1);
                if (string.IsNullOrWhiteSpace(uid) || string.IsNullOrWhiteSpace(targetUid))
                    continue;

                result.Add(new MmBudget(uid, targetUid));
            }
        }
        catch (SqliteException)
        {
            try
            {
                result.Clear();
                await using var fallback = connection.CreateCommand();
                fallback.CommandText = "SELECT \"uid\", \"targetUid\" FROM \"BUDGET\"";
                await using var reader = await fallback.ExecuteReaderAsync(ct);
                while (await reader.ReadAsync(ct))
                {
                    var uid = SafeGetString(reader, 0);
                    var targetUid = SafeGetString(reader, 1);
                    if (string.IsNullOrWhiteSpace(uid) || string.IsNullOrWhiteSpace(targetUid))
                        continue;
                    result.Add(new MmBudget(uid, targetUid));
                }
            }
            catch (SqliteException)
            {
            }
        }

        return result;
    }

    private static async Task<IReadOnlyList<MmBudgetAmount>> ReadBudgetAmountsAsync(SqliteConnection connection, CancellationToken ct)
    {
        var result = new List<MmBudgetAmount>();
        try
        {
            await using var cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT \"budgetUid\", \"AMOUNT\", \"IS_DEL\" FROM \"BUDGET_AMOUNT\"";

            await using var reader = await cmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                var isDel = SafeGetInt(reader, 2);
                if (isDel != 0)
                    continue;

                var budgetUid = SafeGetString(reader, 0);
                if (string.IsNullOrWhiteSpace(budgetUid))
                    continue;

                var amount = SafeGetDouble(reader, 1);
                result.Add(new MmBudgetAmount(budgetUid, (decimal)amount));
            }
        }
        catch (SqliteException)
        {
            try
            {
                result.Clear();
                await using var fallback = connection.CreateCommand();
                fallback.CommandText = "SELECT \"budgetUid\", \"AMOUNT\" FROM \"BUDGET_AMOUNT\"";
                await using var reader = await fallback.ExecuteReaderAsync(ct);
                while (await reader.ReadAsync(ct))
                {
                    var budgetUid = SafeGetString(reader, 0);
                    if (string.IsNullOrWhiteSpace(budgetUid))
                        continue;
                    var amount = SafeGetDouble(reader, 1);
                    result.Add(new MmBudgetAmount(budgetUid, (decimal)amount));
                }
            }
            catch (SqliteException)
            {
            }
        }

        return result;
    }

    private static async Task<IReadOnlyList<MmRepeatTransaction>> ReadRepeatTransactionsAsync(SqliteConnection connection, CancellationToken ct)
    {
        var result = new List<MmRepeatTransaction>();
        try
        {
            await using var cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT \"uid\", \"assetUid\", \"toAssetUid\", \"ctgUid\", \"currencyUid\", \"AMOUNT_SUB\", \"DO_TYPE\", \"REPEAT_TYPE\", \"NEXT_DATE\", \"MEMO\", \"IS_DEL\" FROM \"REPEATTRANSACTION\"";

            await using var reader = await cmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                var isDel = SafeGetInt(reader, 10);
                if (isDel != 0)
                    continue;

                var uid = SafeGetString(reader, 0);
                if (string.IsNullOrWhiteSpace(uid))
                    continue;

                var assetUid = SafeGetString(reader, 1) ?? string.Empty;
                var toAssetUid = SafeGetString(reader, 2);
                var ctgUid = SafeGetString(reader, 3);
                var currencyUid = SafeGetString(reader, 4);
                var amountSub = reader.IsDBNull(5) ? null : Convert.ToString(reader.GetValue(5), CultureInfo.InvariantCulture);
                var doType = SafeGetInt(reader, 6);
                var repeatType = SafeGetInt(reader, 7);
                var nextDateRaw = reader.IsDBNull(8) ? null : Convert.ToString(reader.GetValue(8), CultureInfo.InvariantCulture);
                var memo = SafeGetString(reader, 9);

                result.Add(new MmRepeatTransaction(uid, assetUid, toAssetUid, ctgUid, amountSub, null, currencyUid, doType, repeatType, nextDateRaw, memo));
                // Note: MmRepeatTransaction expects (Uid, AssetUid, ToAssetUid, CtgUid, ZMoney, AmountAccount, CurrencyUid, DoType, RepeatType, NextDate, ZContent)
                // We map AMOUNT_SUB to ZMoney, memo to ZContent, AmountAccount left null (handler will handle)
            }
        }
        catch (SqliteException)
        {
            try
            {
                result.Clear();
                await using var fallback = connection.CreateCommand();
                fallback.CommandText = "SELECT \"uid\", \"assetUid\", \"toAssetUid\", \"ctgUid\", \"currencyUid\", \"AMOUNT_SUB\", \"DO_TYPE\", \"REPEAT_TYPE\", \"NEXT_DATE\", \"MEMO\" FROM \"REPEATTRANSACTION\"";
                await using var reader = await fallback.ExecuteReaderAsync(ct);
                while (await reader.ReadAsync(ct))
                {
                    var uid = SafeGetString(reader, 0);
                    if (string.IsNullOrWhiteSpace(uid))
                        continue;
                    var assetUid = SafeGetString(reader, 1) ?? string.Empty;
                    var toAssetUid = SafeGetString(reader, 2);
                    var ctgUid = SafeGetString(reader, 3);
                    var currencyUid = SafeGetString(reader, 4);
                    var amountSub = reader.IsDBNull(5) ? null : Convert.ToString(reader.GetValue(5), CultureInfo.InvariantCulture);
                    var doType = SafeGetInt(reader, 6);
                    var repeatType = SafeGetInt(reader, 7);
                    var nextDateRaw = reader.IsDBNull(8) ? null : Convert.ToString(reader.GetValue(8), CultureInfo.InvariantCulture);
                    var memo = SafeGetString(reader, 9);
                    result.Add(new MmRepeatTransaction(uid, assetUid, toAssetUid, ctgUid, amountSub, null, currencyUid, doType, repeatType, nextDateRaw, memo));
                }
            }
            catch (SqliteException)
            {
            }
        }

        return result;
    }

    private static string? SafeGetString(SqliteDataReader reader, int ordinal)
    {
        if (reader.IsDBNull(ordinal))
            return null;

        var value = reader.GetValue(ordinal);
        return value switch
        {
            string s => s,
            _ => Convert.ToString(value, CultureInfo.InvariantCulture)
        };
    }

    private static int SafeGetInt(SqliteDataReader reader, int ordinal)
    {
        if (reader.IsDBNull(ordinal))
            return 0;

        var value = reader.GetValue(ordinal);
        return value switch
        {
            int i => i,
            long l => (int)l,
            string s when int.TryParse(s.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) => parsed,
            double d => (int)d,
            _ => 0
        };
    }

    private static double SafeGetDouble(SqliteDataReader reader, int ordinal)
    {
        if (reader.IsDBNull(ordinal))
            return 0;

        var value = reader.GetValue(ordinal);
        return value switch
        {
            double d => d,
            float f => f,
            decimal dec => (double)dec,
            long l => l,
            int i => i,
            string s when double.TryParse(s.Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed) => parsed,
            _ => 0
        };
    }

    private static void TryDeleteFile(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch
        {
            // Best-effort cleanup — no lock should remain due to ReadOnly mode
        }
    }
}
