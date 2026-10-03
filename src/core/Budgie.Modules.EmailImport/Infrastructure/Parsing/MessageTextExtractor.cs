using System.Text.RegularExpressions;
using Budgie.Modules.EmailImport.Application.Abstractions;
using Budgie.Modules.EmailImport.Domain.Parsing;

namespace Budgie.Modules.EmailImport.Infrastructure.Parsing;

/// <summary>
/// Default <see cref="IMessageTextExtractor"/>. Orchestrates the two parsing primitives:
/// <see cref="RawEmailReader"/> decodes raw MIME source (transfer encodings, multipart
/// trees) and <see cref="HtmlToTextConverter"/> turns HTML into structured text — but only
/// when the result actually looks like HTML. Idempotent: text that is not HTML comes back
/// untouched.
/// </summary>
internal sealed partial class MessageTextExtractor : IMessageTextExtractor
{
    /// <inheritdoc />
    public string ExtractPlainText(EmailMessage message)
    {
        var body = message.Body ?? string.Empty;
        var text = RawEmailReader.ExtractBody(body);
        return LooksLikeHtml(text) ? HtmlToTextConverter.Convert(text) : text;
    }

    /// <summary>
    /// True when the text contains a '<' followed by a letter or '/', i.e. an HTML tag.
    /// Plain text with stray '<' characters (comparisons, "a &lt; b") does not match.
    /// </summary>
    private static bool LooksLikeHtml(string text) => HtmlTagRegex().IsMatch(text);

    [GeneratedRegex("<[/a-zA-Z]", RegexOptions.CultureInvariant)]
    private static partial Regex HtmlTagRegex();
}
