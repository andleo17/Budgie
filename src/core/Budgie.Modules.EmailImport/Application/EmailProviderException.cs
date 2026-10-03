namespace Budgie.Modules.EmailImport.Application;

/// <summary>
/// Typed failure modes of an email provider call. Lives in Application (not next to any
/// provider implementation) because the sync service catches and switches on these
/// codes without knowing which vendor produced them.
/// </summary>
public enum EmailProviderError
{
    /// <summary>The credential was rejected or revoked (HTTP 401/403, invalid_grant). The connection must be disabled.</summary>
    Unauthorized = 0,

    /// <summary>The provider is throttling us (HTTP 429). Retry on a later tick; the cursor must NOT be advanced.</summary>
    RateLimited = 1,

    /// <summary>Network failure or provider-side server error (5xx, timeout). Retry on a later tick.</summary>
    Transient = 2,

    /// <summary>
    /// The persisted sync cursor is no longer valid (the provider no longer has that
    /// history). Reset the cursor and fall back to the recovery window.
    /// </summary>
    CursorExpired = 3,

    /// <summary>The provider client is not configured (missing client id/secret/scope). An operational problem, not a user one.</summary>
    NotConfigured = 4
}

/// <summary>
/// Provider call failure with a typed <see cref="EmailProviderError"/> so callers can
/// react without parsing vendor error payloads. Lives in Application — not in any
/// provider folder — because the sync service (which knows nothing about vendors)
/// catches it and maps each error code to a sync behaviour.
/// </summary>
public sealed class EmailProviderException : Exception
{
    /// <summary>The typed failure mode.</summary>
    public EmailProviderError Error { get; }

    public EmailProviderException(EmailProviderError error, string message)
        : base(message) => Error = error;

    public EmailProviderException(EmailProviderError error, string message, Exception? innerException)
        : base(message, innerException) => Error = error;
}
