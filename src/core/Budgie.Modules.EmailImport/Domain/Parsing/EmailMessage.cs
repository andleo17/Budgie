namespace Budgie.Modules.EmailImport.Domain.Parsing;

/// <summary>
/// Provider-neutral snapshot of an email: the raw data the parsers work with. Carries no
/// provider vocabulary on purpose — Gmail, Outlook or IMAP all map into this shape.
/// </summary>
/// <param name="MessageId">Provider-unique message id. Part of the idempotency key.</param>
/// <param name="ThreadId">Provider thread id, when the provider exposes one.</param>
/// <param name="FromAddress">
/// Sender as supplied by the provider. May be a raw MIME header
/// (<c>Nombre &lt;correo@dominio&gt;</c>); use <see cref="GetFromAddress"/> to normalize it.
/// </param>
/// <param name="Subject">Email subject, as supplied by the provider.</param>
/// <param name="Body">Raw body (plain text or HTML source; conversion happens in Infrastructure).</param>
/// <param name="ReceivedAt">When the provider says the message was received.</param>
public sealed record EmailMessage(
    string MessageId,
    string? ThreadId,
    string? FromAddress,
    string? Subject,
    string Body,
    DateTimeOffset ReceivedAt)
{
    /// <summary>
    /// Creates a message from a provider that already supplies a normalized From address
    /// (no display name, no angle brackets). The address is trimmed; everything else is kept as-is.
    /// </summary>
    public static EmailMessage Create(
        string messageId,
        string? threadId,
        string? fromAddress,
        string? subject,
        string body,
        DateTimeOffset receivedAt) =>
        new(messageId, threadId, fromAddress?.Trim(), subject, body, receivedAt);

    /// <summary>
    /// Returns the bare sender address. If <see cref="FromAddress"/> is a raw MIME header
    /// (<c>Nombre &lt;correo@dominio&gt;</c>), only the address inside the angle brackets is
    /// returned; otherwise the value is trimmed and returned as-is. Null stays null.
    /// </summary>
    public string? GetFromAddress()
    {
        if (string.IsNullOrWhiteSpace(FromAddress))
            return FromAddress;

        var start = FromAddress.IndexOf('<');
        var end = FromAddress.IndexOf('>');
        if (start >= 0 && end > start)
            return FromAddress[(start + 1)..end].Trim();

        return FromAddress.Trim();
    }
}
