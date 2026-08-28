using System.Data;
using Budgie.Modules.Finances.Domain.Import;

namespace Budgie.Modules.Finances.Application.Import;

public interface IImportRunRepository
{
    Task<bool> ExistsByHashAsync(Guid userId, string hash, CancellationToken ct);

    Task AddAsync(ImportRun run, CancellationToken ct, IDbTransaction? tx = null);

    // Merge las ISO del import dentro de UserSettings.SupportedCurrencies (jsonb UNION).
    // tx es obligatorio: se ejecuta dentro de la transacción del CommitHandler.
    Task MergeSupportedCurrenciesAsync(Guid userId, IReadOnlyCollection<string> isos, IDbTransaction tx, CancellationToken ct);
}
