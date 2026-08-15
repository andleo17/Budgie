using Budgie.Modules.Finances.Application.Export;

namespace Budgie.Modules.Finances.Application;

public interface IExportRepository
{
    Task<SnapshotData> GetSnapshotAsync(Guid userId, CancellationToken ct);
    Task RestoreAsync(Guid userId, SnapshotData snapshot, CancellationToken ct);
}
