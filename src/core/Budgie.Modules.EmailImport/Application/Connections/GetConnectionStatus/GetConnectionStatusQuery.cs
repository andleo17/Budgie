using Budgie.Shared.Enums;
using ErrorOr;
using Mediator;

namespace Budgie.Modules.EmailImport.Application.Connections.GetConnectionStatus;

/// <summary>
/// Lists the user's connected mailboxes as metadata only.
/// </summary>
public record GetConnectionStatusQuery : IRequest<ErrorOr<List<EmailConnectionStatusResponse>>>;

/// <summary>
/// Metadata of one connected mailbox. <b>Never carries the credential</b> — not even
/// encrypted (spec §11): the response is assembled from connection fields only, and
/// <c>EncryptedCredentials</c> is deliberately absent from every property.
/// </summary>
/// <param name="Provider">The provider the mailbox is connected through.</param>
/// <param name="Email">The connected mailbox address.</param>
/// <param name="SyncEnabled">Whether the background sync is enabled for this connection.</param>
/// <param name="AutoCommitMinConfidence">Minimum confidence required to auto-commit a parsed email.</param>
/// <param name="LastSyncAt">When the last sync pass ran, if any.</param>
/// <param name="LastError">The last sync error, if any.</param>
/// <param name="LastSkippedCount">Messages skipped on the last sync because no rule matched.</param>
public record EmailConnectionStatusResponse(
    EmailProvider Provider,
    string Email,
    bool SyncEnabled,
    decimal AutoCommitMinConfidence,
    DateTime? LastSyncAt,
    string? LastError,
    int LastSkippedCount);
