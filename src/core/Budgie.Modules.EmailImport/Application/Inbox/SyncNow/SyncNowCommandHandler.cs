using System.Globalization;
using Budgie.Shared.Abstractions;
using ErrorOr;
using Mediator;
using Microsoft.Extensions.Options;

namespace Budgie.Modules.EmailImport.Application.Inbox.SyncNow;

/// <inheritdoc cref="SyncNowCommand"/>
internal sealed class SyncNowCommandHandler(
    IEmailSyncService emailSyncService,
    ICurrentUserProvider currentUserProvider,
    IOptions<EmailImportOptions> options)
    : IRequestHandler<SyncNowCommand, ErrorOr<EmailSyncResult>>
{
    public async ValueTask<ErrorOr<EmailSyncResult>> Handle(SyncNowCommand request, CancellationToken ct)
    {
        // Worker enabled → it already syncs every enabled mailbox on its own timer; a
        // manual pass now would race the same cursor. Conflict, not silent duplication.
        if (options.Value.Enabled)
            return SyncAlreadyScheduled();

        return await emailSyncService.SyncUserAsync(currentUserProvider.GetUserId(), ct);
    }

    private static Error SyncAlreadyScheduled() =>
        CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "en"
            ? Error.Conflict(
                "EmailImport.SyncAlreadyScheduled",
                "The sync worker is enabled, so mailboxes sync automatically. Turn it off (EmailImport:Enabled=false) to sync on demand.")
            : Error.Conflict(
                "EmailImport.SyncAlreadyScheduled",
                "El worker de sincronización está activo, así que los buzones se sincronizan automáticamente. Desactívalo (EmailImport:Enabled=false) para sincronizar bajo demanda.");
}
