using System.Globalization;
using System.Text.RegularExpressions;

namespace Budgie.Modules.EmailImport.Infrastructure.Parsing;

/// <summary>
/// Converts HTML email bodies into structured plain text. Bank notification emails are
/// HTML tables, and a naive <c>Regex.Replace("&lt;[^&gt;]+&gt;", "")</c> collapses every
/// cell into one unreadable line, which makes the user-facing rule regexes impossible to
/// write. This converter keeps the structure: cell boundaries become tabs and block
/// boundaries become newlines.
/// </summary>
/// <remarks>
/// The order of operations is deliberate and must not change:
/// <list type="number">
/// <item><description>Remove <c>&lt;script&gt;</c>/<c>&lt;style&gt;</c> together with their content.</description></item>
/// <item><description><c>&lt;br&gt;</c> and block-end tags become newlines.</description></item>
/// <item><description>Cell-end tags become tabs.</description></item>
/// <item><description>Remove the remaining tags.</description></item>
/// <item>
/// <description>
/// Decode entities <b>after</b> tag removal. Decoding first would turn
/// <c>&amp;lt;script&amp;gt;</c> into a real <c>&lt;script&gt;</c> and reintroduce the
/// tag removed in step 1.
/// </description>
/// </item>
/// <item><description>Collapse whitespace runs and blank lines.</description></item>
/// <item><description>Final trim.</description></item>
/// </list>
/// Returns <see cref="string.Empty"/> for null or empty input; never throws.
/// </remarks>
public static partial class HtmlToTextConverter
{
    /// <summary>
    /// Converts an HTML body to plain text preserving table and block structure.
    /// </summary>
    /// <param name="html">HTML source (or any text; non-HTML passes through safely).</param>
    public static string Convert(string html)
    {
        if (string.IsNullOrEmpty(html))
            return string.Empty;

        var text = html;

        // 1. <script>/<style> die with their content — trackers and CSS must not leak into
        //    the text the user writes regexes against.
        text = ScriptOrStyleRegex().Replace(text, string.Empty);
        text = UnclosedScriptOrStyleRegex().Replace(text, string.Empty);

        // 2. Structure becomes whitespace BEFORE tags are stripped: the tag carries the
        //    structure, so replacing it first is what preserves layout.
        text = LineBreakTagRegex().Replace(text, "\n");
        text = BlockEndTagRegex().Replace(text, "\n");

        // 3. Cell boundaries become tabs so table rows stay aligned.
        text = CellEndTagRegex().Replace(text, "\t");

        // 4. Comments and every remaining tag go away.
        text = CommentRegex().Replace(text, string.Empty);
        text = AnyTagRegex().Replace(text, string.Empty);

        // 5. Entities decode AFTER tag removal (see remarks).
        text = EntityRegex().Replace(text, DecodeEntity);

        // 6. Collapse: horizontal runs become one space (tabs are structural and survive),
        //    whitespace around newlines is trimmed, and blank lines collapse to one.
        text = HorizontalSpaceRegex().Replace(text, " ");
        text = NewlineEdgeRegex().Replace(text, "\n");
        text = BlankLineRegex().Replace(text, "\n\n");

        // 7. Final trim.
        return text.Trim();
    }

    /// <summary>Decodes one entity; unknown named entities stay literal.</summary>
    private static string DecodeEntity(Match match)
    {
        var code = match.Groups["code"].Value;

        if (code.StartsWith('#'))
        {
            var isHex = code.Length > 1 && code[1] is 'x' or 'X';
            var digits = isHex ? code[2..] : code[1..];
            if (!int.TryParse(digits, isHex ? NumberStyles.HexNumber : NumberStyles.None,
                    CultureInfo.InvariantCulture, out var codePoint))
                return match.Value;
            // Reject invalid scalar values (surrogates, out of range): keep the entity literal.
            if (codePoint is < 0 or > 0x10FFFF || codePoint is >= 0xD800 and <= 0xDFFF)
                return match.Value;
            return char.ConvertFromUtf32(codePoint);
        }

        return code.ToLowerInvariant() switch
        {
            "nbsp" => "\u00A0",
            "amp" => "&",
            "lt" => "<",
            "gt" => ">",
            "quot" => "\"",
            "apos" => "'",
            "euro" => "€",
            "pound" => "£",
            "dollar" => "$",
            _ => match.Value // unknown named entity: keep as-is
        };
    }

    [GeneratedRegex("<(script|style)\\b[^>]*>[\\s\\S]*?</\\1\\s*>",
        RegexOptions.IgnoreCase | RegexOptions.Multiline | RegexOptions.CultureInvariant)]
    private static partial Regex ScriptOrStyleRegex();

    [GeneratedRegex("<(script|style)\\b[^>]*>[\\s\\S]*\\z",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex UnclosedScriptOrStyleRegex();

    [GeneratedRegex("<br\\s*/?>",
        RegexOptions.IgnoreCase | RegexOptions.Multiline | RegexOptions.CultureInvariant)]
    private static partial Regex LineBreakTagRegex();

    [GeneratedRegex("</(p|div|tr|li)\\s*>",
        RegexOptions.IgnoreCase | RegexOptions.Multiline | RegexOptions.CultureInvariant)]
    private static partial Regex BlockEndTagRegex();

    [GeneratedRegex("</(td|th)\\s*>",
        RegexOptions.IgnoreCase | RegexOptions.Multiline | RegexOptions.CultureInvariant)]
    private static partial Regex CellEndTagRegex();

    [GeneratedRegex("<!--[\\s\\S]*?-->", RegexOptions.Multiline | RegexOptions.CultureInvariant)]
    private static partial Regex CommentRegex();

    [GeneratedRegex("<[^>]*>", RegexOptions.Multiline | RegexOptions.CultureInvariant)]
    private static partial Regex AnyTagRegex();

    // 'x' is case-insensitive by spec (HTML5): "&#x20AC;" AND "&#X20AC;" are the same
    // entity. The lowercase-only alternative was a defect (debt plan §6.5): the matching
    // DecodeEntity already handled 'X', but the regex never let it see one.
    [GeneratedRegex("&(?<code>#[0-9]+|#[xX][0-9a-fA-F]+|[a-zA-Z]+);", RegexOptions.CultureInvariant)]
    private static partial Regex EntityRegex();

    [GeneratedRegex("[ \\u00A0\\u202F]+", RegexOptions.CultureInvariant)]
    private static partial Regex HorizontalSpaceRegex();

    [GeneratedRegex("[\\t ]*\\n[\\t ]*", RegexOptions.Multiline | RegexOptions.CultureInvariant)]
    private static partial Regex NewlineEdgeRegex();

    [GeneratedRegex("\\n{3,}", RegexOptions.Multiline | RegexOptions.CultureInvariant)]
    private static partial Regex BlankLineRegex();
}
