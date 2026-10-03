using Budgie.Modules.EmailImport.Infrastructure.Parsing;
using FluentAssertions;
using Xunit;

namespace Budgie.Modules.EmailImport.UnitTests.Parsing;

/// <summary>
/// <see cref="HtmlToTextConverter"/> is what makes user-writable regexes possible at all:
/// bank notifications are HTML tables, and a naive tag-strip collapses every cell into one
/// unreadable line. Cell boundaries must become tabs and block boundaries newlines.
/// </summary>
public class HtmlToTextConverterTests
{
    // ── Table structure: the whole reason this class exists ────────────────────────────
    [Fact]
    public void Convert_TwoByTwoTable_KeepsRowsAsLinesAndCellsAsTabs()
    {
        var html = "<table><tr><td>a</td><td>b</td></tr><tr><td>c</td><td>d</td></tr></table>";

        HtmlToTextConverter.Convert(html).Should().Be("a\tb\nc\td");
    }

    [Fact]
    public void Convert_SingleRowTable_EndsWithATabTrimmedAway()
    {
        // The trailing newline of a one-row table is removed by the final Trim: that is
        // correct behaviour, not a lost boundary.
        HtmlToTextConverter.Convert("<table><tr><td>a</td><td>b</td></tr></table>")
            .Should().Be("a\tb");
    }

    [Fact]
    public void Convert_TableWithHeaders_UsesTheSameCellSeparator()
    {
        var html = "<table><tr><th>Campo</th><th>Valor</th></tr>" +
                   "<tr><td>Importe</td><td>45,20 &euro;</td></tr></table>";

        HtmlToTextConverter.Convert(html).Should().Be("Campo\tValor\nImporte\t45,20 €");
    }

    [Fact]
    public void Convert_TableCellWithNestedMarkup_KeepsTheCellContent()
    {
        var html = "<table><tr><td><strong>Importe</strong></td><td><span>45,20 &euro;</span></td></tr></table>";

        HtmlToTextConverter.Convert(html).Should().Be("Importe\t45,20 €");
    }

    // ── <br> and block-end tags ────────────────────────────────────────────────────────
    [Fact]
    public void Convert_BreakTags_BecomeNewlines()
    {
        HtmlToTextConverter.Convert("uno<br>dos<br/>tres").Should().Be("uno\ndos\ntres");
    }

    [Fact]
    public void Convert_BlockEndTags_BecomeNewlines()
    {
        HtmlToTextConverter.Convert("<div>uno</div><div>dos</div><ul><li>a</li><li>b</li></ul>")
            .Should().Be("uno\ndos\na\nb");
    }

    [Fact]
    public void Convert_ConsecutiveParagraphs_AreSeparatedByOneNewline()
    {
        // Only the CLOSING tag becomes a newline, so </p><p> yields a single break — the
        // blank-line collapsing step then never sees the two-newline run. Users writing
        // multi-line rules rely on one break per block, not a blank line.
        HtmlToTextConverter.Convert("<p>uno</p><p>dos</p>").Should().Be("uno\ndos");
    }

    // ── <script>/<style> die WITH their content ─────────────────────────────────────────
    [Fact]
    public void Convert_ScriptTag_RemovesTagAndContent()
    {
        var html = "<p>antes</p><script>var tracking = 'pago'; alert(1);</script><p>despues</p>";

        var text = HtmlToTextConverter.Convert(html);

        text.Should().NotContain("tracking");
        text.Should().NotContain("alert");
        text.Should().Be("antes\ndespues");
    }

    [Fact]
    public void Convert_StyleTag_RemovesCssIncludingMarkupInsideIt()
    {
        var html = "<style>td > span { color: #ff0000; }</style><table><tr><td>x</td></tr></table>";

        var text = HtmlToTextConverter.Convert(html);

        text.Should().NotContain("color");
        text.Should().Be("x");
    }

    [Fact]
    public void Convert_UnclosedScriptTag_RemovesEverythingAfterIt()
    {
        // No closing tag: a truncated email must not leak the rest of the script as text.
        var text = HtmlToTextConverter.Convert("<p>antes</p><script>var leaked = 1;");

        text.Should().Be("antes");
        text.Should().NotContain("leaked");
    }

    [Fact]
    public void Convert_ScriptTagWithAttributes_IsStillRemoved()
    {
        var html = "<script type=\"text/javascript\" data-x=\"1\">var a = 2;</script><p>visible</p>";

        HtmlToTextConverter.Convert(html).Should().Be("visible");
    }

    // ── Entity decoding happens AFTER tag removal — the load-bearing invariant ──────────
    [Fact]
    public void Convert_EscapedScriptText_SurvivesAsLiteralText()
    {
        // If entities were decoded BEFORE tags were removed, "&lt;script&gt;" would become a
        // real <script> tag and its content would vanish from the output. That the literal
        // text SURVIVES here is precisely what proves the operation order is correct.
        var html = "<p>Mensaje &lt;script&gt;alert('x')&lt;/script&gt; recibido</p>";

        var text = HtmlToTextConverter.Convert(html);

        text.Should().Contain("<script>");
        text.Should().Contain("alert('x')");
        text.Should().Contain("</script>");
    }

