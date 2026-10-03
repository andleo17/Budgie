using System.Security.Cryptography;
using System.Text;
using Budgie.Modules.EmailImport.Infrastructure.Security;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace Budgie.Modules.EmailImport.UnitTests.Security;

/// <summary>
/// <see cref="AesSecretProtector"/> guards the OAuth refresh token at rest. The properties
/// that matter: round-trip fidelity, a random nonce per call (never a deterministic blob a
/// DBA could correlate), and a LOUD failure on anything corrupt — silently returning a wrong
/// secret is worse than refusing to decrypt.
/// </summary>
public class AesSecretProtectorTests
{
    private const string Plaintext =
        """{"accessToken":"ya29.a0AfB_x","refreshToken":"1//0eXyZ-secret-value","expiresAt":"2026-10-02T11:00:00Z"}""";

    private static AesSecretProtector Protector(string? encryptionKey, string? jwtSecret = "jwt-secret-value")
    {
        var values = new Dictionary<string, string?>();
        if (encryptionKey is not null) values["EmailImport:EncryptionKey"] = encryptionKey;
        if (jwtSecret is not null) values["Jwt:Secret"] = jwtSecret;

        var configuration = new ConfigurationBuilder().AddInMemoryCollection(values).Build();
        return new AesSecretProtector(
            configuration, Substitute.For<ILogger<AesSecretProtector>>());
    }

    // ── Round trip ─────────────────────────────────────────────────────────────────────
    [Theory]
    [InlineData("token-simple")]
    [InlineData("{\"refreshToken\":\"1//0eXyZ\"}")]
    [InlineData("con acentos: ñ € £")]
    [InlineData("")]
    public void Unprotect_Protect_RoundTripsExactly(string plaintext)
    {
        var protector = Protector("clave-de-prueba");

        protector.Unprotect(protector.Protect(plaintext)).Should().Be(plaintext);
    }

    [Fact]
    public void Protect_ProducesTheSelfDescribingFourSegmentFormat()
    {
        var segments = Protector("clave-de-prueba").Protect("secreto").Split(':');

        segments.Should().HaveCount(4);
        segments[0].Should().Be("v1");   // the version prefix allows rotating the algorithm later
        segments[1].Length.Should().BeGreaterThan(0);  // nonce
        segments[2].Length.Should().BeGreaterThan(0);  // ciphertext
        segments[3].Length.Should().Be(24);           // 16-byte GCM tag, base64
    }

    [Fact]
    public void Protect_CarriesNoPlaintextAnywhereInTheOutput()
    {
        var protectedValue = Protector("clave-de-prueba").Protect(Plaintext);

        protectedValue.Should().NotContain("refreshToken");
        protectedValue.Should().NotContain("1//0eXyZ-secret-value");
        protectedValue.Should().NotContain("ya29");
    }

    // ── A fresh nonce per call ─────────────────────────────────────────────────────────
    [Fact]
    public void Protect_TwiceOnTheSameText_ProducesDifferentCiphertexts()
    {
        // A deterministic blob would let anyone with DB read access tell that two users
        // hold the same refresh token — and would leak equality across snapshots.
        var protector = Protector("clave-de-prueba");

        var first = protector.Protect(Plaintext);
        var second = protector.Protect(Plaintext);

        first.Should().NotBe(second);
    }

    [Fact]
    public void Protect_TwiceOnTheSameText_BothDecryptToTheSameValue()
    {
        var protector = Protector("clave-de-prueba");

        var first = protector.Protect(Plaintext);
        var second = protector.Protect(Plaintext);

        protector.Unprotect(first).Should().Be(Plaintext);
        protector.Unprotect(second).Should().Be(Plaintext);
    }

    // ── Tampering is refused, never tolerated ──────────────────────────────────────────
    [Fact]
    public void Unprotect_TamperedCiphertext_ThrowsInsteadOfReturningCorruptData()
    {
        var protector = Protector("clave-de-prueba");
        var segments = protector.Protect(Plaintext).Split(':');

        var bytes = Convert.FromBase64String(segments[2]);
        bytes[0] ^= 0xFF;   // flip a bit
        var tampered = string.Join(':', segments[0], segments[1], Convert.ToBase64String(bytes), segments[3]);

        var act = () => protector.Unprotect(tampered);

        // AES-GCM authenticates the tag, so a modified ciphertext can never decrypt.
        act.Should().Throw<CryptographicException>();
    }

