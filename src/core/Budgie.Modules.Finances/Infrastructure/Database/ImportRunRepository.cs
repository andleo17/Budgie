using System.Data;
using Budgie.Modules.Finances.Application.Import;
using Budgie.Modules.Finances.Domain.Import;
using Budgie.Shared.Infrastructure;
using Dapper;

namespace Budgie.Modules.Finances.Infrastructure.Database;

internal sealed class ImportRunRepository(IDbConnectionFactory factory) : IImportRunRepository
{
    public Task<bool> ExistsByHashAsync(Guid userId, string hash, CancellationToken ct)
        => factory.ExecuteScalarAsync<bool>(
            @"SELECT EXISTS(SELECT 1 FROM ""ImportRuns"" WHERE ""UserId"" = @UserId AND ""FileHash"" = @FileHash)",
            new { UserId = userId, FileHash = hash }, ct);

    public Task AddAsync(ImportRun run, CancellationToken ct, IDbTransaction? tx = null)
    {
        const string sql =
            @"INSERT INTO ""ImportRuns"" (""Id"",""UserId"",""FileHash"",""FileName"",""CreatedAt"",""Counts"")
              VALUES (@Id,@UserId,@FileHash,@FileName,@CreatedAt,@Counts::jsonb)";
        var p = new { run.Id, run.UserId, run.FileHash, run.FileName, run.CreatedAt, Counts = run.CountsJson };
        return tx is not null
            ? tx.Connection!.ExecuteAsync(new CommandDefinition(sql, p, transaction: tx, cancellationToken: ct))
            : factory.ExecuteAsync(sql, p, ct);
    }

    // Merge ISO del import dentro de SupportedCurrencies (jsonb): union de elementos + distinct.
    public Task MergeSupportedCurrenciesAsync(Guid userId, IReadOnlyCollection<string> isos, IDbTransaction tx, CancellationToken ct)
    {
        if (isos.Count == 0)
            return Task.CompletedTask;

        const string sql =
            @"UPDATE ""UserSettings""
              SET ""SupportedCurrencies"" = (
                  SELECT jsonb_agg(DISTINCT iso ORDER BY iso)
                  FROM (
                      SELECT jsonb_array_elements_text(""SupportedCurrencies"") AS iso FROM ""UserSettings"" WHERE ""UserId"" = @UserId
                      UNION
                      SELECT unnest(@Isos::text[])
                  ) s
              )
              WHERE ""UserId"" = @UserId";
        return tx.Connection!.ExecuteAsync(
            new CommandDefinition(sql, new { UserId = userId, Isos = isos.ToArray() }, transaction: tx, cancellationToken: ct));
    }
}
