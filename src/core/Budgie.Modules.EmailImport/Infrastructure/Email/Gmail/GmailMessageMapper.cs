using System.Globalization;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Budgie.Modules.EmailImport.Domain.Parsing;

namespace Budgie.Modules.EmailImport.Infrastructure.Email.Gmail;

/// <summary>
/// Maps a <c>users.messages.get?format=full</c> response to the provider-neutral
/// <see cref="EmailMessage"/>. The body is stored <b>raw</b> (plain text or HTML
/// source): converting HTML to text is <c>IMessageTextExtractor</c>'s job (slice 4),
/// not the provider's. The sender header is normalized to the bare address at this
/// edge so rule matching works on addresses, not on display names.
/// </summary>
internal static partial class GmailMessageMapper
{
    /// <summary>Maps a full message resource to the neutral record.</summary>
    public static EmailMessage Map(GmailMessageResponse message)
    {
        var headers = message.Payload?.Headers ?? [];

        return EmailMessage.Create(
            message.Id,
            message.ThreadId,
            NormalizeFrom(FindHeader(headers, "From")),
            FindHeader(headers, "Subject")?.Trim(),
            ExtractBody(message.Payload),
            ParseReceivedAt(FindHeader(headers, "Date")));
    }

    private static string? FindHeader(GmailHeader[] headers, string name) =>
        headers.FirstOrDefault(h =>
            string.Equals(h.Name, name, StringComparison.OrdinalIgnoreCase))?.Value;

    /// <summary>
    /// Normalizes a raw From header (<c>"Banco X" &lt;correo@dominio&gt;</c>) to the bare
    /// address. Values without angle brackets are trimmed and returned as-is.
    /// </summary>
    private static string? NormalizeFrom(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return null;

        var start = raw.IndexOf('<');
        var end = raw.IndexOf('>');
        return start >= 0 && end > start
            ? raw[(start + 1)..end].Trim()
            : raw.Trim();
    }

    /// <summary>
    /// Walks the (possibly multipart) payload recursively, collecting the first
    /// <c>text/plain</c> part and the first <c>text/html</c> part. Plain text wins over
    /// HTML regardless of document order. Body data is base64 <b>URL-safe</b>
    /// (<c>-</c>/<c>_</c>, no padding). A malformed part degrades to an empty body for
    /// that message instead of failing the whole batch.
    /// </summary>
    private static string ExtractBody(GmailMessagePart? part)
    {
        if (part is null)
            return string.Empty;

        string? plain = null;
        string? html = null;
        Walk(part);
        return plain ?? html ?? string.Empty;

        void Walk(GmailMessagePart current)
        {
            if (!string.IsNullOrEmpty(current.Body?.Data))
            {
                string data;
                try
                {
                    data = GmailBase64Url.DecodeToString(current.Body.Data);
                }
                catch (FormatException)
                {
                    data = string.Empty;
                }

                var mime = current.MimeType ?? string.Empty;
                if (plain is null && mime.StartsWith("text/plain", StringComparison.OrdinalIgnoreCase))
                    plain = data;
                else if (html is null && mime.StartsWith("text/html", StringComparison.OrdinalIgnoreCase))
                    html = data;
            }

            if (current.Parts is not null)
            {
                foreach (var child in current.Parts)
                    Walk(child);
            }
        }
    }

    /// <summary>
    /// Parses the RFC 2822 <c>Date</c> header (e.g. <c>Thu, 2 Oct 2026 10:15:30 +0200</c>)
    /// to a <see cref="DateTimeOffset"/>, preserving the stated offset. RFC 2822 allows a
    /// single-digit day, a compact ±HHMM offset and an optional parenthesized zone
    /// comment — none of which <see cref="DateTimeOffset"/> parses reliably — so all
    /// three are normalized before parsing. If the weekday disagrees with the numeric
    /// date (common after forwards mangle headers), .NET rejects the value outright; per
    /// RFC 5322 §3.3 the day-of-week MUST be ignored when inconsistent, so the prefix is
    /// stripped and the parse retried on the authoritative numeric fields. Only if
    /// everything fails is the fallback the current instant (the parser treats
    /// <see cref="EmailMessage.ReceivedAt"/> as a low-confidence date source anyway).
    /// </summary>
    private static DateTimeOffset ParseReceivedAt(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return DateTimeOffset.UtcNow;

        var value = ZoneCommentRegex().Replace(raw.Trim(), string.Empty);
        value = SingleDigitDayRegex().Replace(value, "${1}0$2");
        value = NumericOffsetRegex().Replace(value, "$1:$2");

        if (DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces, out var parsed))
            return parsed;

        var withoutWeekday = WeekdayPrefixRegex().Replace(value, string.Empty);
        if (withoutWeekday != value &&
            DateTimeOffset.TryParse(withoutWeekday, CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces, out parsed))
            return parsed;

        return DateTimeOffset.UtcNow;
    }

    // " +0200 (CEST)" → drop the optional trailing zone comment.
    [GeneratedRegex(@"\s+\([^)]*\)\s*$")]
    private static partial Regex ZoneCommentRegex();

    // "Thu, 2 Oct" → "Thu, 02 Oct" (RFC 2822 allows 1*2DIGIT for the day of month,
    // with or without a preceding day-of-week).
    [GeneratedRegex(@"(\A|\s)(\d)(?=\s+[A-Za-z]{3}\s+\d{4})")]
    private static partial Regex SingleDigitDayRegex();

    // "Thu, 02 Oct ..." → "02 Oct ..." (day-of-week prefix; ignored per RFC 5322 when
    // inconsistent with the numeric date).
    [GeneratedRegex(@"^\s*[A-Za-z]{3,9},\s*")]
    private static partial Regex WeekdayPrefixRegex();

    // "+0200" → "+02:00" (RFC 2822 allows the compact form; DateTimeOffset prefers the colon).
    [GeneratedRegex(@"([+-]\d{2})(\d{2})\s*$")]
    private static partial Regex NumericOffsetRegex();
}

/// <summary><c>users.messages.get</c> message resource (only the fields Budgie reads).</summary>
internal sealed record GmailMessageResponse(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("threadId")] string? ThreadId,
    [property: JsonPropertyName("payload")] GmailMessagePart? Payload);

/// <summary>A MIME part of a message payload; <c>parts</c> nests for multipart bodies.</summary>
internal sealed record GmailMessagePart(
    [property: JsonPropertyName("mimeType")] string? MimeType,
    [property: JsonPropertyName("headers")] GmailHeader[]? Headers,
    [property: JsonPropertyName("body")] GmailBody? Body,
    [property: JsonPropertyName("parts")] GmailMessagePart[]? Parts);

/// <summary>A payload header (<c>From</c>, <c>Subject</c>, <c>Date</c>, ...).</summary>
internal sealed record GmailHeader(
    [property: JsonPropertyName("name")] string? Name,
    [property: JsonPropertyName("value")] string? Value);

/// <summary>The body of a part; <c>data</c> holds base64url-encoded content.</summary>
internal sealed record GmailBody(
    [property: JsonPropertyName("data")] string? Data);
