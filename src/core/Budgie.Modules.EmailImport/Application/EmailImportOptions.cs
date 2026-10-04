namespace Budgie.Modules.EmailImport.Application;

/// <summary>
/// Configuration of the EmailImport module, bound from the <c>EmailImport</c> section
/// (spec §10). It lives in Application because the handlers (<c>SyncNowCommandHandler</c>) and
/// the worker (<c>EmailSyncWorker</c>, whose tick delegates the work to Application services)
/// must read it, and Application may not depend on Infrastructure types.
/// </summary>
/// <remarks>
/// There is deliberately NO <c>Providers</c> dictionary here. A dictionary of provider entries
/// would have to be typed <c>Dictionary&lt;string, GmailOptions&gt;</c>, dragging GmailOptions
/// (an Infrastructure type) back into Application — the exact layering violation this type's
/// location exists to prevent. Each provider's configuration lives in its OWN section
/// (<c>EmailImport:Providers:gmail</c>) and is bound directly by that provider's Infrastructure
/// options class — see <c>Infrastructure.Email.Gmail.GmailOptions.SectionName</c> — which the
/// module bootstrap registers next to this one. So the configuration path in
/// <c>docker-compose.yml</c>/<c>.env.example</c> does not move; only the POCO that declared a
/// dictionary nobody ever read disappears.
/// </remarks>
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
}
