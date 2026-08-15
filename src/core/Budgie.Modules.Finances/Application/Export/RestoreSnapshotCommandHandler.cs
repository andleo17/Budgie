using Budgie.Modules.Finances.Application;
using Budgie.Shared.Abstractions;
using ErrorOr;
using Mediator;

namespace Budgie.Modules.Finances.Application.Export;

internal sealed class RestoreSnapshotCommandHandler(
    IExportRepository exportRepository,
    ICurrentUserProvider currentUserProvider)
    : IRequestHandler<RestoreSnapshotCommand, ErrorOr<Success>>
{
    public async ValueTask<ErrorOr<Success>> Handle(RestoreSnapshotCommand request, CancellationToken ct)
    {
        var userId = currentUserProvider.GetUserId();
        var snapshot = request.Snapshot;

        if (string.IsNullOrWhiteSpace(snapshot.Version))
            return Error.Validation("Snapshot.InvalidVersion", "Snapshot version is missing.");

        if (snapshot.Accounts is null || snapshot.Transactions is null)
            return Error.Validation("Snapshot.Invalid", "Snapshot is missing required data.");

        await exportRepository.RestoreAsync(userId, snapshot, ct);
        return Result.Success;
    }
}
