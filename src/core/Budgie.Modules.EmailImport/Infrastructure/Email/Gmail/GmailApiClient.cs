using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using Budgie.Modules.EmailImport.Application;
using Budgie.Modules.EmailImport.Infrastructure.Logging;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;

namespace Budgie.Modules.EmailImport.Infrastructure.Email.Gmail;

/// <summary>
/// Thin HTTP client for the Gmail REST + OAuth2 endpoints. Deliberately built on
/// <see cref="HttpClient"/> and <see cref="System.Text.Json"/> only — no vendor SDK
/// package, which would drag a large dependency and trimming/AOT problems for three
/// endpoints' worth of JSON.
/// </summary>
/// <remarks>
/// Every failure is surfaced as a typed <see cref="EmailProviderException"/>
/// (<see cref="EmailProviderError.Unauthorized"/> for 401/403, <see cref="EmailProviderError.RateLimited"/>
/// for 429, <see cref="EmailProviderError.Transient"/> for 5xx and network failures).
/// A non-success response is never half-parsed: the body is only deserialized after a
/// success status, and a malformed success body is also a <see cref="EmailProviderError.Transient"/>
/// error rather than a partially populated DTO.
/// </remarks>
/// <summary>
/// The mailbox operations <see cref="GmailEmailProvider"/> depends on. Extracted as a port so
/// the provider's cursor logic can be tested without HTTP: <see cref="GmailApiClient"/> is
/// sealed, so NSubstitute cannot proxy it.
/// </summary>
internal interface IGmailMailboxApi
{
    string BuildAuthorizationUri(string redirectUri, string state);
    Task<GmailTokenResponse> ExchangeCodeAsync(string code, string redirectUri, CancellationToken ct);
    Task<GmailTokenResponse> RefreshAsync(string refreshToken, CancellationToken ct);
    Task<IReadOnlyList<string>> ListMessageIdsAsync(string accessToken, string query, CancellationToken ct);
    Task<GmailHistoryPage> ListHistoryAsync(string accessToken, string startHistoryId, CancellationToken ct);
    Task<GmailProfileResponse?> GetProfileAsync(string accessToken, CancellationToken ct);
    Task<GmailMessageResponse?> GetMessageAsync(string accessToken, string messageId, CancellationToken ct);
}

