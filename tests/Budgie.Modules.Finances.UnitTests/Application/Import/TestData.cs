using Microsoft.Data.Sqlite;

namespace Budgie.Modules.Finances.UnitTests.Application.Import;

// Helper to build real SQLite .mmbak files in a temp folder for parser tests.
internal static class TestData
{
    public static string CreateTempDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"budgie-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        return dir;
    }

    /// <summary>Writes raw bytes to a file in the given directory and returns the path.</summary>
    public static string WriteFile(string dir, string name, byte[] bytes)
    {
        var path = Path.Combine(dir, name);
        File.WriteAllBytes(path, bytes);
        return path;
    }

    /// <summary>Creates a valid minimal Money Manager backup with the required tables.</summary>
    public static string CreateValidBackup(string dir, string name = "valid.mmbak")
    {
        var path = Path.Combine(dir, name);
        var cs = new SqliteConnectionStringBuilder { DataSource = path }.ToString();

        using var conn = new SqliteConnection(cs);
        conn.Open();

        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = """
                CREATE TABLE CURRENCY (ISO VARCHAR, RATE REAL, IS_MAIN_CURRENCY INTEGER, IS_DEL INTEGER);
                CREATE TABLE ASSETGROUP (uid TEXT, ACC_GROUP_NAME VARCHAR, TYPE INTEGER, IS_DEL INTEGER);
                CREATE TABLE ASSETS (uid TEXT, groupUid TEXT, currencyUid TEXT, NIC_NAME VARCHAR, ZDATA VARCHAR, A_UTIME INTEGER);
                CREATE TABLE ZCATEGORY (uid TEXT, NAME VARCHAR, TYPE INTEGER, pUid TEXT, C_IS_DEL INTEGER, STATUS INTEGER);
                CREATE TABLE INOUTCOME (uid TEXT, assetUid TEXT, toAssetUid TEXT, ctgUid TEXT, ZDATE VARCHAR, WDATE VARCHAR, ZMONEY VARCHAR, IN_ZMONEY VARCHAR, AMOUNT_ACCOUNT REAL, currencyUid TEXT, ZCONTENT VARCHAR, DO_TYPE VARCHAR, MARK INTEGER, IS_DEL INTEGER);
                CREATE TABLE BUDGET (uid TEXT, targetUid TEXT, IS_DEL INTEGER);
                CREATE TABLE BUDGET_AMOUNT (budgetUid TEXT, AMOUNT REAL, IS_DEL INTEGER);
                CREATE TABLE REPEATTRANSACTION (uid TEXT, assetUid TEXT, toAssetUid TEXT, ctgUid TEXT, currencyUid TEXT, AMOUNT_SUB REAL, DO_TYPE INTEGER, REPEAT_TYPE INTEGER, NEXT_DATE INTEGER, MEMO VARCHAR, IS_DEL INTEGER);
                INSERT INTO CURRENCY (ISO, RATE, IS_MAIN_CURRENCY, IS_DEL) VALUES ('PEN_PEN', 1.0, 1, 0);
                INSERT INTO ASSETGROUP (uid, ACC_GROUP_NAME, TYPE, IS_DEL) VALUES ('1', 'Cuentas', 1, 0);
                INSERT INTO ASSETS (uid, groupUid, currencyUid, NIC_NAME, ZDATA, A_UTIME) VALUES ('acc1', '1', 'PEN_PEN', 'Cuenta', '0', 1735689600000);
                INSERT INTO ZCATEGORY (uid, NAME, TYPE, pUid, C_IS_DEL, STATUS) VALUES ('cat1', 'Ingresos', 0, '', 0, 0);
                INSERT INTO INOUTCOME (uid, assetUid, toAssetUid, ctgUid, ZDATE, WDATE, ZMONEY, IN_ZMONEY, AMOUNT_ACCOUNT, currencyUid, ZCONTENT, DO_TYPE, MARK, IS_DEL) VALUES ('tx1', 'acc1', '-4', '-4', '1735689600000', '2025-01-01', '100', '100', 100, 'PEN_PEN', 'Saldo', '7', 0, 0);
                """;
            cmd.ExecuteNonQuery();
        }

        conn.Close();
        return path;
    }

    /// <summary>Creates a SQLite DB missing one of the required tables.</summary>
    public static string CreateBackupMissingTable(string dir)
    {
        var path = Path.Combine(dir, "missing.mmbak");
        var cs = new SqliteConnectionStringBuilder { DataSource = path }.ToString();
        using var conn = new SqliteConnection(cs);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "CREATE TABLE ZCATEGORY (uid TEXT);"; // no INOUTCOME / ASSETS
        cmd.ExecuteNonQuery();
        conn.Close();
        return path;
    }

    /// <summary>SQLite header magic "SQLite format 3\0".</summary>
    public static byte[] SqliteMagic => "SQLite format 3\0"u8.ToArray();
}
