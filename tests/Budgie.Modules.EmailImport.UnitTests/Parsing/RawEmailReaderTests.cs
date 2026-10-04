using System.Text;
using Budgie.Modules.EmailImport.Infrastructure.Parsing;
using FluentAssertions;
using Xunit;

namespace Budgie.Modules.EmailImport.UnitTests.Parsing;

/// <summary>
/// <see cref="RawEmailReader"/> turns what a user pastes into the rule tester — a whole
/// exported message — into a body. Transfer encodings and multipart trees are decoded by
/// hand, and <c>text/plain</c> always wins over <c>text/html</c>.
/// </summary>
/// <remarks>
/// Every raw-source fixture starts with a header from <c>RawSourcePrefixes</c> (From,
/// Received, Return-Path, MIME-Version, Content-Type, Delivered-To). A snippet beginning
/// with <c>Content-Transfer-Encoding</c> is NOT recognised as raw source and comes back
/// verbatim — asserted explicitly below, because that is exactly what a user's pasted
/// fragment would do.
/// </remarks>
public class RawEmailReaderTests
{
    private static string B64(string plain) =>
        Convert.ToBase64String(Encoding.UTF8.GetBytes(plain));

    /// <summary>Wraps a body in a single-part raw message with the given transfer encoding.</summary>
    private static string Raw(string contentType, string encoding, string body) =>
        $"From: banco@ejemplo.es\r\nContent-Type: {contentType}\r\n" +
        $"Content-Transfer-Encoding: {encoding}\r\n\r\n{body}";

    // ── Detecting raw source ───────────────────────────────────────────────────────────
    [Theory]
    [InlineData("From: banco@ejemplo.es\n\ncuerpo")]
    [InlineData("Received: from x\n\ncuerpo")]
    [InlineData("Return-Path: <a@b.c>\n\ncuerpo")]
    [InlineData("MIME-Version: 1.0\n\ncuerpo")]
    [InlineData("Content-Type: text/plain\n\ncuerpo")]
    [InlineData("Delivered-To: a@b.c\n\ncuerpo")]
    public void LooksLikeRawSource_WellKnownFirstHeader_ReturnsTrue(string input)
    {
        RawEmailReader.LooksLikeRawSource(input).Should().BeTrue();
    }

    [Fact]
    public void LooksLikeRawSource_HeaderNameIsCaseInsensitive()
    {
        RawEmailReader.LooksLikeRawSource("from: banco@ejemplo.es\n\ncuerpo").Should().BeTrue();
    }

    [Fact]
    public void LooksLikeRawSource_LeadingBlankLinesAndBom_AreSkipped()
    {
        RawEmailReader.LooksLikeRawSource("\uFEFF\n  From: a@b.c\n\ncuerpo").Should().BeTrue();
    }

    [Theory]
    [InlineData("Importe: 45,20 EUR")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("<table><tr><td>a</td></tr></table>")]
    [InlineData("Content-Transfer-Encoding: base64\r\n\r\naGk=")]
    public void LooksLikeRawSource_LooseBodyOrUnknownFirstHeader_ReturnsFalse(string input)
    {
        RawEmailReader.LooksLikeRawSource(input).Should().BeFalse();
    }

    [Fact]
    public void ExtractBody_TransferEncodingAloneIsNotARawSourceMarker_ReturnsItVerbatim()
    {
        // Only the six RawSourcePrefixes count. A fragment the user pasted starting at the
        // Content-Transfer-Encoding header is therefore treated as a loose body and is NOT
        // decoded — documented behaviour, pinned so it can never change unnoticed.
        const string fragment = "Content-Transfer-Encoding: base64\r\n\r\naGk=";

        RawEmailReader.LooksLikeRawSource(fragment).Should().BeFalse();
        RawEmailReader.ExtractBody(fragment).Should().Be(fragment);
    }

    // ── A loose body is returned untouched ──────────────────────────────────────────────
    [Fact]
    public void ExtractBody_LooseBodyWithoutHeaders_ReturnsItVerbatim()
    {
        const string body = "Banco Ejemplo\nImporte: 45,20 EUR\nOperacion: Compra";

        RawEmailReader.ExtractBody(body).Should().Be(body);
    }

    [Fact]
    public void ExtractBody_HtmlLooseBody_IsNotTouched()
    {
        // RawEmailReader only decodes; the HTML→text step belongs to MessageTextExtractor.
        const string html = "<table><tr><td>a</td></tr></table>";

        RawEmailReader.ExtractBody(html).Should().Be(html);
    }

    // ── base64 ─────────────────────────────────────────────────────────────────────────
    [Fact]
    public void ExtractBody_Base64Part_IsDecoded()
    {
        var raw = Raw("text/plain; charset=utf-8", "base64", B64("Importe: 45,20 EUR"));

        RawEmailReader.ExtractBody(raw).Should().Be("Importe: 45,20 EUR");
    }

