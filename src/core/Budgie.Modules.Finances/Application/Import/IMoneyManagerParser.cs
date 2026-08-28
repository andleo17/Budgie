namespace Budgie.Modules.Finances.Application.Import;

// Parser readonly del .mmbak (SQLite 3.x) — implementación en Infrastructure/Import.
// No referencia IDbConnectionFactory ni Dapper (usa Microsoft.Data.Sqlite).
public interface IMoneyManagerParser
{
    Task<MmImportData> ParseAsync(string tempFilePath, CancellationToken ct);

    Task<MmImportData> ParseAsync(Stream stream, CancellationToken ct);
}
