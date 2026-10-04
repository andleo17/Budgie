namespace Budgie.Shared.Domain;

/// <summary>
/// <b>THE</b> parser for the RFC 5322 <c>From</c> header. Every call site that needs the
/// bare sender address — rule matching, provider mappers, domain snapshots — must call
/// <see cref="ExtractAddress"/> instead of writing its own angle-bracket logic. This class
/// exists because that logic was copy-pasted in three places (EmailMessage.GetFromAddress,
/// GmailMessageMapper.NormalizeFrom, EmailImportRule's From filter) and the three copies
/// drifted apart exactly because there was no single home for the semantics.
/// </summary>
/// <remarks>
/// The semantics are fixed here on purpose (debt plan §6.2) — do NOT add a fourth parser:
/// <list type="bullet">
/// <item><description><c>null</c>, empty or blank → <c>null</c> (blank means "no sender", never "").</description></item>
/// <item><description><c>Nombre &lt;a@b.c&gt;</c> → <c>a@b.c</c> (the first angle pair wins; the inner value is trimmed).</description></item>
/// <item><description>no angle brackets — or an unclosed pair — → the value trimmed as-is.</description></item>
/// </list>
/// </remarks>
public static class EmailHeader
{
    /// <summary>
    /// Extracts the bare address from a raw <c>From</c> header value. See the class remarks
    /// for the exact semantics; this method is the single place that implements them.
    /// </summary>
    public static string? ExtractAddress(string? rawHeader)
    {
        if (string.IsNullOrWhiteSpace(rawHeader))
            return null;

        var start = rawHeader.IndexOf('<');
        var end = rawHeader.IndexOf('>');
        if (start >= 0 && end > start)
            return rawHeader[(start + 1)..end].Trim();

        return rawHeader.Trim();
    }
}