    [Fact]
    public void ExtractBody_Base64WithAccentsAndSymbols_IsDecodedAsUtf8()
    {
        const string plain = "Importe: 45,20 € — operación café";
        var raw = Raw("text/plain; charset=utf-8", "base64", B64(plain));

        RawEmailReader.ExtractBody(raw).Should().Be(plain);
    }

    [Fact]
    public void ExtractBody_Base64WithLineBreaks_IsDecoded()
    {
        // Real emails wrap base64 at 76 columns; the whitespace is stripped first.
        var wrapped = string.Join("\r\n",
            Convert.ToBase64String(Encoding.UTF8.GetBytes("Importe: 1234,56 EUR")).Chunk(20).Select(c => new string(c)));

        RawEmailReader.ExtractBody(Raw("text/plain", "base64", wrapped))
            .Should().Be("Importe: 1234,56 EUR");
    }

    [Fact]
    public void ExtractBody_MalformedBase64_KeepsTheRawText()
    {
        // Degrading to the raw text beats throwing: the rule tester must show something.
        var act = () => RawEmailReader.ExtractBody(Raw("text/plain", "base64", "no soy base64 !!!"));

        act.Should().NotThrow();
        act().Should().Be("no soy base64 !!!");
    }

    // ── quoted-printable ───────────────────────────────────────────────────────────────
    [Fact]
    public void ExtractBody_QuotedPrintable_DecodesHexEscapes()
    {
        RawEmailReader.ExtractBody(Raw("text/plain; charset=utf-8", "quoted-printable", "Importe: 45,20 =E2=82=AC"))
            .Should().Be("Importe: 45,20 €");
    }

    [Fact]
    public void ExtractBody_QuotedPrintableWithSoftBreak_JoinsTheLines()
    {
        // A trailing '=' is a soft break: the two halves are ONE line, not two.
        RawEmailReader.ExtractBody(Raw("text/plain", "quoted-printable", "Importe: 1234,=\r\n56 EUR"))
            .Should().Be("Importe: 1234,56 EUR");
    }

    [Fact]
    public void ExtractBody_QuotedPrintableWithLatin1Bytes_DoesNotThrow()
    {
        // =F3 is 'ó' in ISO-8859-1 and invalid UTF-8: the Latin-1 fallback must kick in.
        var act = () => RawEmailReader.ExtractBody(
            Raw("text/plain; charset=iso-8859-1", "quoted-printable", "Operaci=F3n"));

        act.Should().NotThrow();
    }

    [Fact]
    public void ExtractBody_QuotedPrintableWithLiteralEquals_KeepsItLiteral()
    {
        // '=' not followed by two hex digits is not an escape.
        RawEmailReader.ExtractBody(Raw("text/plain", "quoted-printable", "Comparaci=n: a = b"))
            .Should().Be("Comparaci=n: a = b");
    }

    // ── multipart: text/plain always beats text/html ───────────────────────────────────
    [Fact]
    public void ExtractBody_MultipartAlternative_PrefersTextPlainEvenWhenHtmlComesFirst()
    {
        // Document order must not decide: the HTML part is physically FIRST here.
        var raw =
            "From: banco@ejemplo.es\r\n" +
            "MIME-Version: 1.0\r\n" +
            "Content-Type: multipart/alternative; boundary=\"BOUND\"\r\n\r\n" +
            "--BOUND\r\n" +
            "Content-Type: text/html; charset=utf-8\r\n\r\n" +
            "<table><tr><td>Importe</td><td>45,20</td></tr></table>\r\n" +
            "--BOUND\r\n" +
            "Content-Type: text/plain; charset=utf-8\r\n\r\n" +
            "Importe: 45,20 EUR\r\n" +
            "--BOUND--\r\n";

        RawEmailReader.ExtractBody(raw).Should().Be("Importe: 45,20 EUR");
    }

    [Fact]
    public void ExtractBody_MultipartWithOnlyHtml_FallsBackToTheHtmlPart()
    {
        var raw =
            "From: banco@ejemplo.es\r\n" +
            "Content-Type: multipart/alternative; boundary=\"BOUND\"\r\n\r\n" +
            "--BOUND\r\n" +
            "Content-Type: text/html; charset=utf-8\r\n\r\n" +
            "<p>Importe: 45,20 &euro;</p>\r\n" +
            "--BOUND--\r\n";

        RawEmailReader.ExtractBody(raw).Should().Be("<p>Importe: 45,20 &euro;</p>");
    }

