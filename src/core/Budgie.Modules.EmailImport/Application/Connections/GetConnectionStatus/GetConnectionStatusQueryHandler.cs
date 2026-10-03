using Budgie.Modules.EmailImport.Application.Abstractions;
using Budgie.Shared.Abstractions;
using ErrorOr;
using Mediator;

namespace Budgie.Modules.EmailImport.Application.Connections.GetConnectionStatus;

internal sealed class GetConnectionStatusQueryHandler(
    IEmailConnectionRepository emailConnectionRepository,
    ICurrentUserProvider currentUserProvider)
    : IRequestHandler<GetConnectionStatusQuery, ErrorOr<List<EmailConnectionStatusResponse>>>
{
    public async ValueTask<ErrorOr<List<EmailConnectionStatusResponse>>> Handle(
        GetConnectionStatusQuery request, CancellationToken ct)
    {
        var connections = await emailConnectionRepository.GetAllAsync(currentUserProvider.GetUserId(), ct);

        // No connections → empty list, NOT NotFound: "nothing connected yet" is a valid
        // state the UI renders as the connect card. Credentials never leave the
        // repository: the mapping below is metadata-only by construction.
        return connections
            .Select(connection => new EmailConnectionStatusResponse(
                connection.Provider,
                connection.Email,
                connection.SyncEnabled,
                connection.AutoCommitMinConfidence,
                connection.LastSyncAt,
                connection.LastError,
                connection.LastSkippedCount))
            .ToList();
    }
}
