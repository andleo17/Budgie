using Budgie.Modules.Finances.Application;
using Budgie.Shared.Abstractions;
using ErrorOr;
using Mediator;

namespace Budgie.Modules.Finances.Application.Export;

internal sealed class ExportSnapshotQueryHandler(
    IExportRepository exportRepository,
    ICurrentUserProvider currentUserProvider)
    : IRequestHandler<ExportSnapshotQuery, ErrorOr<SnapshotData>>
{
    public async ValueTask<ErrorOr<SnapshotData>> Handle(ExportSnapshotQuery request, CancellationToken ct)
    {
        var userId = currentUserProvider.GetUserId();
        return await exportRepository.GetSnapshotAsync(userId, ct);
    }
}
