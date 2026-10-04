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
    /// Fallback mailbox value when the provider reported no address (or its profile read failed).
    /// It is deliberately NOT a syntactically valid email: the alternative would be fabricating
    /// an address that looks real and would be stored inside
    /// <c>UNIQUE("UserId","Provider","Email")</c>. Rows created before the provider started
    /// reporting the address keep this value until the user disconnects and reconnects.
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
        // 3b. The mailbox address, when the provider reported one. The provider already fetches it
        //     during the exchange (the profile call that yields the historyId yields emailAddress
        //     too), so it costs nothing extra. Falling back to the placeholder keeps the
        //     connection working when the provider is silent — the mailbox IS connected.
        var connection = EmailConnection.Create(
            userId,
            provider,
            string.IsNullOrWhiteSpace(credentials.Email) ? ConnectedEmailPlaceholder : credentials.Email,
            credentials.EncryptedRefreshToken);

        // SyncCursor stays null (the Create factory never sets it): the first sync reads
        // the provider's recovery window.
        await emailConnectionRepository.AddAsync(connection, ct);

        return connection.Id;
    }
}
