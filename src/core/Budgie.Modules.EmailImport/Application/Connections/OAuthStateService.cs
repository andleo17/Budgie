using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Budgie.Shared.Enums;
using Microsoft.Extensions.Configuration;

namespace Budgie.Modules.EmailImport.Application.Connections;

/// <summary>
/// Issues and consumes the signed <c>state</c> token of the mailbox OAuth flow.
///
/// <para><b>Why this exists (the only authentication of the callback).</b>
/// The provider redirects the user's browser to <c>GET /api/email-import/connections/{provider}/callback</c>
/// <b>without</b> Budgie's JWT: the token lives in localStorage and never travels on that
/// navigation, so the callback endpoint is deliberately left without <c>[Authorize]</c>
/// (spec §6). The state token is therefore the ONLY thing binding the callback to the user
/// who started the connection. It carries the <c>userId</c> inside a payload signed with
/// the server's <c>Jwt:Secret</c> (HMAC-SHA256), so the callback resolves the owner from
/// the state itself and <b>must not</b> read <c>ICurrentUserProvider</c>: a valid state
/// issued for user A stays bound to user A no matter whose browser completes the flow.</para>
///
/// <para><b>Why the state is single-use (nonce burning).</b>
/// An authorization <c>code</c> that leaks (proxy logs, browser history, referrer) could
/// otherwise be replayed against the callback to re-hang a connection. Every state carries
/// a random nonce; consuming a state burns its nonce in a process-wide dictionary, so the
/// second use of the same state fails even inside the 10-minute window.</para>
///
/// <para><b>What the signature covers.</b>
/// The HMAC is computed over the base64url payload <c>{userId, provider, nonce,
/// redirectUri, expiresAt}</c>. Any tampering with the payload — including swapping the
/// userId — breaks the signature. The provider travels inside the payload so the callback
/// never has to trust a provider value sent by the browser.</para>
/// </summary>
/// <remarks>
/// Registered as a <b>singleton</b>: the consumed-nonce dictionary is shared process-wide
/// state. The signing key is read lazily on every call from <c>IConfiguration</c> so a
/// missing <c>Jwt:Secret</c> surfaces as a controlled exception at issue/consume time
/// instead of at construction.
/// </remarks>
internal sealed class OAuthStateService(IConfiguration configuration, TimeProvider timeProvider)
{
    /// <summary>How long an issued state stays valid (spec: 10 minutes).</summary>
    private static readonly TimeSpan StateLifetime = TimeSpan.FromMinutes(10);

    /// <summary>Random bytes of the single-use nonce embedded in every state.</summary>
    private const int NonceSize = 32;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    /// <summary>
    /// Nonces already consumed, mapped to the expiry of the state that carried them. The
    /// value (not just a flag) is stored so entries can be pruned: a nonce is only needed
    /// while its state could still be valid.
    /// </summary>
    private readonly ConcurrentDictionary<string, DateTimeOffset> _consumedNonces = new();

    /// <summary>Production constructor: wall clock, shared by every scope.</summary>
    public OAuthStateService(IConfiguration configuration)
        : this(configuration, TimeProvider.System)
    {
    }

    /// <summary>
    /// Issues a signed state for starting the OAuth flow of one provider.
    /// </summary>
    /// <param name="userId">The user the connection will belong to; sealed inside the payload.</param>
    /// <param name="provider">The provider being connected; sealed inside the payload so the
    /// callback never trusts a provider value sent by the browser.</param>
    /// <param name="redirectUri">Where the provider must send the user back.</param>
    /// <returns>Base64url token of the form <c>payload.signature</c>.</returns>
    /// <exception cref="CryptographicException">No <c>Jwt:Secret</c> is configured.</exception>
    public string Issue(Guid userId, EmailProvider provider, string redirectUri)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(redirectUri);
        var key = ResolveSigningKey();

        var payload = new StatePayload(
            userId,
            (int)provider,
            Convert.ToBase64String(RandomNumberGenerator.GetBytes(NonceSize)),
            redirectUri,
            timeProvider.GetUtcNow().Add(StateLifetime));

