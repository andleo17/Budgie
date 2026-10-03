namespace Budgie.Modules.EmailImport.Application.Connections;

/// <summary>
/// Provider-neutral OAuth credential produced by an authorization code exchange or a
/// token refresh.
/// </summary>
/// <param name="AccessToken">
/// The fresh access token in clear text, for immediate use by the caller. Short-lived.
/// </param>
/// <param name="EncryptedRefreshToken">
/// The provider's protected credential envelope: an opaque, self-describing string
/// (produced with <c>ISecretProtector</c>) that must be persisted <b>verbatim</b> in
/// <c>EmailConnection.EncryptedCredentials</c>. Only the provider that issued it ever
/// decrypts it; despite the historical field name it protects the provider's full
/// credential payload, which always includes the refresh token and may include the
/// access token and its expiry so that later syncs can avoid a network round-trip.
/// </param>
/// <param name="ExpiresAt">
/// When the access token expires, decoded from the token itself (no network call) or
/// from the provider's token response.
/// </param>
public sealed record EmailCredentials(
    string AccessToken,
    string EncryptedRefreshToken,
    DateTimeOffset ExpiresAt);
