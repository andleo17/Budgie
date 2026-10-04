using Budgie.Shared.Domain;
using FluentAssertions;

namespace Budgie.Shared.UnitTests.Domain;

/// <summary>
/// <see cref="EmailHeader.ExtractAddress"/> is THE From-header parser (debt plan §6.2):
/// EmailMessage.GetFromAddress, GmailMessageMapper and EmailImportRule's From filter all
/// delegate to it. These tests pin its ONE semantics — the blank-input rule unified to
/// <c>null</c> — so a future "quick" copy elsewhere would have to break them on purpose.
/// </summary>
public sealed class EmailHeaderTests
{
    // ── Blank input unifies to null (the decision that resolved the three drifted copies) ─
    [Fact]
    public void ExtractAddress_Null_ReturnsNull()
    {
        EmailHeader.ExtractAddress(null).Should().BeNull();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t")]
    [InlineData("\r\n \t")]
    public void ExtractAddress_Blank_ReturnsNull(string? raw)
    {
        // Blank means "no sender". EmailMessage.GetFromAddress used to return the blank
        // string itself; that old behaviour was the outlier and was dropped on purpose.
        EmailHeader.ExtractAddress(raw).Should().BeNull();
    }

    // ── The documented shapes ──────────────────────────────────────────────────────────
    [Fact]
    public void ExtractAddress_BareAddress_IsTrimmedAndReturned()
    {
        EmailHeader.ExtractAddress("  a@b.c  ").Should().Be("a@b.c");
    }

    [Theory]
    [InlineData("Nombre <a@b.c>", "a@b.c")]
    [InlineData("\"Nombre, raro\" <a@b.c>", "a@b.c")]
    [InlineData("  Nombre  < a@b.c >  ", "a@b.c")]
    [InlineData("Nombre <a@b.c (comentario)>", "a@b.c (comentario)")]
    public void ExtractAddress_AngleBrackets_ReturnTheInnerValueTrimmed(string raw, string expected)
    {
        EmailHeader.ExtractAddress(raw).Should().Be(expected);
    }

    [Fact]
    public void ExtractAddress_TrailingCommentWithoutAngles_KeepsTheWholeTrimmedValue()
    {
        // No angle brackets → the value trimmed as-is, comment included.
        EmailHeader.ExtractAddress("a@b.c (comentario)").Should().Be("a@b.c (comentario)");
    }

    // ── Malformed headers degrade to the trimmed value, never throw ────────────────────
    [Theory]
    [InlineData("Nombre <a@b.c")]      // open bracket, never closed
    [InlineData("Nombre a@b.c>")]      // close bracket only
    [InlineData("<")]                  // lone bracket
    [InlineData(">")]
    public void ExtractAddress_UnclosedOrLoneAngleBrackets_FallBackToTheTrimmedValue(string raw)
    {
        var act = () => EmailHeader.ExtractAddress(raw);

        act.Should().NotThrow();
        act().Should().Be(raw.Trim());
    }

    [Fact]
    public void ExtractAddress_TwoOpeningBrackets_UsesTheFirstAnglePair()
    {
        // The FIRST '<' is the delimiter and the first '>' closes the address region, so
        // everything between them (a second '<' included) comes back trimmed.
        EmailHeader.ExtractAddress("a <b <c@x.y>").Should().Be("b <c@x.y");
    }
}