internal sealed class GmailApiClient(
    HttpClient httpClient,
    IOptions<GmailOptions> options,
    ILogger<GmailApiClient> logger) : IGmailMailboxApi
{
    private readonly GmailOptions _options = options.Value;

    /// <summary>
    /// Builds the OAuth authorization URI. <c>access_type=offline</c> and
    /// <c>prompt=consent</c> are <b>mandatory</b>: without offline access the provider
    /// never issues a refresh token, and without forcing consent it issues one only on
    /// the very first authorization — a re-connect would silently receive none. Missing
    /// either of them is the number-one cause of "it works but the token dies in an
    /// hour", so both are always sent.
    /// </summary>
    public string BuildAuthorizationUri(string redirectUri, string state)
    {
        ValidateConfigured();

        var query = new Dictionary<string, string?>
        {
            ["client_id"] = _options.ClientId,
            ["redirect_uri"] = redirectUri,
            ["response_type"] = "code",
            ["scope"] = string.Join(' ', _options.Scopes),
            ["state"] = state,
            ["access_type"] = "offline",
            ["prompt"] = "consent",
            ["include_granted_scopes"] = "true"
        };

        return QueryHelpers.AddQueryString(
            $"{_options.AuthBaseAddress}o/oauth2/v2/auth", query);
    }

    /// <summary>Exchanges the authorization code for tokens (form-encoded token endpoint call).</summary>
    public Task<GmailTokenResponse> ExchangeCodeAsync(string code, string redirectUri, CancellationToken ct) =>
        RequestTokenAsync(
            new Dictionary<string, string>
            {
                ["grant_type"] = "authorization_code",
                ["code"] = code,
                ["redirect_uri"] = redirectUri,
                ["client_id"] = RequireConfigured(_options.ClientId, nameof(_options.ClientId)),
                ["client_secret"] = RequireConfigured(_options.ClientSecret, nameof(_options.ClientSecret))
            },
            ct);

    /// <summary>Exchanges a refresh token for a fresh access token (form-encoded token endpoint call).</summary>
    public Task<GmailTokenResponse> RefreshAsync(string refreshToken, CancellationToken ct) =>
        RequestTokenAsync(
            new Dictionary<string, string>
            {
                ["grant_type"] = "refresh_token",
                ["refresh_token"] = refreshToken,
                ["client_id"] = RequireConfigured(_options.ClientId, nameof(_options.ClientId)),
                ["client_secret"] = RequireConfigured(_options.ClientSecret, nameof(_options.ClientSecret))
            },
            ct);

    /// <summary>
    /// Lists message ids matching a search query (e.g. <c>newer_than:7d</c>). Returns
    /// ids only; full messages are fetched one by one with <see cref="GetMessageAsync"/>.
    /// </summary>
    public async Task<IReadOnlyList<string>> ListMessageIdsAsync(
        string accessToken, string query, CancellationToken ct)
    {
        var uri = QueryHelpers.AddQueryString(
            MePath("messages/list"), new Dictionary<string, string?> { ["q"] = query });

        using var response = await SendAuthorizedAsync(uri, accessToken, "users.messages.list", ct);
        var payload = await ReadSuccessAsync<GmailMessageListResponse>(response, "users.messages.list", ct);

        return payload.Messages?
            .Select(m => m.Id)
            .Where(id => !string.IsNullOrEmpty(id))
            .ToList() ?? [];
    }

    /// <summary>
    /// Builds a REST path under the authenticated mailbox.
    /// <para>
    /// The Gmail REST API is <c>gmail/v1/users/{userId}/&lt;resource&gt;</c>, and <c>me</c> is
    /// the alias for the authenticated user — <c>users/messages/list</c> is NOT a valid path
    /// and returns 404 against the real API. The RPC-style names used elsewhere in this file
    /// (<c>users.messages.list</c>) are method identifiers, not URLs; do not transcribe one
    /// into the other.
    /// </para>
    /// </summary>
    private static string MePath(string resource) => $"users/me/{resource}";

    /// <summary>
    /// Lists messages added since <paramref name="startHistoryId"/>. A 404 means the
    /// history window has expired (the provider keeps roughly 10 days of history); it is
    /// translated to <see cref="EmailProviderError.CursorExpired"/> so the caller can
    /// reset the cursor and fall back to the recovery window.
    /// </summary>
    public async Task<GmailHistoryPage> ListHistoryAsync(
        string accessToken, string startHistoryId, CancellationToken ct)
    {
        var uri = QueryHelpers.AddQueryString(
            MePath("history/list"),
            new Dictionary<string, string?>
            {
                ["startHistoryId"] = startHistoryId,
                ["historyTypes"] = "messageAdded"
            });

        using var response = await SendAuthorizedAsync(uri, accessToken, "users.history.list", ct);
        if (response.StatusCode == HttpStatusCode.NotFound)
            throw new EmailProviderException(
                EmailProviderError.CursorExpired,
                "The provider no longer has history for the persisted cursor; it must be reset.");

        var payload = await ReadSuccessAsync<GmailHistoryListResponse>(response, "users.history.list", ct);

        var ids = payload.History?
            .SelectMany(h => h.MessagesAdded ?? [])
            .Select(m => m.Message?.Id)
            .Where(id => !string.IsNullOrEmpty(id))
            .Cast<string>()
            .Distinct(StringComparer.Ordinal)
            .ToList() ?? [];

        return new GmailHistoryPage(ids, payload.LatestHistoryId);
    }

    /// <summary>
    /// Fetches the mailbox profile (<c>users.getProfile</c>). The profile's
    /// <c>historyId</c> is the mailbox's current history position — the correct value to
    /// re-anchor a sync cursor after a history expiry (slice 11: without it, a
    /// connection whose cursor expired would re-read its 7-day recovery window on every
    /// tick forever, because <c>messages.list</c> never advances a cursor).
    /// </summary>
    public async Task<GmailProfileResponse?> GetProfileAsync(
        string accessToken, CancellationToken ct)
    {
        using var response = await SendAuthorizedAsync(
            MePath("getProfile"), accessToken, "users.getProfile", ct);
        return await ReadSuccessAsync<GmailProfileResponse>(response, "users.getProfile", ct);
    }

    /// <summary>
    /// Fetches one message with <c>format=full</c>. Returns <c>null</c> on 404 — the
    /// message was deleted between listing and fetching; the caller skips it.
    /// </summary>
    public async Task<GmailMessageResponse?> GetMessageAsync(
        string accessToken, string messageId, CancellationToken ct)
    {
        var uri = $"{MePath($"messages/{Uri.EscapeDataString(messageId)}")}?format=full";

        using var response = await SendAuthorizedAsync(uri, accessToken, "users.messages.get", ct);
        if (response.StatusCode == HttpStatusCode.NotFound)
            return null;

        return await ReadSuccessAsync<GmailMessageResponse>(response, "users.messages.get", ct);
    }

    /// <summary>
    /// Decodes the expiry (<c>exp</c> claim) of a JWT access token from its base64url
    /// payload — <b>no network call involved</b>: "refreshing" the view of an access
    /// token's validity is pure decoding, only acquiring a NEW token touches the
    /// network. Returns <c>null</c> when the token is not a parseable JWT.
    /// </summary>
    public static DateTimeOffset? TryDecodeExpiry(string accessToken)
    {
        var parts = accessToken.Split('.');
        if (parts.Length < 2)
            return null;

        try
        {
            var payload = GmailBase64Url.DecodeToString(parts[1]);
            using var document = JsonDocument.Parse(payload);
            if (document.RootElement.TryGetProperty("exp", out var exp) &&
                exp.TryGetInt64(out var seconds))
                return DateTimeOffset.FromUnixTimeSeconds(seconds);

            return null;
        }
        catch (Exception ex) when (ex is FormatException or JsonException or ArgumentException)
        {
            return null;
        }
    }

    private async Task<GmailTokenResponse> RequestTokenAsync(
        Dictionary<string, string> form, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, _options.TokenUrl)
        {
            Content = new FormUrlEncodedContent(form)
        };

        using var response = await SendAsync(request, "oauth2.token", ct);
        var token = await ReadSuccessAsync<GmailTokenResponse>(response, "oauth2.token", ct);

        if (string.IsNullOrEmpty(token.AccessToken))
            throw new EmailProviderException(
                EmailProviderError.Transient,
                "oauth2.token: the provider returned a token response without an access token.");

        return token;
    }

    private async Task<HttpResponseMessage> SendAuthorizedAsync(
        string relativeUri, string accessToken, string operation, CancellationToken ct)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, relativeUri);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return await SendAsync(request, operation, ct);
    }

    private async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, string operation, CancellationToken ct)
    {
        try
        {
            return await httpClient.SendAsync(request, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            LogMessages.ProviderNetworkError(logger, operation);
            throw new EmailProviderException(
                EmailProviderError.Transient,
                $"{operation}: the provider could not be reached (network error or timeout).",
                ex);
        }
    }

    /// <summary>
    /// Deserializes a success body. Never called for non-success statuses — error
    /// responses are turned into typed exceptions first, so a half-understood error
    /// payload can never surface as a partially populated DTO.
    /// </summary>
    private async Task<T> ReadSuccessAsync<T>(
        HttpResponseMessage response, string operation, CancellationToken ct)
    {
        var body = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
        {
            LogMessages.ProviderCallFailed(logger, operation, (int)response.StatusCode);

            throw response.StatusCode switch
            {
                HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden =>
                    new EmailProviderException(
                        EmailProviderError.Unauthorized,
                        $"{operation}: the provider rejected the credential (HTTP {(int)response.StatusCode})."),
                HttpStatusCode.TooManyRequests =>
                    new EmailProviderException(
                        EmailProviderError.RateLimited,
                        $"{operation}: the provider is throttling requests (HTTP 429)."),
                _ when (int)response.StatusCode >= 500 =>
                    new EmailProviderException(
                        EmailProviderError.Transient,
                        $"{operation}: the provider returned a server error (HTTP {(int)response.StatusCode})."),
                _ =>
                    new EmailProviderException(
                        EmailProviderError.Transient,
                        $"{operation}: unexpected provider response (HTTP {(int)response.StatusCode}).")
            };
        }

        try
        {
            return JsonSerializer.Deserialize<T>(body)
                ?? throw new EmailProviderException(
                    EmailProviderError.Transient,
                    $"{operation}: the provider returned an empty response body.");
        }
        catch (JsonException ex)
        {
            throw new EmailProviderException(
                EmailProviderError.Transient,
                $"{operation}: the provider returned a malformed response body.",
                ex);
        }
    }

    private void ValidateConfigured()
    {
        RequireConfigured(_options.ClientId, nameof(_options.ClientId));
        RequireConfigured(_options.ClientSecret, nameof(_options.ClientSecret));
        if (_options.Scopes is not { Count: > 0 })
            throw new EmailProviderException(
                EmailProviderError.NotConfigured,
                "EmailImport:Providers:gmail:Scopes is empty; at least the read-only mail scope is required.");
    }

    private static string RequireConfigured(string? value, string name) =>
        !string.IsNullOrWhiteSpace(value)
            ? value
            : throw new EmailProviderException(
                EmailProviderError.NotConfigured,
                $"EmailImport:Providers:gmail:{name} is not configured.");
}

