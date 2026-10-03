namespace Budgie.Modules.EmailImport.Application.Abstractions;

/// <summary>
/// Protects secrets at rest (currently the OAuth credential stored in
/// <c>EmailConnection.EncryptedCredentials</c>). Implementations encrypt on write and
/// decrypt on read; neither plaintext nor ciphertext may ever be logged.
/// </summary>
public interface ISecretProtector
{
    /// <summary>
    /// Encrypts <paramref name="plaintext"/> into an opaque, self-describing string
    /// that can be persisted as-is.
    /// </summary>
    /// <param name="plaintext">The secret to protect. May be empty but not null.</param>
    /// <returns>The protected value.</returns>
    string Protect(string plaintext);

    /// <summary>
    /// Decrypts a value previously produced by <see cref="Protect"/>.
    /// </summary>
    /// <param name="protectedValue">The protected value.</param>
    /// <returns>The original plaintext.</returns>
    /// <exception cref="System.Security.Cryptography.CryptographicException">
    /// The value has an unknown format, a malformed segment, or fails authentication
    /// (tampered ciphertext or wrong key). A corrupt value must never be returned silently.
    /// </exception>
    string Unprotect(string protectedValue);
}