    [Fact]
    public void Unprotect_TamperedTag_Throws()
    {
        var protector = Protector("clave-de-prueba");
        var segments = protector.Protect(Plaintext).Split(':');

        var tag = Convert.FromBase64String(segments[3]);
        tag[0] ^= 0xFF;
        var tampered = string.Join(':', segments[0], segments[1], segments[2], Convert.ToBase64String(tag));

        var act = () => protector.Unprotect(tampered);
        act.Should().Throw<CryptographicException>();
    }

    [Fact]
    public void Unprotect_ProtectedWithADifferentKey_Throws()
    {
        // The realistic incident: an operator rotates the key and the old rows stop
        // decrypting. That MUST surface as an error, not as a garbage credential.
        var other = Protector("otra-clave-distinta").Protect(Plaintext);

        var act = () => Protector("clave-de-prueba").Unprotect(other);
        act.Should().Throw<CryptographicException>();
    }

    [Theory]
    [InlineData("no-es-un-envelope")]
    [InlineData("v2:bm9uY2U=:Y2lwaGVy:dGFn")]                  // unknown version prefix
    [InlineData("v1:bm9uY2U=:Y2lwaGVy")]                        // too few segments
    [InlineData("v1:bm9uY2U=:Y2lwaGVy:dGFn:extra")]             // too many segments
    [InlineData("v1:bm9uY2U=:not-base64!!:dGFn")]                // malformed base64
    [InlineData("v1:bm9uY2U=:Y2lwaGVy:dGFn")]                   // valid base64, wrong tag length
    public void Unprotect_MalformedValue_ThrowsWithACryptographicException(string value)
    {
        var act = () => Protector("clave-de-prueba").Unprotect(value);
        act.Should().Throw<CryptographicException>();
    }

    [Fact]
    public void Unprotect_MalformedBase64Message_NeverEchoesTheValue()
    {
        // The segment may hold ciphertext; leaking it into an exception message could put
        // it in a log file.
        var act = () => Protector("clave-de-prueba").Unprotect("v1:nonce:SEGREDOENCLARODEDUCIDO:tag");

        act.Should().Throw<CryptographicException>()
            .Which.Message.Should().NotContain("SEGREDOENCLARODEDUCIDO");
    }

    [Fact]
    public void Protect_NullPlaintext_Throws()
    {
        var act = () => Protector("clave-de-prueba").Protect(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Unprotect_NullValue_Throws()
    {
        var act = () => Protector("clave-de-prueba").Unprotect(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    // ── Key resolution ─────────────────────────────────────────────────────────────────
    [Fact]
    public void Protect_KeyFromEncryptionKey_WorksWithoutAnyJwtSecret()
    {
        // The preferred configuration: a dedicated key, no dependency on the JWT secret.
        var protector = Protector("clave-de-prueba", jwtSecret: null);

        protector.Unprotect(protector.Protect(Plaintext)).Should().Be(Plaintext);
    }

    [Fact]
    public void Protect_Base64EncryptionKey_IsAcceptedAsWellAsPlaintext()
    {
        // Either form works because the key is always hashed to exactly 32 bytes.
        var base64 = Convert.ToBase64String(Encoding.UTF8.GetBytes("clave-de-prueba-en-utf8"));

        Protector(base64).Unprotect(Protector(base64).Protect(Plaintext)).Should().Be(Plaintext);
    }

    [Fact]
    public void Protect_ShortKey_IsHashedToTheRequiredLengthAndStillWorks()
    {
        // A misconfigured key length can never break the protector.
        Protector("corta").Unprotect(Protector("corta").Protect(Plaintext)).Should().Be(Plaintext);
    }

    [Fact]
    public void Protect_NoEncryptionKey_FallsBackToTheJwtSecret()
    {
        var protector = Protector(encryptionKey: null, jwtSecret: "jwt-secret-value");

        protector.Unprotect(protector.Protect(Plaintext)).Should().Be(Plaintext);
    }

    [Fact]
    public void Protect_EmptyEncryptionKey_FallsBackToTheJwtSecret()
    {
        // Whitespace counts as absent: configuration written as "" must not silently
        // produce a key derived from an empty string.
        var protector = Protector(encryptionKey: "   ", jwtSecret: "jwt-secret-value");

        protector.Unprotect(protector.Protect(Plaintext)).Should().Be(Plaintext);
    }

    [Fact]
    public void Protect_NoKeyAtAll_ThrowsAtConstruction()
    {
        // Failing here guarantees an unsigned/unkeyed state can never be produced.
        var act = () => Protector(encryptionKey: null, jwtSecret: null);

        act.Should().Throw<InvalidOperationException>()
            .Which.Message.Should().Contain("EmailImport:EncryptionKey");
    }
}
