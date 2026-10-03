using Budgie.Modules.EmailImport.Application.Abstractions;
using Budgie.Modules.EmailImport.Domain.Connections;
using Budgie.Shared.Abstractions;
using Budgie.Shared.Enums;
using ErrorOr;
using Mediator;
using System.Globalization;

namespace Budgie.Modules.EmailImport.Application.Connections.GetAuthorizationUri;

internal sealed class GetAuthorizationUriCommandHandler(
    IEmailConnectionRepository emailConnectionRepository,
    IEmailProviderResolver providerResolver,
    OAuthStateService stateService,
    ICurrentUserProvider currentUserProvider)
    : IRequestHandler<GetAuthorizationUriCommand, ErrorOr<EmailAuthorizationUri>>
{
    public async ValueTask<ErrorOr<EmailAuthorizationUri>> Handle(
        GetAuthorizationUriCommand request, CancellationToken ct)
    {
        var userId = currentUserProvider.GetUserId();

        // ExistsByProviderAsync (EXISTS, never throws) instead of GetByProviderAsync: the
        // question is "is this provider already connected?", and the DDL's UNIQUE is on
        // (UserId, Provider, Email) — a triple, not the pair.
        if (await emailConnectionRepository.ExistsByProviderAsync(userId, request.Provider, ct))
        {
            return CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "en"
                ? Error.Conflict("EmailImport.AlreadyConnected", "A mailbox is already connected for this provider. Disconnect it before connecting another one.")
                : Error.Conflict("EmailImport.AlreadyConnected", "Ya hay un buzón conectado para este proveedor. Desconéctalo antes de conectar otro.");
        }

        var client = providerResolver.Resolve(request.Provider);

        // The state is signed here, by Budgie: it seals userId + provider + redirectUri
        // and is what authenticates the unauthenticated callback (spec §6).
        var state = stateService.Issue(userId, request.Provider, request.RedirectUri);

        // Provisional connection, never persisted: the provider derives everything from
        // configuration; the placeholder values only satisfy EmailConnection.Create's
        // invariants (non-empty email and credentials).
        var provisional = EmailConnection.Create(userId, request.Provider, "pending", "pending");

        // The provider embeds our state in the authorization URI and echoes it back. If a
        // provider ever returned a different state, the callback's TryConsume would reject
        // it — fail-closed by construction, so no extra check is needed here.
        return await client.BuildAuthorizationUriAsync(
            provisional, request.RedirectUri, state, ct);
    }
}