/// <summary>Token endpoint response (only the fields Budgie needs).</summary>
internal sealed record GmailTokenResponse(
    [property: JsonPropertyName("access_token")] string AccessToken,
    [property: JsonPropertyName("refresh_token")] string? RefreshToken,
    [property: JsonPropertyName("expires_in")] int ExpiresIn);

/// <summary>Reference to a message in a list response.</summary>
internal sealed record GmailMessageRef(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("threadId")] string? ThreadId);

/// <summary><c>users.messages.list</c> response.</summary>
internal sealed record GmailMessageListResponse(
    [property: JsonPropertyName("messages")] GmailMessageRef[]? Messages);

/// <summary>One entry of a <c>users.history.list</c> response.</summary>
internal sealed record GmailHistoryRecord(
    [property: JsonPropertyName("messagesAdded")] GmailMessageAdded[]? MessagesAdded);

/// <summary>A message-added history entry.</summary>
internal sealed record GmailMessageAdded(
    [property: JsonPropertyName("message")] GmailMessageRef? Message);

/// <summary><c>users.getProfile</c> response (only the fields Budgie needs).</summary>
internal sealed record GmailProfileResponse(
    [property: JsonPropertyName("emailAddress")] string? EmailAddress,
    [property: JsonPropertyName("historyId")] string? HistoryId);