    [Fact]
    public void Convert_EscapedAngleBrackets_DecodeToLiteralAngleBrackets()
    {
        HtmlToTextConverter.Convert("&lt;b&gt;negrita&lt;/b&gt;").Should().Be("<b>negrita</b>");
    }

    [Theory]
    [InlineData("&amp;", "&")]
    [InlineData("&lt;", "<")]
    [InlineData("&gt;", ">")]
    [InlineData("&quot;", "\"")]
    [InlineData("&apos;", "'")]
    [InlineData("&euro;", "€")]
    [InlineData("&pound;", "£")]
    [InlineData("&dollar;", "$")]
    public void Convert_NamedEntities_AreDecoded(string html, string expected)
    {
        HtmlToTextConverter.Convert(html).Should().Be(expected);
    }

    [Fact]
    public void Convert_NamedEntities_AreCaseInsensitive()
    {
        HtmlToTextConverter.Convert("&EURO;").Should().Be("€");
    }

    [Theory]
    [InlineData("&#8364;", "€")]   // decimal
    [InlineData("&#x20AC;", "€")]  // lowercase hex
    [InlineData("&#39;", "'")]
    public void Convert_NumericEntities_AreDecoded(string html, string expected)
    {
        HtmlToTextConverter.Convert(html).Should().Be(expected);
    }

    [Fact]
    public void Convert_UppercaseHexEntity_IsNotRecognisedAndStaysLiteral()
    {
        // The entity regex only accepts a LOWERCASE '#x'. HTML5 permits "&#X20AC;", so this
        // is a real (minor) gap rather than intended behaviour — pinned here so that fixing
        // it later is a deliberate, visible change rather than a silent one.
        HtmlToTextConverter.Convert("&#X20AC;").Should().Be("&#X20AC;");
    }

    [Fact]
    public void Convert_InvalidCodePoint_IsKeptLiteral()
    {
        // Surrogates and out-of-range values must stay as-is rather than throw or corrupt.
        HtmlToTextConverter.Convert("&#xD800;").Should().Be("&#xD800;");
        HtmlToTextConverter.Convert("&#1114112;").Should().Be("&#1114112;");
    }

    [Fact]
    public void Convert_UnknownNamedEntity_IsKeptLiteral()
    {
        HtmlToTextConverter.Convert("&banana;").Should().Be("&banana;");
    }

    // ── &nbsp; collapses: it must not survive as a structural character ─────────────────
    [Fact]
    public void Convert_NbspEntity_CollapsesIntoTheSurroundingWhitespace()
    {
        // Step 6 turns horizontal runs (space, NBSP, narrow NBSP) into a single space, so an
        // amount written "45,20&nbsp;&euro;" becomes "45,20 €" and the user's amount regex —
        // which cannot know about NBSP — keeps matching.
        HtmlToTextConverter.Convert("Importe: 45,20&nbsp;&euro;").Should().Be("Importe: 45,20 €");
    }

    [Fact]
    public void Convert_TwoNonBreakingSpaces_CollapseToOneOrdinarySpace()
    {
        HtmlToTextConverter.Convert("a&nbsp;&nbsp;b").Should().Be("a b");
    }

    [Fact]
    public void Convert_NarrowNoBreakSpace_CollapsesToo()
    {
        HtmlToTextConverter.Convert("1\u202F234,56").Should().Be("1 234,56");
    }

    // ── Whitespace and blank-line collapsing ────────────────────────────────────────────
    [Fact]
    public void Convert_ThreeOrMoreNewlines_CollapseToABlankLine()
    {
        HtmlToTextConverter.Convert("a\n\n\n\n\nb").Should().Be("a\n\nb");
    }

    [Fact]
    public void Convert_LeadingAndTrailingWhitespace_IsTrimmed()
    {
        HtmlToTextConverter.Convert("\n\n   texto   \n\n").Should().Be("texto");
    }

    [Fact]
    public void Convert_Comments_AreRemoved()
    {
        HtmlToTextConverter.Convert("visible<!-- oculto -->").Should().Be("visible");
    }

    // ── Edge cases: never throws ────────────────────────────────────────────────────────
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Convert_EmptyInput_ReturnsEmptyString(string? html)
    {
        var act = () => HtmlToTextConverter.Convert(html!);
        act.Should().NotThrow();
        act().Should().Be(string.Empty);
    }

    [Fact]
    public void Convert_PlainTextWithNoMarkup_PassesThrough()
    {
        HtmlToTextConverter.Convert("Importe: 45,20 EUR").Should().Be("Importe: 45,20 EUR");
    }

    [Fact]
    public void Convert_UnclosedTag_DoesNotThrow()
    {
        var act = () => HtmlToTextConverter.Convert("<td>celda</td");
        act.Should().NotThrow();
    }
}
