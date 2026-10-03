using ErrorOr;
using Mediator;

namespace Budgie.Modules.EmailImport.Application.Inbox.SyncNow;

/// <summary>
/// Runs one synchronisation pass for the current user's mailboxes on demand. Rejected with
/// a Conflict while the background worker owns the schedule (<c>EmailImport:Enabled =
/// true</c>): two syncs racing each other would only duplicate work over the same cursor.
/// </summary>
public record SyncNowCommand : IRequest<ErrorOr<EmailSyncResult>>;