/// <summary><c>users.history.list</c> response.</summary>
internal sealed record GmailHistoryListResponse(
    [property: JsonPropertyName("history")] GmailHistoryRecord[]? History,
    [property: JsonPropertyName("latestHistoryId")] string? LatestHistoryId);

/// <summary>
/// Normalized result of a history query: the distinct message ids added since the
/// cursor, plus the cursor to persist after the batch (the provider's latest history id).
/// </summary>
internal sealed record GmailHistoryPage(IReadOnlyList<string> MessageIds, string? LatestHistoryId);

/// <summary>
/// Base64url decoding (RFC 4648 §5: URL-safe alphabet, no padding) shared by the
/// message body decoder and the JWT payload decoder.
/// </summary>
internal static class GmailBase64Url
{
    public static string DecodeToString(string value)
    {
        var bytes = DecodeToBytes(value);
        return System.Text.Encoding.UTF8.GetString(bytes);
    }

    public static byte[] DecodeToBytes(string value)
    {
        var s = value.Replace('-', '+').Replace('_', '/');
        s = s switch
        {
            var v when v.Length % 4 == 2 => v + "==",
            var v when v.Length % 4 == 3 => v + "=",
            var v when v.Length % 4 == 1 => throw new FormatException("Invalid base64url length."),
            _ => s
        };

        return Convert.FromBase64String(s);
    }
}
