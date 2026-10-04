using Budgie.Modules.EmailImport.Application.Abstractions;
using Budgie.Shared.Abstractions;
using Budgie.Shared.Enums;
using ErrorOr;
using Mediator;

namespace Budgie.Modules.EmailImport.Application.Connections.DisconnectEmail;

internal sealed class DisconnectEmailCommandHandler(
    IEmailConnectionRepository emailConnectionRepository,
    ICurrentUserProvider currentUserProvider)
    : IRequestHandler<DisconnectEmailCommand, ErrorOr<Success>>
{
    public async ValueTask<ErrorOr<Success>> Handle(DisconnectEmailCommand request, CancellationToken ct)
    {
        var userId = currentUserProvider.GetUserId();

        // GetByProviderAsync (LIMIT 1, documented for the multi-mailbox case) is correct
        // here: unlike "already connected?", disconnecting acts on one concrete row.
        var connection = await emailConnectionRepository.GetByProviderAsync(userId, request.Provider, ct);
        if (connection is null)
            return EmailImportErrors.NotConnected();

        // ON DELETE CASCADE removes this connection's STAGING rows in the same statement
        // (see 003_email_import.sql). NOTE: EmailImportRules has no FK to connections —
        // it hangs off the user and the account — so the rules deliberately SURVIVE a
        // disconnect: reconnecting the mailbox should not force the user to rebuild
        // them. The consequence to be aware of is that the staging history (including
        // Committed rows and their CreatedTransactionId links) is destroyed by the
        // disconnect, so traceability of past imports is lost with the connection.
        await emailConnectionRepository.DeleteAsync(userId, connection.Id, ct);

        return Result.Success;
    }
}
