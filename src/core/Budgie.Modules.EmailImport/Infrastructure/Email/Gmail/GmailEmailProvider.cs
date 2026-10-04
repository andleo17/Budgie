using System.Security.Cryptography;
using System.Text.Json;
using Budgie.Modules.EmailImport.Application;
using Budgie.Modules.EmailImport.Application.Abstractions;
using Budgie.Modules.EmailImport.Application.Connections;
using Budgie.Modules.EmailImport.Domain.Connections;
using Budgie.Modules.EmailImport.Domain.Parsing;
using Budgie.Modules.EmailImport.Infrastructure.Logging;
using Budgie.Shared.Enums;

namespace Budgie.Modules.EmailImport.Infrastructure.Email.Gmail;

/// <summary>
/// Gmail implementation of <see cref="IEmailProviderClient"/>.
/// </summary>
/// <remarks>
/// <para>
/// <b>Read-only guarantee.</b> The OAuth scope requested is <c>gmail.readonly</c>: this
/// provider only ever reads the mailbox. It never marks messages as read, never applies
/// or removes anything from the mailbox, and performs no kind of write against it — the
/// only outbound calls are read-only queries and the OAuth token endpoint. That is a
/// promise made to the user and it must stay true for every method here.
/// </para>
/// <para>
/// <b>Encrypted credential envelope.</b> The blob persisted in
/// <see cref="EmailConnection.EncryptedCredentials"/> is
/// <c>ISecretProtector.Protect</c> over JSON <c>{ accessToken, refreshToken, expiresAt }</c>.
/// The refresh token is therefore never in clear text beyond the scope of a single
/// method below. <c>connection.EncryptedCredentials</c> may be updated in memory when a
/// preemptive refresh happens; persisting it remains the sync service's job, after the
/// whole batch has been processed (same rule as the cursor).
/// </para>
/// </remarks>
internal sealed class GmailEmailProvider(
    ISecretProtector protector,
    IGmailMailboxApi api,
    ILogger<GmailEmailProvider> logger) : IEmailProviderClient
{
    /// <summary>Recovery window used when the connection has no cursor yet, or after a cursor reset.</summary>
    private const string RecentWindowQuery = "newer_than:7d";

    /// <summary>
    /// Points the connection at the mailbox's current <c>historyId</c> so the next tick uses
    /// the cheap incremental <c>history.list</c> instead of re-reading the recovery window.
    /// In-memory only; the sync service persists the connection after the batch.
    /// <para>
    /// Best-effort by design: a failed profile read leaves the cursor null, which is safe
    /// (re-reading is deduped by the staging UNIQUE, just wasteful), and the client already
    /// logged the failure.
    /// </para>
    /// </summary>
    private async Task TryAnchorCursorAsync(EmailConnection connection, string accessToken, CancellationToken ct)
    {
        try
        {
            var profile = await api.GetProfileAsync(accessToken, ct);
            if (profile?.HistoryId is not null)
                connection.AdvanceCursor(profile.HistoryId);
        }
        catch (EmailProviderException)
        {
            // Best-effort; see remarks.
        }
    }

    /// <summary>
    /// An access token closer than this to its expiry is refreshed preemptively, so a
    /// long batch never starts with a token that dies mid-flight.
    /// </summary>
    private static readonly TimeSpan RefreshLeeway = TimeSpan.FromSeconds(60);

    /// <inheritdoc />
    public EmailProvider Provider => EmailProvider.Gmail;

    /// <inheritdoc />
    /// <remarks>
    /// Gmail derives everything from configuration, not from the connection, so
    /// <paramref name="connection"/> is unused here; the parameter exists on the port for
    /// providers that need existing connection state while authorizing.
    /// </remarks>
    public Task<EmailAuthorizationUri> BuildAuthorizationUriAsync(
        EmailConnection connection, string redirectUri, string state, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        return Task.FromResult(new EmailAuthorizationUri(api.BuildAuthorizationUri(redirectUri, state), state));
    }

    /// <inheritdoc />
    /// <remarks>
    /// <paramref name="encryptedCredentials"/> is unused: a fresh consent replaces the
    /// whole Gmail credential. The parameter exists on the port for providers that must
    /// revoke or carry the previous credential explicitly.
    /// </remarks>
    public async Task<EmailCredentials> ExchangeCodeAsync(
        string encryptedCredentials, string code, string redirectUri, CancellationToken ct)
    {
        var token = await api.ExchangeCodeAsync(code, redirectUri, ct);
        if (string.IsNullOrEmpty(token.RefreshToken))
            throw new EmailProviderException(
                EmailProviderError.NotConfigured,
                "The provider did not return a refresh token; the authorization must be requested with offline access and forced consent.");

        var credential = new StoredCredential(token.AccessToken, token.RefreshToken, ResolveExpiry(token));
        return ToCredentials(credential, email: await ReadMailboxAddressAsync(token.AccessToken, ct));
    }

    /// <summary>
    /// Reads the connected mailbox's address so the UI can show which mailbox it is.
    /// </summary>
    /// <remarks>
    /// <b>One extra round trip per connect.</b> The profile endpoint already exists in this class
    /// — it is what anchors the <c>historyId</c> cursor — but that happens on the first
    /// <c>ListNewAsync</c> tick, i.e. AFTER this exchange. So connecting costs a second call to the
    /// same endpoint: no new endpoint and no new scope (<c>gmail.readonly</c> already covers it),
    /// and the single response carries both <c>emailAddress</c> and <c>historyId</c>.
    /// </remarks>
    /// <remarks>
    /// <b>Best-effort, and deliberately so.</b> A failure here is swallowed and reported as null:
    /// the mailbox really is connected, and refusing the connection because an informational
    /// endpoint was unreachable would be strictly worse than not knowing the address. An empty
    /// catch always looks like a bug to whoever reads it later, hence the comment.
    /// </remarks>
    private async Task<string?> ReadMailboxAddressAsync(string accessToken, CancellationToken ct)
    {
        try
        {
            var profile = await api.GetProfileAsync(accessToken, ct);
            return string.IsNullOrWhiteSpace(profile?.EmailAddress) ? null : profile.EmailAddress;
        }
        catch (EmailProviderException)
        {
            // Best-effort; see remarks above.
            return null;
        }
    }

    /// <inheritdoc />
    public async Task<EmailCredentials> RefreshAsync(string encryptedCredentials, CancellationToken ct)
    {
        var credential = Decrypt(encryptedCredentials);
        return ToCredentials(await RefreshCoreAsync(credential, ct));
    }

    /// <inheritdoc />
    /// <remarks>
    /// <b>Cursor handling.</b> With no cursor the provider reads the 7-day recovery
    /// window. With a cursor it reads the provider history since that point. A 404 from
    /// the history query means the provider no longer keeps history that far back
    /// (roughly ten days): the cursor is reset to null in memory and the call is retried
    /// once with the 7-day window. Re-reading is harmless — the staging
    /// <c>UNIQUE("UserId","MessageId")</c> discards anything already seen. The reset is
    /// surfaced as <see cref="EmailProviderError.CursorExpired"/> on the retry path only
    /// if it happens again (defensive; the 7-day window does not use cursors).
    /// </remarks>
    public async Task<IReadOnlyList<EmailMessage>> ListNewAsync(EmailConnection connection, CancellationToken ct)
    {
        var accessToken = await GetAccessTokenAsync(connection, ct);

        IReadOnlyList<string> messageIds;
        if (connection.SyncCursor is null)
        {
            // First tick of a freshly connected mailbox. Re-anchor to the mailbox's current
            // historyId BEFORE listing the recovery window: messages.list never advances a
            // cursor, so without this the connection would re-download and re-parse the
            // whole 7-day window on EVERY tick forever, leaning on the staging UNIQUE to
            // discard everything. The window is query-based (newer_than:7d) and ignores the
            // cursor, so anchoring first cannot shrink it; every message it returns has a
            // history position <= the profile's, so the next history.list sees them too.
            // The sync service persists the connection after the batch.
            await TryAnchorCursorAsync(connection, accessToken, ct);

            messageIds = await api.ListMessageIdsAsync(accessToken, RecentWindowQuery, ct);
        }
        else
        {
            try
            {
                var page = await api.ListHistoryAsync(accessToken, connection.SyncCursor, ct);
                messageIds = page.MessageIds;

                // In-memory only: the sync service persists the connection after the batch.
                if (page.LatestHistoryId is not null)
                    connection.AdvanceCursor(page.LatestHistoryId);
            }
            catch (EmailProviderException ex) when (ex.Error == EmailProviderError.CursorExpired)
            {
                LogMessages.HistoryCursorExpired(logger);
                connection.AdvanceCursor(null);

                // Re-anchor at the mailbox's current historyId BEFORE listing the
                // recovery window (slice 11 trap fix): users.messages.list never
                // advances a cursor, so without this the connection would re-read the
                // 7-day window on EVERY tick forever. The window listing is query-based
                // (newer_than:7d) and ignores the cursor, so re-anchoring first cannot
                // shrink it; and every message the window returns has a history position
                // ≤ the profile's, so the next history.list sees them too — the staging
                // UNIQUE("UserId","MessageId") dedupes the overlap. Best-effort: a failed
                // profile read leaves the cursor null (safe, just wasteful) and its
                // failure is already logged by the client itself.
                await TryAnchorCursorAsync(connection, accessToken, ct);

                messageIds = await api.ListMessageIdsAsync(accessToken, RecentWindowQuery, ct);
            }
        }

        var messages = new List<EmailMessage>(messageIds.Count);
        foreach (var messageId in messageIds)
        {
            var dto = await api.GetMessageAsync(accessToken, messageId, ct);
            if (dto is null)
                continue; // deleted between listing and fetching; nothing to import

            messages.Add(GmailMessageMapper.Map(dto));
        }

        return messages;
    }

    /// <inheritdoc />
    /// <remarks>
    /// Gmail's cursor is the mailbox <c>historyId</c>, which <c>users.getProfile</c>
    /// reports as its current position. This is the probe the sync service uses to
    /// re-anchor a connection after a cursor expiry (see <see cref="ListNewAsync"/>).
    /// </remarks>
    public async Task<string?> GetCurrentCursorAsync(EmailConnection connection, CancellationToken ct)
    {
        var accessToken = await GetAccessTokenAsync(connection, ct);
        var profile = await api.GetProfileAsync(accessToken, ct);
        return profile?.HistoryId;
    }

    /// <inheritdoc />
    public async Task<EmailMessage> GetAsync(EmailConnection connection, string messageId, CancellationToken ct)
    {
        var accessToken = await GetAccessTokenAsync(connection, ct);
        var dto = await api.GetMessageAsync(accessToken, messageId, ct)
            ?? throw new EmailProviderException(
                EmailProviderError.Transient,
                $"The message '{messageId}' no longer exists at the provider.");

        return GmailMessageMapper.Map(dto);
    }

    /// <summary>
    /// Returns a usable access token for the connection's credential, refreshing
    /// preemptively when it is about to expire. Determining that expiry needs
    /// <b>no network call</b> — the access token is a JWT whose <c>exp</c> claim is
    /// decoded locally; only acquiring a NEW token touches the token endpoint.
    /// </summary>
    private async Task<string> GetAccessTokenAsync(EmailConnection connection, CancellationToken ct)
    {
        var credential = Decrypt(connection.EncryptedCredentials);

        var expiry = GmailApiClient.TryDecodeExpiry(credential.AccessToken) ?? credential.ExpiresAt;
        if (expiry - DateTimeOffset.UtcNow >= RefreshLeeway)
            return credential.AccessToken;

        var refreshed = await RefreshCoreAsync(credential, ct);

        // In-memory only, same contract as the cursor: the sync service persists the
        // connection after the batch. Keeps the at-rest envelope current without this
        // provider depending on any repository.
        connection.EncryptedCredentials = protector.Protect(JsonSerializer.Serialize(refreshed));
        LogMessages.AccessTokenRefreshed(logger);

        return refreshed.AccessToken;
    }

    private async Task<StoredCredential> RefreshCoreAsync(StoredCredential credential, CancellationToken ct)
    {
        var token = await api.RefreshAsync(credential.RefreshToken, ct);

        // The token endpoint omits refresh_token on refresh; keep the one we already hold.
        var refreshToken = string.IsNullOrEmpty(token.RefreshToken)
            ? credential.RefreshToken
            : token.RefreshToken;

        return new StoredCredential(token.AccessToken, refreshToken, ResolveExpiry(token));
    }

    /// <summary>
    /// Builds the neutral credential from the stored envelope. <paramref name="email"/> is only
    /// known on the authorization-exchange path (see <see cref="ReadMailboxAddressAsync"/>); the
    /// refresh path leaves it null because the address already lives on the connection.
    /// </summary>
    private EmailCredentials ToCredentials(StoredCredential credential, string? email = null) =>
        new(
            credential.AccessToken,
            protector.Protect(JsonSerializer.Serialize(credential)),
            credential.ExpiresAt,
            email);

    private StoredCredential Decrypt(string encrypted)
    {
        string json;
        try
        {
            json = protector.Unprotect(encrypted);
        }
        catch (CryptographicException ex)
        {
            // Undecryptable envelope (wrong key, truncated value): the mailbox must be
            // reconnected, which is what the Unauthorized path triggers downstream.
            throw new EmailProviderException(
                EmailProviderError.Unauthorized,
                "The stored mailbox credential could not be decrypted; the mailbox must be reconnected.",
                ex);
        }

        StoredCredential? credential;
        try
        {
            credential = JsonSerializer.Deserialize<StoredCredential>(json);
        }
        catch (JsonException ex)
        {
            throw new EmailProviderException(
                EmailProviderError.Unauthorized,
                "The stored mailbox credential is malformed; the mailbox must be reconnected.",
                ex);
        }

        if (credential is null ||
            string.IsNullOrEmpty(credential.AccessToken) ||
            string.IsNullOrEmpty(credential.RefreshToken))
            throw new EmailProviderException(
                EmailProviderError.Unauthorized,
                "The stored mailbox credential is incomplete; the mailbox must be reconnected.");

        return credential;
    }

    /// <summary>
    /// Prefers decoding <c>exp</c> from the JWT itself (no network); falls back to the
    /// token response's <c>expires_in</c>, then to a conservative one-hour lifetime.
    /// </summary>
    private static DateTimeOffset ResolveExpiry(GmailTokenResponse token) =>
        GmailApiClient.TryDecodeExpiry(token.AccessToken)
        ?? (token.ExpiresIn > 0
            ? DateTimeOffset.UtcNow.AddSeconds(token.ExpiresIn)
            : DateTimeOffset.UtcNow.AddHours(1));
}

/// <summary>
/// Decrypted shape of the credential envelope at rest. Vendor-internal; serialized to
/// JSON and protected with <see cref="ISecretProtector"/> before leaving this class.
/// </summary>
internal sealed record StoredCredential(
    string AccessToken,
    string RefreshToken,
    DateTimeOffset ExpiresAt);
