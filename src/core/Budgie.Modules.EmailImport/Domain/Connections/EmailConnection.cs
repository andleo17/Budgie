using Budgie.Shared.Abstractions;
using Budgie.Shared.Enums;

namespace Budgie.Modules.EmailImport.Domain.Connections;

/// <summary>
/// A user mailbox connected to Budgie through an email provider (OAuth, read-only).
/// The provider is discriminated by <see cref="Provider"/> and resolved through the
/// <c>IEmailProviderResolver</c>; Domain stays provider-neutral and never mentions a vendor.
/// <see cref="SyncCursor"/> is an opaque string whose meaning is known only by the provider.
/// </summary>
public class EmailConnection : Entity
{
    /// <summary>Default minimum confidence required to auto-commit a parsed email.</summary>
    public const decimal DefaultAutoCommitMinConfidence = 0.85m;

    /// <summary>Maximum length accepted for <see cref="Email"/> (matches the DDL column).</summary>
    private const int EmailMaxLength = 255;

    /// <summary>Maximum length accepted for <see cref="LastError"/> (matches the DDL column).</summary>
    private const int LastErrorMaxLength = 500;

    /// <summary>Maximum length accepted for <see cref="SyncCursor"/> (matches the DDL column).</summary>
    private const int SyncCursorMaxLength = 255;

    public Guid Id { get; internal set; }
    public Guid UserId { get; internal set; }
    public EmailProvider Provider { get; internal set; }
    public string Email { get; internal set; } = null!;
    public string EncryptedCredentials { get; internal set; } = null!;
    public string? SyncCursor { get; internal set; }
    public bool SyncEnabled { get; internal set; } = true;
    public decimal AutoCommitMinConfidence { get; internal set; }
    public DateTime? LastSyncAt { get; internal set; }
    public string? LastError { get; internal set; }
    public int LastSkippedCount { get; internal set; }

    /// <summary>Public parameterless constructor for Dapper materialization.</summary>
    public EmailConnection() { }

    /// <summary>
    /// Creates a connection with sync enabled and no cursor yet.
    /// </summary>
    /// <exception cref="ArgumentException">
    /// Email is null/empty or longer than 255 characters; credentials are null/empty;
    /// or <paramref name="autoCommitMinConfidence"/> is outside [0, 1].
    /// </exception>
    public static EmailConnection Create(
        Guid userId,
        EmailProvider provider,
        string email,
        string encryptedCredentials,
        decimal autoCommitMinConfidence = DefaultAutoCommitMinConfidence)
    {
        var normalizedEmail = Guard.NotNullOrEmpty(email, nameof(email)).Trim();
        if (normalizedEmail.Length > EmailMaxLength)
            throw new ArgumentException($"Email cannot exceed {EmailMaxLength} characters.", nameof(email));

        var credentials = Guard.NotNullOrEmpty(encryptedCredentials, nameof(encryptedCredentials));

        return new EmailConnection
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Provider = provider,
            Email = normalizedEmail,
            EncryptedCredentials = credentials,
            SyncEnabled = true,
            AutoCommitMinConfidence = NormalizeThreshold(autoCommitMinConfidence, nameof(autoCommitMinConfidence)),
            LastSkippedCount = 0
        };
    }

    /// <summary>
    /// Records a successful sync pass: updates <see cref="LastSyncAt"/> and
    /// <see cref="LastSkippedCount"/>, and clears <see cref="LastError"/>.
    /// The cursor is advanced separately through <see cref="AdvanceCursor"/>.
    /// </summary>
    public void RecordSuccess(DateTime now, int skippedCount)
    {
        LastSyncAt = now;
        LastSkippedCount = skippedCount;
        LastError = null;
    }

    /// <summary>
    /// Records a sync failure. The message is truncated to 500 characters and
    /// <see cref="LastSyncAt"/> is intentionally NOT touched: a failed pass is not a sync.
    /// </summary>
    public void RecordError(string error)
    {
        LastError = Truncate(error, LastErrorMaxLength);
    }

    /// <summary>Disables sync (e.g. after a 401 / revoked credential). The connection is kept.</summary>
    public void Disable() => SyncEnabled = false;

    /// <summary>
    /// Advances (or resets, with <c>null</c>) the opaque provider cursor.
    /// Callers must only do this after the whole batch has been processed.
    /// </summary>
    public void AdvanceCursor(string? cursor)
    {
        SyncCursor = Truncate(cursor, SyncCursorMaxLength);
    }

    /// <summary>
    /// Changes the minimum confidence required to auto-commit a parsed email.
    /// </summary>
    /// <exception cref="ArgumentException">Value is outside [0, 1].</exception>
    public void ChangeAutoCommitThreshold(decimal value)
    {
        AutoCommitMinConfidence = NormalizeThreshold(value, nameof(value));
    }

    private static decimal NormalizeThreshold(decimal value, string paramName)
    {
        if (value < 0m || value > 1m)
            throw new ArgumentException($"{paramName} must be between 0 and 1.", paramName);
        return Math.Round(value, 2);
    }

    private static string? Truncate(string? value, int maxLength) =>
        value is null || value.Length <= maxLength ? value : value[..maxLength];
}