    [Fact]
    public void ExtractBody_MultipartWithBase64PlainPart_DecodesTheChosenPart()
    {
        var raw =
            "From: banco@ejemplo.es\r\n" +
            "Content-Type: multipart/alternative; boundary=\"B\"\r\n\r\n" +
            "--B\r\n" +
            "Content-Type: text/html\r\n\r\n" +
            "<p>html</p>\r\n" +
            "--B\r\n" +
            "Content-Type: text/plain; charset=utf-8\r\n" +
            "Content-Transfer-Encoding: base64\r\n\r\n" +
            B64("Importe: 45,20 EUR") + "\r\n" +
            "--B--\r\n";

        RawEmailReader.ExtractBody(raw).Should().Be("Importe: 45,20 EUR");
    }

    [Fact]
    public void ExtractBody_MultipartWithEmptyPlainPart_FallsBackToTheNextPlainPart()
    {
        var raw =
            "From: banco@ejemplo.es\r\n" +
            "Content-Type: multipart/alternative; boundary=\"B\"\r\n\r\n" +
            "--B\r\n" +
            "Content-Type: text/plain; charset=utf-8\r\n\r\n" +
            "\r\n" +
            "--B\r\n" +
            "Content-Type: text/plain; charset=utf-8\r\n\r\n" +
            "Importe: 45,20 EUR\r\n" +
            "--B--\r\n";

        RawEmailReader.ExtractBody(raw).Should().Be("Importe: 45,20 EUR");
    }

    [Fact]
    public void ExtractBody_NestedMultipart_PrefersThePlainPartInsideTheContainer()
    {
        var raw =
            "From: banco@ejemplo.es\r\n" +
            "Content-Type: multipart/mixed; boundary=\"OUTER\"\r\n\r\n" +
            "--OUTER\r\n" +
            "Content-Type: multipart/alternative; boundary=\"INNER\"\r\n\r\n" +
            "--INNER\r\n" +
            "Content-Type: text/html\r\n\r\n" +
            "<p>html</p>\r\n" +
            "--INNER\r\n" +
            "Content-Type: text/plain\r\n\r\n" +
            "plano\r\n" +
            "--INNER--\r\n" +
            "--OUTER\r\n" +
            "Content-Type: text/plain\r\n\r\n" +
            "adjunto exterior\r\n" +
            "--OUTER--\r\n";

        RawEmailReader.ExtractBody(raw).Should().Be("plano");
    }

    [Fact]
    public void ExtractBody_TextPlainAttachment_IsSkipped_AndTheHtmlBodyWins()
    {
        // The previous version of this test PINNED the defect (§6.4/§9.3): a text/plain
        // part with Content-Disposition: attachment was chosen as the body because the
        // reader did not inspect Content-Disposition. Attachments are skipped now, so the
        // real html body of the message wins — changed on purpose, not by side effect.
        var raw =
            "From: banco@ejemplo.es\r\n" +
            "Content-Type: multipart/mixed; boundary=\"B\"\r\n\r\n" +
            "--B\r\n" +
            "Content-Type: text/plain; name=\"recibo.txt\"\r\n" +
            "Content-Disposition: attachment; filename=\"recibo.txt\"\r\n\r\n" +
            "contenido del recibo\r\n" +
            "--B\r\n" +
            "Content-Type: text/html\r\n\r\n" +
            "<p>cuerpo del correo</p>\r\n" +
            "--B--\r\n";

        RawEmailReader.ExtractBody(raw).Should().Be("<p>cuerpo del correo</p>");
    }

    [Fact]
    public void ExtractBody_TextPlainAttachmentBeforeTheRealPlainPart_ReturnsTheRealBody()
    {
        // Document order must not decide: the attachment is physically FIRST, the real
        // text/plain body comes second and must win (§6.4).
        var raw =
            "From: banco@ejemplo.es\r\n" +
            "Content-Type: multipart/mixed; boundary=\"B\"\r\n\r\n" +
            "--B\r\n" +
            "Content-Type: text/plain; name=\"notas.txt\"\r\n" +
            "Content-Disposition: attachment; filename=\"notas.txt\"\r\n\r\n" +
            "notas internas\r\n" +
            "--B\r\n" +
            "Content-Type: text/plain; charset=utf-8\r\n\r\n" +
            "Importe: 45,20 EUR\r\n" +
            "--B--\r\n";

        RawEmailReader.ExtractBody(raw).Should().Be("Importe: 45,20 EUR");
    }

    [Fact]
    public void ExtractBody_InlinePartWithFilename_IsAlsoSkipped()
    {
        // HTML senders attach assets inline (logos and the like): inline + filename is the
        // attachment case too, not just disposition "attachment" (§6.4).
        var raw =
            "From: banco@ejemplo.es\r\n" +
            "Content-Type: multipart/related; boundary=\"B\"\r\n\r\n" +
            "--B\r\n" +
            "Content-Type: text/plain; name=\"logo.txt\"\r\n" +
            "Content-Disposition: inline; filename=\"logo.txt\"\r\n\r\n" +
            "datos del logo\r\n" +
            "--B\r\n" +
            "Content-Type: text/html\r\n\r\n" +
            "<p>cuerpo</p>\r\n" +
            "--B--\r\n";

        RawEmailReader.ExtractBody(raw).Should().Be("<p>cuerpo</p>");
    }

