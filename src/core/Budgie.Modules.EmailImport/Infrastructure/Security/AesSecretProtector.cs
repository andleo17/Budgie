using System.Security.Cryptography;
using System.Text;
using Budgie.Modules.EmailImport.Application.Abstractions;
using Budgie.Modules.EmailImport.Infrastructure.Logging;
using Microsoft.Extensions.Configuration;

namespace Budgie.Modules.EmailImport.Infrastructure.Security;

/// <summary>
/// AES-256-GCM encryption for secrets at rest (the OAuth credential persisted in
/// <c>EmailConnection.EncryptedCredentials</c>). Output format (spec §11):
/// <c>v1:&lt;base64(nonce)&gt;:&lt;base64(ciphertext)&gt;:&lt;base64(tag)&gt;</c>.
/// The <c>v1</c> prefix allows rotating the algorithm later without ambiguity.
/// The key is resolved from <c>EmailImport:EncryptionKey</c> (base64 or plaintext,
/// always hashed to 32 bytes with SHA-256) or, failing that, derived from
/// <c>Jwt:Secret</c> with a one-time warning. Neither plaintext nor ciphertext is
/// ever logged.
/// </summary>
internal sealed class AesSecretProtector(
    IConfiguration configuration,
    ILogger<AesSecretProtector> logger) : ISecretProtector
{
    private const string VersionPrefix = "v1";
    private const int NonceSize = 12;
    private const int TagSize = 16;
    private const int KeySize = 32;

    // Process-wide, so the fallback warning is emitted exactly once regardless of how the
    // protector is registered. AesSecretProtector holds no mutable state — the key is
    // read-only and every AesGcm instance is created per call — so it is thread-safe and
    // the registration lifetime must not be relied on to bound the warning.
    private static int _fallbackWarningLogged;

    private readonly byte[] _key = ResolveKey(configuration, logger);

    /// <inheritdoc />
    public string Protect(string plaintext)
    {
        ArgumentNullException.ThrowIfNull(plaintext);

        var nonce = RandomNumberGenerator.GetBytes(NonceSize);
        var plaintextBytes = Encoding.UTF8.GetBytes(plaintext);
        var ciphertext = new byte[plaintextBytes.Length];
        var tag = new byte[TagSize];

        using var aes = new AesGcm(_key, TagSize);
        aes.Encrypt(nonce, plaintextBytes, ciphertext, tag);

        return string.Join(':',
            VersionPrefix,
            Convert.ToBase64String(nonce),
            Convert.ToBase64String(ciphertext),
            Convert.ToBase64String(tag));
    }

    /// <inheritdoc />
    public string Unprotect(string protectedValue)
    {
        ArgumentNullException.ThrowIfNull(protectedValue);

        var segments = protectedValue.Split(':');
        if (segments.Length != 4 || !segments[0].Equals(VersionPrefix, StringComparison.Ordinal))
            throw new CryptographicException(
                "Protected value has an unknown format: expected 'v1:<nonce>:<ciphertext>:<tag>'.");

        var nonce = DecodeSegment(segments[1], "nonce");
        var ciphertext = DecodeSegment(segments[2], "ciphertext");
        var tag = DecodeSegment(segments[3], "tag");

        if (nonce.Length != NonceSize || tag.Length != TagSize)
            throw new CryptographicException(
                "Protected value has an invalid nonce or tag length.");

        var plaintextBytes = new byte[ciphertext.Length];

        // Decrypt authenticates the tag: a tampered ciphertext or a wrong key throws
        // (AuthenticationTagMismatchException derives from CryptographicException)
        // instead of silently returning a corrupt secret.
        using var aes = new AesGcm(_key, TagSize);
        aes.Decrypt(nonce, ciphertext, tag, plaintextBytes);

        return Encoding.UTF8.GetString(plaintextBytes);
    }

    private static byte[] DecodeSegment(string value, string segmentName)
    {
        try
        {
            return Convert.FromBase64String(value);
        }
        catch (FormatException ex)
        {
            // The segment value itself is never included in the message: it may hold ciphertext.
            throw new CryptographicException(
                $"Protected value has an invalid base64 '{segmentName}' segment.", ex);
        }
    }

    private static byte[] ResolveKey(IConfiguration configuration, ILogger logger)
    {
        var configuredKey = configuration["EmailImport:EncryptionKey"];
        if (!string.IsNullOrWhiteSpace(configuredKey))
            return DeriveKey(configuredKey);

        var jwtSecret = configuration["Jwt:Secret"];
        if (!string.IsNullOrWhiteSpace(jwtSecret))
        {
            // Exactly one warning for the whole process, on the Jwt:Secret fallback path.
            if (Interlocked.Exchange(ref _fallbackWarningLogged, 1) == 0)
                LogMessages.KeyDerivedFromJwtSecret(logger);

            return DeriveKey(jwtSecret);
        }

        throw new InvalidOperationException(
            "No encryption key configured: set 'EmailImport:EncryptionKey' (preferred) " +
            "or 'Jwt:Secret' (fallback) so the OAuth credential can be encrypted at rest.");
    }

    // Accepts base64 or plaintext: hashing the UTF-8 bytes yields exactly 32 key bytes
    // in both cases, so a misconfigured length can never break the protector.
    private static byte[] DeriveKey(string value) =>
        SHA256.HashData(Encoding.UTF8.GetBytes(value));
}
