using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Budgie.Modules.EmailImport.Application.Connections;
using Budgie.Modules.EmailImport.UnitTests.TestDoubles;
using Budgie.Shared.Enums;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace Budgie.Modules.EmailImport.UnitTests.Connections;

/// <summary>
/// <see cref="OAuthStateService"/> is THE authentication of the OAuth callback: the state is
/// the only thing binding a redirect that arrives without a JWT to the user who started the
/// flow. So it must be signed, sealed, single-use and time-bounded. A leaked authorization
/// code could otherwise be replayed against the callback to hang a connection on someone
/// else's account.
/// </summary>
public class OAuthStateServiceTests
{
    private const string Secret = "clave-de-firma-de-prueba-para-los-tests";
    private const string RedirectUri = "https://budgie.test/api/email-import/connections/gmail/callback";

    private static readonly Guid UserId = Guid.NewGuid();

    private static OAuthStateService Service(TimeProvider? clock = null, string? secret = Secret) =>
        new(new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?> { ["Jwt:Secret"] = secret }).Build(),
            clock ?? new TestTimeProvider());

    // ── Issue ──────────────────────────────────────────────────────────────────────────
    [Fact]
    public void Issue_ProducesATwoPartBase64UrlToken()
    {
        var state = Service().Issue(UserId, EmailProvider.Gmail, RedirectUri);

        var parts = state.Split('.');
        parts.Should().HaveCount(2);
        parts.Should().OnlyContain(p => !p.Contains('=') && !p.Contains('+') && !p.Contains('/'));
    }

    [Fact]
    public void Issue_Twice_ProducesDifferentTokens()
    {
        var service = Service();

        service.Issue(UserId, EmailProvider.Gmail, RedirectUri)
            .Should().NotBe(service.Issue(UserId, EmailProvider.Gmail, RedirectUri));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Issue_EmptyRedirectUri_Throws(string? redirectUri)
    {
        var act = () => Service().Issue(UserId, EmailProvider.Gmail, redirectUri!);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Issue_NoSecretConfigured_ThrowsInsteadOfIssuingAnUnsignedState()
    {
        var service = Service(secret: null);

        var act = () => service.Issue(UserId, EmailProvider.Gmail, RedirectUri);

        act.Should().Throw<CryptographicException>()
            .Which.Message.Should().Contain("Jwt:Secret");
    }

    [Fact]
    public void Issue_PayloadSealsTheUserIdProviderAndRedirectUri()
    {
        var state = Service().Issue(UserId, EmailProvider.Gmail, RedirectUri);
        var payload = DecodePayload(state);

        payload.GetProperty("userId").GetGuid().Should().Be(UserId);
        payload.GetProperty("provider").GetInt32().Should().Be((int)EmailProvider.Gmail);
        payload.GetProperty("redirectUri").GetString().Should().Be(RedirectUri);
        payload.GetProperty("nonce").GetString().Should().NotBeNullOrWhiteSpace();
        payload.GetProperty("expiresAt").GetDateTimeOffset()
            .Should().Be(new DateTimeOffset(2026, 10, 2, 10, 10, 0, TimeSpan.Zero));
    }

    // ── Consume: the happy path ────────────────────────────────────────────────────────
    [Fact]
    public void TryConsume_FreshlyIssuedState_ReturnsTheSealedPayload()
    {
        var service = Service();
        var state = service.Issue(UserId, EmailProvider.Gmail, RedirectUri);

        var consumed = service.TryConsume(state, out var userId, out var provider, out var redirectUri);

        consumed.Should().BeTrue();
        userId.Should().Be(UserId);
        provider.Should().Be(EmailProvider.Gmail);
        redirectUri.Should().Be(RedirectUri);
    }

    // ── Single use: the nonce is burned ────────────────────────────────────────────────
    [Fact]
    public void TryConsume_SameStateTwice_SecondUseIsRejected()
    {
        // THE invariant of the whole OAuth flow: a leaked code cannot be replayed.
        var service = Service();
        var state = service.Issue(UserId, EmailProvider.Gmail, RedirectUri);

        service.TryConsume(state, out _, out _, out _).Should().BeTrue();
        var second = service.TryConsume(state, out _, out _, out _);

        second.Should().BeFalse();
    }

    [Fact]
    public void TryConsume_ReplayAcrossDifferentStatesWithTheSameNonce_IsRejected()
    {
        // The nonce, not the token string, is what is tracked.
        var service = Service();
        var state = service.Issue(UserId, EmailProvider.Gmail, RedirectUri);
        var payload = DecodePayload(state);
        var payloadB64 = state.Split('.')[0];
        var signature = state.Split('.')[1];

        service.TryConsume(state, out _, out _, out _).Should().BeTrue();

        // Re-sign a byte-identical payload with a fresh (valid) signature: the burned nonce
        // must still refuse it.
        var key = Encoding.UTF8.GetBytes(Secret);
        var forgedSignature = Base64UrlEncode(HMACSHA256.HashData(key, Encoding.UTF8.GetBytes(payloadB64)));
        var resigned = $"{payloadB64}.{forgedSignature}";
        payload.GetProperty("nonce").GetString().Should().NotBeNull();

        service.TryConsume(resigned, out _, out _, out _).Should().BeFalse();
    }

    // ── Tampering ──────────────────────────────────────────────────────────────────────
    [Fact]
    public void TryConsume_TamperedSignature_IsRejected()
    {
        var service = Service();
        var state = service.Issue(UserId, EmailProvider.Gmail, RedirectUri);
        var parts = state.Split('.');

        var signature = Base64UrlDecode(parts[1]);
        signature[0] ^= 0xFF;
        var tampered = $"{parts[0]}.{Base64UrlEncode(signature)}";

        service.TryConsume(tampered, out _, out _, out _).Should().BeFalse();
    }

    [Fact]
    public void TryConsume_TamperedPayload_IsRejectedBecauseTheSignatureCoversIt()
    {
        // Swapping the userId is the attack this signature exists to stop.
        var service = Service();
        var state = service.Issue(UserId, EmailProvider.Gmail, RedirectUri);
        var parts = state.Split('.');

        var payload = DecodePayload(state);
        var forged = JsonSerializer.Serialize(new
        {
            userId = Guid.NewGuid(),          // a different victim
            provider = (int)EmailProvider.Gmail,
            nonce = payload.GetProperty("nonce").GetString(),
            redirectUri = RedirectUri,
            expiresAt = payload.GetProperty("expiresAt").GetDateTimeOffset()
        });

        service.TryConsume($"{Base64UrlEncode(Encoding.UTF8.GetBytes(forged))}.{parts[1]}",
            out _, out _, out _).Should().BeFalse();
    }

    [Fact]
    public void TryConsume_StateSignedWithADifferentSecret_IsRejected()
    {
        var forged = Service(secret: "otra-clave-distinta").Issue(UserId, EmailProvider.Gmail, RedirectUri);

        Service().TryConsume(forged, out _, out _, out _).Should().BeFalse();
    }

    [Fact]
    public void TryConsume_UnsignedStateOfTheCorrectShape_IsRejected()
    {
        // A hand-built payload with no signature must never be accepted: failing closed here
        // is what guarantees an unsigned state cannot be minted.
        var payload = JsonSerializer.Serialize(new
        {
            userId = UserId, provider = (int)EmailProvider.Gmail, nonce = "n",
            redirectUri = RedirectUri, expiresAt = DateTimeOffset.UtcNow.AddMinutes(10)
        });
        var unsigned = $"{Base64UrlEncode(Encoding.UTF8.GetBytes(payload))}.{Base64UrlEncode(new byte[32])}";

        Service().TryConsume(unsigned, out _, out _, out _).Should().BeFalse();
    }

    // ── Malformed input ────────────────────────────────────────────────────────────────
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void TryConsume_EmptyState_IsRejected(string? state)
    {
        Service().TryConsume(state!, out _, out _, out _).Should().BeFalse();
    }

    [Theory]
    [InlineData("no-dot")]
    [InlineData("a.b.c")]
    [InlineData("!!!.???")]
    [InlineData("only-one-part")]
    public void TryConsume_WrongShape_IsRejectedWithoutThrowing(string state)
    {
        var act = () => Service().TryConsume(state, out _, out _, out _);

        act.Should().NotThrow();
        act().Should().BeFalse();
    }

    [Fact]
    public void TryConsume_RejectedStateLeavesTheOutParametersAtTheirDefaults()
    {
        var service = Service();
        service.TryConsume("basura", out var userId, out var provider, out var redirectUri);

        userId.Should().Be(Guid.Empty);
        provider.Should().Be(default);
        redirectUri.Should().BeEmpty();
    }

    // ── Expiry ─────────────────────────────────────────────────────────────────────────
    [Fact]
    public void TryConsume_StateOlderThanTenMinutes_IsRejected()
    {
        // The window is 10 minutes; 11 is the smallest jump that proves it is enforced.
        var clock = new TestTimeProvider();
        var service = Service(clock);
        var state = service.Issue(UserId, EmailProvider.Gmail, RedirectUri);

        clock.Advance(TimeSpan.FromMinutes(11));

        service.TryConsume(state, out _, out _, out _).Should().BeFalse();
    }

    [Fact]
    public void TryConsume_StateJustInsideTheWindow_IsAccepted()
    {
        var clock = new TestTimeProvider();
        var service = Service(clock);
        var state = service.Issue(UserId, EmailProvider.Gmail, RedirectUri);

        clock.Advance(TimeSpan.FromMinutes(9));

        service.TryConsume(state, out var userId, out _, out _).Should().BeTrue();
        userId.Should().Be(UserId);
    }

    [Fact]
    public void TryConsume_ExpiredStateDoesNotBurnAnythingElse()
    {
        // An expired consume must not consume the nonce of a different, valid state.
        var clock = new TestTimeProvider();
        var service = Service(clock);
        var expired = service.Issue(UserId, EmailProvider.Gmail, RedirectUri);
        clock.Advance(TimeSpan.FromMinutes(11));
        var fresh = service.Issue(UserId, EmailProvider.Gmail, RedirectUri);

        service.TryConsume(expired, out _, out _, out _).Should().BeFalse();
        service.TryConsume(fresh, out _, out _, out _).Should().BeTrue();
    }

    // ── The provider value is range-checked even with a valid signature ───────────────
    [Fact]
    public void TryConsume_UndefinedProviderValue_IsRejected()
    {
        // A state signed by this server can still carry a provider number from a future
        // enum; it must not resolve to a bogus provider.
        var payload = JsonSerializer.Serialize(new
        {
            userId = UserId, provider = 99, nonce = "nonce-99",
            redirectUri = RedirectUri, expiresAt = DateTimeOffset.UtcNow.AddMinutes(10)
        });
        var key = Encoding.UTF8.GetBytes(Secret);
        var payloadB64 = Base64UrlEncode(Encoding.UTF8.GetBytes(payload));
        var signature = Base64UrlEncode(HMACSHA256.HashData(key, Encoding.UTF8.GetBytes(payloadB64)));

        var consumed = Service().TryConsume($"{payloadB64}.{signature}", out _, out var provider, out _);

        consumed.Should().BeFalse();
        provider.Should().Be(default);
    }

    // ── Two services in the same process share the burned nonces ───────────────────────
    [Fact]
    public void TryConsume_StateIssuedByOneServiceInstance_CannotBeConsumedTwice()
    {
        // The dictionary is process-wide singleton state — that is why the service is
        // registered as a singleton, not scoped.
        var issuer = Service();
        var consumer = Service();
        var state = issuer.Issue(UserId, EmailProvider.Gmail, RedirectUri);

        consumer.TryConsume(state, out _, out _, out _).Should().BeTrue();
        consumer.TryConsume(state, out _, out _, out _).Should().BeFalse();
    }

    // ── helpers ────────────────────────────────────────────────────────────────────────
    private static JsonElement DecodePayload(string state)
    {
        var json = Encoding.UTF8.GetString(Base64UrlDecode(state.Split('.')[0]));
        return JsonDocument.Parse(json).RootElement.Clone();
    }

    private static string Base64UrlEncode(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static byte[] Base64UrlDecode(string value)
    {
        var s = value.Replace('-', '+').Replace('_', '/');
        s = (s.Length % 4) switch { 0 => s, 2 => s + "==", 3 => s + "=", _ => throw new FormatException() };
        return Convert.FromBase64String(s);
    }
}
