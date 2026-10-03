using Budgie.Modules.EmailImport.Application.Connections;
using Budgie.Modules.EmailImport.Domain.Connections;
using Budgie.Modules.EmailImport.Domain.Parsing;
using Budgie.Shared.Enums;

namespace Budgie.Modules.EmailImport.Application.Abstractions;

/// <summary>
/// Provider-neutral port for reading a user mailbox over OAuth. The whole point of this
/// interface is that the first vendor implementation (see
/// <c>Infrastructure/Email/Gmail</c>) will not be the only one: adding another provider
/// means registering another implementation of this port — Application, Domain and the
/// schema never change. Nothing in this interface may carry vendor vocabulary.
/// </summary>
/// <remarks>
/// The credential always travels <b>encrypted</b> (<c>encryptedCredentials</c>, the blob
/// persisted in <see cref="EmailConnection.EncryptedCredentials"/>). Implementations
/// decrypt it with <c>ISecretProtector</c> internally, so the refresh token is never in
/// clear text beyond the scope of a single provider call.
/// </remarks>
public interface IEmailProviderClient
{
    /// <summary>The provider discriminator this client serves.</summary>
    EmailProvider Provider { get; }

    /// <summary>
    /// Builds the URI the user must visit to authorize Budgie on the provider.
    /// <paramref name="redirectUri"/> and <paramref name="state"/> are passed explicitly
    /// because Application cannot read <c>HttpContext</c>: the endpoint derives the
    /// redirect URI from the request scheme + host and signs the state.
    /// <paramref name="connection"/> is carried for providers that need existing
    /// connection state while authorizing; implementations that do not need it ignore it.
    /// </summary>
    Task<EmailAuthorizationUri> BuildAuthorizationUriAsync(
        EmailConnection connection,
        string redirectUri,
        string state,
        CancellationToken ct);

    /// <summary>
    /// Exchanges the OAuth authorization code for tokens and returns the credential
    /// ready to persist. <paramref name="encryptedCredentials"/> carries the previous
    /// encrypted credential when the user is re-authorizing an existing connection
    /// (providers that revoke or replace prior credentials need it); on first connect
    /// the caller passes an empty string.
    /// </summary>
    Task<EmailCredentials> ExchangeCodeAsync(
        string encryptedCredentials,
        string code,
        string redirectUri,
        CancellationToken ct);

    /// <summary>
    /// Obtains a fresh access token from the persisted encrypted credential. The
    /// returned <see cref="EmailCredentials.EncryptedRefreshToken"/> is the new
    /// protected credential envelope to store over the old one.
    /// </summary>
    Task<EmailCredentials> RefreshAsync(string encryptedCredentials, CancellationToken ct);

    /// <summary>
    /// Returns the messages that arrived since the connection's cursor. When
    /// <see cref="EmailConnection.SyncCursor"/> is null the provider falls back to its
    /// recovery window (recent messages). Implementations may update the cursor
    /// <b>in memory</b> on <paramref name="connection"/> (for example after resetting an
    /// expired cursor); persisting it remains the sync service's job, after the whole
    /// batch has been processed.
    /// </summary>
    Task<IReadOnlyList<EmailMessage>> ListNewAsync(EmailConnection connection, CancellationToken ct);

    /// <summary>
    /// Returns the provider's current sync cursor — the position a brand-new cursor would
    /// have right now — or null when the provider exposes no cursor concept. Used by the
    /// sync service after a cursor-expired recovery to re-anchor the connection, so it
    /// does not re-read its recovery window on every tick (a window listing never
    /// advances a cursor). Implementations must treat this as a read-only probe: it
    /// never lists messages and never mutates <paramref name="connection"/>.
    /// </summary>
    Task<string?> GetCurrentCursorAsync(EmailConnection connection, CancellationToken ct);

    /// <summary>Fetches a single message by provider id, with its full body.</summary>
    Task<EmailMessage> GetAsync(EmailConnection connection, string messageId, CancellationToken ct);
}