        var payloadBytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(payload, JsonOptions));
        var encodedPayload = Base64UrlEncode(payloadBytes);
        var signature = HMACSHA256.HashData(key, Encoding.UTF8.GetBytes(encodedPayload));

        return $"{encodedPayload}.{Base64UrlEncode(signature)}";
    }

    /// <summary>
    /// Validates a state and burns its nonce. Returns <c>false</c> on any malformed,
    /// tampered, expired or already-consumed state; the caller maps that to
    /// <c>Error.Validation("EmailImport.InvalidState")</c>.
    /// </summary>
    /// <remarks>
    /// The <c>userId</c> returned is the one sealed in the state, <b>not</b> the user
    /// making the call: the state is what binds the callback to its owner.
    /// </remarks>
    public bool TryConsume(string state, out Guid userId, out EmailProvider provider, out string redirectUri)
    {
        userId = default;
        provider = default;
        redirectUri = string.Empty;

        if (string.IsNullOrWhiteSpace(state))
            return false;

        var parts = state.Split('.');
        if (parts.Length != 2)
            return false;

        byte[] payloadBytes;
        byte[] presentedSignature;
        try
        {
            payloadBytes = Base64UrlDecode(parts[0]);
            presentedSignature = Base64UrlDecode(parts[1]);
        }
        catch (FormatException)
        {
            return false; // not base64url: malformed, never a valid state
        }

        var key = ResolveSigningKey();
        var expectedSignature = HMACSHA256.HashData(key, Encoding.UTF8.GetBytes(parts[0]));

        // Constant-time comparison: the signature is a secret-equivalent value; a
        // timing side channel must not help forge one.
        if (!CryptographicOperations.FixedTimeEquals(presentedSignature, expectedSignature))
            return false;

        StatePayload? payload;
        try
        {
            payload = JsonSerializer.Deserialize<StatePayload>(payloadBytes, JsonOptions);
        }
        catch (JsonException)
        {
            return false;
        }

        if (payload is null)
            return false;

        var now = timeProvider.GetUtcNow();
        if (now > payload.ExpiresAt)
            return false; // expired: 10-minute window

        if (!Enum.IsDefined(typeof(EmailProvider), payload.Provider))
            return false; // signature valid but payload from a future/unknown provider value

        PruneExpiredNonces(now);

        // Burn the nonce BEFORE handing out the payload: a state is single-use, and
        // consuming it must be atomic even under concurrent replays.
        if (!_consumedNonces.TryAdd(payload.Nonce, payload.ExpiresAt))
            return false; // replay of an already-consumed state

        userId = payload.UserId;
        provider = (EmailProvider)payload.Provider;
        redirectUri = payload.RedirectUri;
        return true;
    }

    /// <summary>
    /// Drops consumed nonces whose state has already expired: they can never be requested
    /// again, so keeping them would only grow the dictionary forever.
    /// </summary>
    private void PruneExpiredNonces(DateTimeOffset now)
    {
        foreach (var entry in _consumedNonces)
        {
            if (entry.Value < now)
                _consumedNonces.TryRemove(entry);
        }
    }

    /// <summary>
    /// Reads <c>Jwt:Secret</c> on every call. A missing secret is a deployment defect:
    /// failing here (instead of silently skipping the signature) guarantees an unsigned
    /// state can never be issued or accepted.
    /// </summary>
    private byte[] ResolveSigningKey()
    {
        var secret = configuration["Jwt:Secret"];
        if (string.IsNullOrWhiteSpace(secret))
            throw new CryptographicException(
                "Jwt:Secret is not configured; the OAuth state cannot be signed or verified.");

        return Encoding.UTF8.GetBytes(secret);
    }

    private static string Base64UrlEncode(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static byte[] Base64UrlDecode(string value)
    {
        var s = value.Replace('-', '+').Replace('_', '/');
        s = (s.Length % 4) switch
        {
            0 => s,
            2 => $"{s}==",
            3 => $"{s}=",
            _ => throw new FormatException("Invalid base64url length.")
        };
        return Convert.FromBase64String(s);
    }

    /// <summary>
    /// Signed payload of a state. <see cref="Provider"/> is stored as its int value so the
    /// token stays valid across enum renames; it is range-checked on consume.
    /// </summary>
    private sealed record StatePayload(
        Guid UserId,
        int Provider,
        string Nonce,
        string RedirectUri,
        DateTimeOffset ExpiresAt);
}