    [Fact]
    public void ExtractBody_OnlyTextPlainAttachments_FallsBackToTheHtmlPart()
    {
        // Nothing but attachments: the html body is the only real body left.
        var raw =
            "From: banco@ejemplo.es\r\n" +
            "Content-Type: multipart/mixed; boundary=\"B\"\r\n\r\n" +
            "--B\r\n" +
            "Content-Type: text/plain; name=\"adjunto1.txt\"\r\n" +
            "Content-Disposition: attachment; filename=\"adjunto1.txt\"\r\n\r\n" +
            "uno\r\n" +
            "--B\r\n" +
            "Content-Type: text/plain; name=\"adjunto2.txt\"\r\n" +
            "Content-Disposition: attachment; filename=\"adjunto2.txt\"\r\n\r\n" +
            "dos\r\n" +
            "--B\r\n" +
            "Content-Type: text/html\r\n\r\n" +
            "<p>único cuerpo</p>\r\n" +
            "--B--\r\n";

        RawEmailReader.ExtractBody(raw).Should().Be("<p>único cuerpo</p>");
    }

    [Fact]
    public void ExtractBody_OnlyTextPlainAttachments_NoHtmlAtAll_ReturnsEmptyWithoutBreaking()
    {
        // Attachments and no body whatsoever: empty is the honest answer, and the reader
        // must not throw or hand the user an attachment as if it were the message (§6.4).
        var raw =
            "From: banco@ejemplo.es\r\n" +
            "Content-Type: multipart/mixed; boundary=\"B\"\r\n\r\n" +
            "--B\r\n" +
            "Content-Type: text/plain; name=\"adjunto.txt\"\r\n" +
            "Content-Disposition: attachment; filename=\"adjunto.txt\"\r\n\r\n" +
            "contenido del adjunto\r\n" +
            "--B--\r\n";

        var act = () => RawEmailReader.ExtractBody(raw);

        act.Should().NotThrow();
        act().Should().Be(string.Empty);
    }

    // ── Header handling ────────────────────────────────────────────────────────────────
    [Fact]
    public void ExtractBody_FoldedHeader_IsUnfoldedWithASingleSpace()
    {
        var raw = "From: banco@ejemplo.es\r\nContent-Type: text/plain;\r\n\tcharset=utf-8\r\n\r\n" +
                  "Importe: 45,20 EUR";

        RawEmailReader.ExtractBody(raw).Should().Be("Importe: 45,20 EUR");
    }

    [Fact]
    public void ExtractBody_LfOnlyLineEndings_AreHandled()
    {
        // A pasted message from a Unix editor has no CR at all.
        RawEmailReader.ExtractBody("From: banco@ejemplo.es\nContent-Type: text/plain\n\nImporte: 45,20 EUR")
            .Should().Be("Importe: 45,20 EUR");
    }

    [Fact]
    public void ExtractBody_AbsentContentType_DefaultsToTextPlainPerRfc2046()
    {
        RawEmailReader.ExtractBody("From: banco@ejemplo.es\r\nSubject: hola\r\n\r\nImporte: 45,20 EUR")
            .Should().Be("Importe: 45,20 EUR");
    }

    [Fact]
    public void ExtractBody_MultipartWithoutBoundary_ReturnsEmptyInsteadOfThrowing()
    {
        var raw = "From: banco@ejemplo.es\r\nContent-Type: multipart/alternative\r\n\r\ncuerpo huerfano";

        var act = () => RawEmailReader.ExtractBody(raw);
        act.Should().NotThrow();
        act().Should().Be(string.Empty);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void ExtractBody_EmptyInput_ReturnsEmptyString(string? input)
    {
        var act = () => RawEmailReader.ExtractBody(input!);
        act.Should().NotThrow();
        act().Should().Be(string.Empty);
    }

    [Fact]
    public void ExtractBody_HeadersWithNoBlankLineSeparator_ReturnsEmptyString()
    {
        // Nothing follows the headers, so there is no body at all.
        RawEmailReader.ExtractBody("From: banco@ejemplo.es\r\nSubject: hola").Should().BeEmpty();
    }

    [Fact]
    public void ExtractBody_SevenBitTransferEncoding_BodyIsAlreadyText()
    {
        var raw = Raw("text/plain; charset=utf-8", "7bit", "Importe: 45,20 EUR");

        RawEmailReader.ExtractBody(raw).Should().Be("Importe: 45,20 EUR");
    }
}
