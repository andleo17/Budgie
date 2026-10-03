using System.Text;
using System.Text.RegularExpressions;

namespace Budgie.Modules.EmailImport.Infrastructure.Parsing;

/// <summary>
/// Extracts the message body from raw email source. Accepts both a loose body (returned
/// as-is) and the full MIME source as exported by a mail client — the exact thing a user
/// pastes into the rule tester. Decodes <c>base64</c> and <c>quoted-printable</c> transfer
/// encodings (hand-rolled, no extra package) and walks <c>multipart/*</c> trees preferring
/// <c>text/plain</c> over <c>text/html</c>.
/// </summary>
internal static partial class RawEmailReader
{
    /// <summary>Guards against hostile nested multiparts; deep enough for real email, shallow enough to stay safe.</summary>
    private const int MaxMultipartDepth = 5;

    /// <summary>Header names that mark the start of raw MIME source (RFC 5322/2045).</summary>
    private static readonly string[] RawSourcePrefixes =
    [
        "From:", "Received:", "Return-Path:", "MIME-Version:", "Content-Type:", "Delivered-To:"
    ];

    private static readonly UTF8Encoding StrictUtf8 = new(
        encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    /// <summary>
    /// True when <paramref name="input"/> starts with a well-known MIME/RFC 5322 header,
    /// i.e. it is raw message source rather than a loose body.
    /// </summary>
    public static bool LooksLikeRawSource(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return false;

        var span = input.AsSpan().TrimStart(['\uFEFF', ' ', '\t', '\r', '\n']);
        var newline = span.IndexOfAny('\r', '\n');
        var firstLine = (newline >= 0 ? span[..newline] : span).TrimEnd();

        foreach (var prefix in RawSourcePrefixes)
        {
            if (firstLine.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    /// <summary>
    /// Returns the message body. Loose bodies come back untouched; raw MIME source is split
    /// into headers and body at the first blank line, the transfer encoding is decoded and
    /// multipart trees are walked. Returns <see cref="string.Empty"/> when nothing usable
    /// is found (e.g. multipart without a boundary) instead of throwing.
    /// </summary>
    public static string ExtractBody(string input)
    {
        if (string.IsNullOrEmpty(input))
            return string.Empty;
        if (!LooksLikeRawSource(input))
            return input; // loose body: use as-is

        // Headers and body are separated by the first blank line (\r\n\r\n, \n\n or mixed).
        var separator = HeaderBodySeparatorRegex().Match(input);
        var headerBlock = separator.Success ? input[..separator.Index] : input;
        var body = separator.Success ? input[(separator.Index + separator.Length)..] : string.Empty;

        var headers = ParseHeaders(headerBlock);
        return ExtractPartBody(body, headers, 0);
    }

    /// <summary>
    /// Unfolds header lines (continuations start with whitespace) into a
    /// case-insensitive name/value dictionary.
    /// </summary>
    private static Dictionary<string, string> ParseHeaders(string headerBlock)
    {
        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        string? currentName = null;

        foreach (var rawLine in headerBlock.Split('\n'))
        {
            var line = rawLine.TrimEnd('\r');
            if (line.Length == 0)
                continue;

            if (currentName is not null && (line[0] is ' ' or '\t'))
            {
                // Unfolded continuation of the previous header.
                headers[currentName] = headers[currentName] + " " + line.Trim();
                continue;
            }

            var colon = line.IndexOf(':');
            if (colon <= 0)
            {
                currentName = null; // malformed line: not part of any header
                continue;
            }

            currentName = line[..colon].Trim();
            headers[currentName] = line[(colon + 1)..].Trim();
        }

        return headers;
    }

    /// <summary>
    /// Decodes one body: multipart trees are walked recursively (depth-limited), everything
    /// else is decoded according to its <c>Content-Transfer-Encoding</c>.
    /// </summary>
    private static string ExtractPartBody(string body, Dictionary<string, string> headers, int depth)
    {
        if (depth > MaxMultipartDepth)
            return string.Empty; // hostile nesting: bail out instead of overflowing the stack

        var contentType = GetMediaType(headers);

        if (contentType.StartsWith("multipart/", StringComparison.OrdinalIgnoreCase))
        {
            var boundary = GetParameter(headers, "Content-Type", "boundary");
            if (string.IsNullOrEmpty(boundary))
                return string.Empty; // broken multipart: nothing recoverable
            return ExtractMultipartBody(body, boundary, depth + 1);
        }

        var encoding = headers.TryGetValue("Content-Transfer-Encoding", out var cte)
            ? cte.Trim().ToLowerInvariant()
            : string.Empty;

        return encoding switch
        {
            "base64" => DecodeBase64(body),
            "quoted-printable" => DecodeQuotedPrintable(body),
            _ => body // 7bit / 8bit / binary / absent: already text
        };
    }

    /// <summary>
    /// Splits a multipart body by its boundary lines, decodes each part and returns the
    /// best text: the first non-empty <c>text/plain</c> part, or the first non-empty
    /// <c>text/html</c> part when there is no plain one.
    /// </summary>
    private static string ExtractMultipartBody(string body, string boundary, int depth)
    {
        // Normalize line endings so boundary lines compare reliably. Quoted-printable
        // decoding below understands LF on its own.
        var normalized = body.Replace("\r\n", "\n").Replace('\r', '\n');
        var startToken = "--" + boundary;
        var endToken = startToken + "--";

        string? plain = null;
        string? html = null;
        StringBuilder? current = null;

        void FlushPart()
        {
            if (current is null)
                return;
            var raw = current.ToString();
            current = null;

            // A part whose body is EMPTY carries no content, and must not be mistaken for
            // one. Without this guard the trailing blank line is trimmed away below, the
            // header/body separator disappears with it, and CollectPart then falls back to
            // "no separator → the whole part is the body" — handing the user a block of MIME
            // headers as if it were the message text, and shadowing the real part behind it.
            // Real senders do emit an empty text/plain alternative next to a full text/html
            // one, so the next non-empty part has to win.
            var separator = HeaderBodySeparatorRegex().Match(raw);
            if (separator.Success && separator.Index + separator.Length >= raw.TrimEnd().Length)
                return;

            CollectPart(raw.TrimEnd(), depth, ref plain, ref html);
        }

        foreach (var line in normalized.Split('\n'))
        {
            var trimmed = line.TrimEnd();
            if (trimmed.Equals(endToken, StringComparison.Ordinal))
            {
                FlushPart();
                break; // "--boundary--": the epilogue is ignored
            }
            if (IsBoundaryLine(trimmed, startToken))
            {
                FlushPart();
                current = new StringBuilder();
                continue;
            }
            current?.AppendLine(line);
        }

        return plain ?? html ?? string.Empty;
    }

    /// <summary>True for a boundary delimiter line (<c>--boundary</c> plus optional whitespace).</summary>
    private static bool IsBoundaryLine(string trimmedLine, string startToken) =>
        trimmedLine.StartsWith(startToken, StringComparison.Ordinal) &&
        (trimmedLine.Length == startToken.Length || char.IsWhiteSpace(trimmedLine[startToken.Length]));

    /// <summary>Decodes one part and records it if it is the preferred plain or html text.</summary>
    private static void CollectPart(
        string partText, int depth, ref string? plain, ref string? html)
    {
        var separator = HeaderBodySeparatorRegex().Match(partText);
        var partHeaders = separator.Success
            ? ParseHeaders(partText[..separator.Index])
            : new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var partBody = separator.Success ? partText[(separator.Index + separator.Length)..] : partText;

        var mediaType = GetMediaType(partHeaders);

        if (mediaType.StartsWith("multipart/", StringComparison.OrdinalIgnoreCase))
        {
            // Nested container: the recursion already prefers text/plain inside the subtree.
            var nested = ExtractPartBody(partBody, partHeaders, depth);
            if (plain is null && nested.Length > 0)
                plain = nested;
            return;
        }

        var text = ExtractPartBody(partBody, partHeaders, depth);
        if (text.Length == 0)
            return;
        if (mediaType.StartsWith("text/plain", StringComparison.OrdinalIgnoreCase))
        {
            plain ??= text;
        }
        else if (mediaType.StartsWith("text/html", StringComparison.OrdinalIgnoreCase))
        {
            html ??= text;
        }
    }

    /// <summary>
    /// Media type without parameters, lowercased. Absent <c>Content-Type</c> defaults to
    /// <c>text/plain</c> per RFC 2046.
    /// </summary>
    private static string GetMediaType(Dictionary<string, string> headers)
    {
        if (!headers.TryGetValue("Content-Type", out var contentType) || string.IsNullOrWhiteSpace(contentType))
            return "text/plain";
        var semicolon = contentType.IndexOf(';');
        return (semicolon >= 0 ? contentType[..semicolon] : contentType).Trim().ToLowerInvariant();
    }

    /// <summary>Reads a parameter from a header value, e.g. <c>boundary</c> from Content-Type.</summary>
    private static string? GetParameter(Dictionary<string, string> headers, string headerName, string parameterName)
    {
        if (!headers.TryGetValue(headerName, out var headerValue))
            return null;
        foreach (Match match in ParameterRegex().Matches(headerValue))
        {
            if (match.Groups[1].Value.Equals(parameterName, StringComparison.OrdinalIgnoreCase))
                return match.Groups[2].Success ? match.Groups[2].Value : match.Groups[3].Value;
        }
        return null;
    }

    /// <summary>Decodes a base64 body; falls back to the raw text when the base64 is malformed.</summary>
    private static string DecodeBase64(string body)
    {
        var compact = WhitespaceRegex().Replace(body, string.Empty);
        if (compact.Length == 0)
            return string.Empty;
        try
        {
            return DecodeBytes(Convert.FromBase64String(compact));
        }
        catch (FormatException)
        {
            return body; // keep the raw text so the rule tester can still show something
        }
    }

    /// <summary>
    /// Decodes quoted-printable by hand: <c>=XX</c> becomes the byte, <c>=</c> at the end
    /// of a line is a soft break that joins the lines, and a <c>=</c> not followed by two
    /// hex digits stays literal.
    /// </summary>
    private static string DecodeQuotedPrintable(string body)
    {
        // Soft line breaks first: '=' (plus optional trailing blanks) before an EOL is removed.
        var joined = QuotedPrintableSoftBreakRegex().Replace(body, string.Empty);

        var bytes = new List<byte>(joined.Length);
        var literalStart = 0;
        var i = 0;
        while (i < joined.Length)
        {
            if (joined[i] != '=')
            {
                i++;
                continue;
            }

            // Flush the literal run before the escape.
            if (i > literalStart)
                bytes.AddRange(Encoding.UTF8.GetBytes(joined, literalStart, i - literalStart));

            if (i + 2 < joined.Length &&
                HexValue(joined[i + 1]) is int high and >= 0 &&
                HexValue(joined[i + 2]) is int low and >= 0)
            {
                bytes.Add((byte)((high << 4) | low));
                i += 3;
            }
            else
            {
                bytes.Add((byte)'='); // literal '=': the escape is not valid QP
                i += 1;
            }
            literalStart = i;
        }
        if (literalStart < joined.Length)
            bytes.AddRange(Encoding.UTF8.GetBytes(joined, literalStart, joined.Length - literalStart));

        return DecodeBytes(bytes.ToArray());
    }

    /// <summary>
    /// UTF-8 strict first — the norm for modern bank emails — with ISO-8859-1 as fallback
    /// for legacy charsets, which can never fail to decode.
    /// </summary>
    private static string DecodeBytes(byte[] bytes)
    {
        try
        {
            return StrictUtf8.GetString(bytes);
        }
        catch (DecoderFallbackException)
        {
            return Encoding.Latin1.GetString(bytes);
        }
    }

    private static int HexValue(char c) => c switch
    {
        >= '0' and <= '9' => c - '0',
        >= 'a' and <= 'f' => c - 'a' + 10,
        >= 'A' and <= 'F' => c - 'A' + 10,
        _ => -1
    };

    [GeneratedRegex(@"(?:\r?\n){2}", RegexOptions.CultureInvariant)]
    private static partial Regex HeaderBodySeparatorRegex();

    [GeneratedRegex(@"([a-zA-Z0-9-]+)\s*=\s*(?:""([^""]*)""|([^;\s]+))", RegexOptions.CultureInvariant)]
    private static partial Regex ParameterRegex();

    [GeneratedRegex(@"\s+", RegexOptions.CultureInvariant)]
    private static partial Regex WhitespaceRegex();

    [GeneratedRegex(@"=[ \t]*(?:\r\n|\r|\n)", RegexOptions.CultureInvariant)]
    private static partial Regex QuotedPrintableSoftBreakRegex();
}
