using Budgie.Modules.EmailImport.Application.Abstractions;
using Budgie.Modules.EmailImport.Domain.Connections;
using ErrorOr;
using Mediator;
using System.Globalization;

namespace Budgie.Modules.EmailImport.Application.Connections.CompleteConnection;

internal sealed class CompleteConnectionCommandHandler(
    IEmailConnectionRepository emailConnectionRepository,
    IEmailProviderResolver providerResolver,
    OAuthStateService stateService)
    : IRequestHandler<CompleteConnectionCommand, ErrorOr<Guid>>
{
    /// <summary>
    /// TODO(EmailCredentials.Email): <see cref="EmailCredentials"/> does not carry the
    /// mailbox address, so the provider cannot tell Application which mailbox was just
    /// connected. The placeholder below is deliberate: it is NOT a syntactically valid
    /// email, so nobody can mistake it for real data, and it documents the gap instead of
    /// hiding it behind a fabricated address. Together with the ExistsByProviderAsync
    /// check below it preserves the application-level invariant of one connection per
    /// provider per user — which is what UNIQUE("UserId","Provider","Email") protects
    /// anyway. When EmailCredentials gains an email/profile field (the provider can fetch
    /// it from its profile endpoint during the exchange), use it here and delete this TODO.
    /// </summary>
    private const string ConnectedEmailPlaceholder = "connected";

    public async ValueTask<ErrorOr<Guid>> Handle(CompleteConnectionCommand request, CancellationToken ct)
    {
        // 1. The state is the ONLY source of userId in this flow. It is signed, expires in
        //    10 minutes and its nonce is burned on use — which is exactly why this handler
        //    must NOT read ICurrentUserProvider: the callback arrives without a JWT, and
        //    the state (not the caller) decides whose mailbox is being connected.
        if (!stateService.TryConsume(request.State, out var userId, out var provider, out var redirectUri))
        {
            return CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "en"
                ? Error.Validation("EmailImport.InvalidState", "The connection request is invalid or has expired. Start the connection again.")
                : Error.Validation("EmailImport.InvalidState", "La solicitud de conexión no es válida o ha caducado. Inicia la conexión de nuevo.");
        }

        // 2. Already connected for this provider? EXISTS, never throws (the DDL unique is
        //    on the triple UserId+Provider+Email, not on the pair).
        if (await emailConnectionRepository.ExistsByProviderAsync(userId, provider, ct))
        {
            return CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "en"
                ? Error.Conflict("EmailImport.AlreadyConnected", "A mailbox is already connected for this provider. Disconnect it before connecting another one.")
                : Error.Conflict("EmailImport.AlreadyConnected", "Ya hay un buzón conectado para este proveedor. Desconéctalo antes de conectar otro.");
        }

        var client = providerResolver.Resolve(provider);

        EmailCredentials credentials;
        try
        {
            // First connect: no previous credential, so an empty string per the port contract.
            credentials = await client.ExchangeCodeAsync(string.Empty, request.Code, redirectUri, ct);
        }
        catch (EmailProviderException ex)
        {
            // invalid_grant (code expired/already used), missing refresh token, provider
            // down... surfaced to the user as a retryable failure instead of a raw 500.
            return CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "en"
                ? Error.Failure("EmailImport.CodeExchangeFailed", $"The provider rejected the authorization code ({ex.Error}). Start the connection again.")
                : Error.Failure("EmailImport.CodeExchangeFailed", $"El proveedor rechazó el código de autorización ({ex.Error}). Inicia la conexión de nuevo.");
        }

        // 3. credentials.EncryptedRefreshToken is ALREADY the provider's protected
        //    envelope ({accessToken, refreshToken, expiresAt} sealed with
        //    ISecretProtector). Store it VERBATIM: re-encrypting here would produce a
        //    double envelope no one can decrypt, breaking every future sync. The access
        //    token itself is intentionally NOT persisted — only the envelope.
        var connection = EmailConnection.Create(
            userId, provider, ConnectedEmailPlaceholder, credentials.EncryptedRefreshToken);

        // SyncCursor stays null (the Create factory never sets it): the first sync reads
        // the provider's recovery window.
        await emailConnectionRepository.AddAsync(connection, ct);

        return connection.Id;
    }
}
