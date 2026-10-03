using Budgie.Modules.EmailImport.Infrastructure.Email.Gmail;

namespace Budgie.Modules.EmailImport.Infrastructure;

/// <summary>
/// Configuration of the EmailImport module, bound from the <c>EmailImport</c> section
/// (spec §10). Vendor entries live under <see cref="Providers"/>, keyed by the lowercase
/// provider name, so adding a provider is adding a configuration entry, not code.
/// </summary>
public sealed class EmailImportOptions
{
    /// <summary>Configuration section this options class is bound from.</summary>
    public const string SectionName = "EmailImport";

    /// <summary>
    /// Master switch. When false the sync worker does nothing (slice 13 also uses it to
    /// skip registering the hosted service, so integration tests never race a timer).
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Minutes between sync ticks. Clamped to a minimum of 1 by the worker.</summary>
    public int SyncIntervalMinutes { get; set; } = 10;

    /// <summary>
    /// Base64 (or plaintext) 32-byte key for at-rest credential encryption. When absent
    /// the key is derived from <c>Jwt:Secret</c> with a warning (see AesSecretProtector).
    /// </summary>
    public string? EncryptionKey { get; set; }

    /// <summary>
    /// Per-provider configuration, keyed by the lowercase provider name
    /// (e.g. <c>gmail</c> → <c>EmailImport:Providers:gmail</c>).
    /// </summary>
    public Dictionary<string, GmailOptions>? Providers { get; set; }
}
