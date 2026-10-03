using System.Text;
using Budgie.Modules.EmailImport.Domain.Parsing;
using Budgie.Modules.EmailImport.Infrastructure.Email.Gmail;
using FluentAssertions;
using Xunit;

namespace Budgie.Modules.EmailImport.UnitTests.Parsing;

/// <summary>
/// <see cref="GmailMessageMapper"/> is the provider edge: it normalises the sender to a bare
/// address, decodes the base64url body and — the case that lives HERE and not in
/// <c>RawEmailReader</c> — parses the RFC 2822 <c>Date</c> header.
/// </summary>
public class GmailMessageMapperTests
{
    private static string B64(string plain) =>
        Convert.ToBase64String(Encoding.UTF8.GetBytes(plain))
            .TrimEnd('=').Replace('+', '-').Replace('/', '_');

    /// <summary>Message whose payload carries ONLY the given headers and no body parts.</summary>
    private static GmailMessageResponse Message(params GmailHeader[] headers) =>
        new(
            "msg-1",
            "thread-1",
            new GmailMessagePart("text/plain", headers, new GmailBody(B64("cuerpo")), null));

    private static GmailMessageResponse WithBody(GmailMessagePart payload) =>
        new("msg-1", "thread-1", payload);

    private static DateTimeOffset ReceivedAtOf(string? date) =>
        GmailMessageMapper.Map(Message(new GmailHeader("Date", date!))).ReceivedAt;

    // ── The RFC 2822 Date header ───────────────────────────────────────────────────────
    [Fact]
    public void Map_ConsistentWeekday_ParsesTheStatedOffset()
    {
        ReceivedAtOf("Thu, 2 Oct 2026 10:15:30 +0200")
            .Should().Be(new DateTimeOffset(2026, 10, 2, 10, 15, 30, TimeSpan.FromHours(2)));
    }

    [Fact]
    public void Map_InconsistentWeekday_StillParsesFromTheNumericFields()
    {
        // "Wed" is wrong for 30 September 2026 (a Thursday). .NET rejects the whole value,
        // so the mapper strips the weekday and retries on the authoritative numeric fields.
        // RFC 5322 §3.3 REQUIRES the weekday to be ignored when it disagrees with the date:
        // forwards and mailing lists routinely mangle it.
        var mapped = GmailMessageMapper.Map(Message(new GmailHeader("Date", "Wed, 30 Sep 2026 08:00:00 +0000")));

        mapped.ReceivedAt.Should().Be(new DateTimeOffset(2026, 9, 30, 8, 0, 0, TimeSpan.Zero));
    }

    [Fact]
    public void Map_InconsistentWeekday_DoesNotDegradeToUtcNow()
    {
        // The only fallback is DateTimeOffset.UtcNow, so "parsed" and "degraded" are visibly
        // different here: 2026-09-30 is nowhere near the current instant.
        var mapped = GmailMessageMapper.Map(Message(new GmailHeader("Date", "Wed, 30 Sep 2026 08:00:00 +0000")));

        mapped.ReceivedAt.Year.Should().Be(2026);
        mapped.ReceivedAt.Month.Should().Be(9);
        mapped.ReceivedAt.Day.Should().Be(30);
    }

    [Fact]
    public void Map_SingleDigitDay_IsZeroPaddedBeforeParsing()
    {
        // RFC 2822 allows 1*2DIGIT for the day of month; DateTimeOffset does not.
        ReceivedAtOf("Thu, 2 Oct 2026 10:15:30 +0200").Day.Should().Be(2);
    }

    [Fact]
    public void Map_CompactOffsetAndZoneComment_AreNormalized()
    {
        ReceivedAtOf("Thu, 2 Oct 2026 10:15:30 +0200 (CEST)").Offset
            .Should().Be(TimeSpan.FromHours(2));
    }

    [Fact]
    public void Map_NegativeOffset_IsPreserved()
    {
        ReceivedAtOf("Wed, 30 Sep 2026 08:00:00 -0500").Offset
            .Should().Be(TimeSpan.FromHours(-5));
    }

    [Fact]
    public void Map_NoWeekdayPrefix_ParsesDirectly()
    {
        ReceivedAtOf("2 Oct 2026 10:15:30 +0200").Day.Should().Be(2);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("no es una fecha")]
    public void Map_MissingBlankOrUnparseableDate_FallsBackToUtcNow(string? date)
    {
        ReceivedAtOf(date).Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromMinutes(1));
    }

    // ── From normalization ─────────────────────────────────────────────────────────────
    [Fact]
    public void Map_FromWithDisplayName_IsReducedToTheBareAddress()
    {
        GmailMessageMapper.Map(Message(new GmailHeader("From", "Banco Ejemplo <notificaciones@banco.es>")))
            .FromAddress.Should().Be("notificaciones@banco.es");
    }

    [Fact]
    public void Map_FromWithQuotedDisplayName_IsReducedToTheBareAddress()
    {
        GmailMessageMapper.Map(Message(new GmailHeader("From", "\"Banco Ejemplo, S.A.\" <aviso@banco.es>")))
            .FromAddress.Should().Be("aviso@banco.es");
    }

    [Fact]
    public void Map_FromWithoutAngleBrackets_IsTrimmedAndKept()
    {
        GmailMessageMapper.Map(Message(new GmailHeader("From", "  aviso@banco.es  ")))
            .FromAddress.Should().Be("aviso@banco.es");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Map_MissingOrBlankFrom_BecomesNull(string? from)
    {
        GmailMessageMapper.Map(Message(new GmailHeader("From", from!))).FromAddress.Should().BeNull();
    }

    [Fact]
    public void Map_Subject_IsTrimmed()
    {
        GmailMessageMapper.Map(Message(new GmailHeader("Subject", "  Su pago  "))).Subject
            .Should().Be("Su pago");
    }

    [Fact]
    public void Map_MissingSubject_IsNull()
    {
        GmailMessageMapper.Map(Message()).Subject.Should().BeNull();
    }

    [Fact]
    public void Map_HeaderLookupIsCaseInsensitive()
    {
        var mapped = GmailMessageMapper.Map(Message(
            new GmailHeader("SUBJECT", "Aviso"),
            new GmailHeader("date", "Thu, 2 Oct 2026 10:15:30 +0200")));

        mapped.Subject.Should().Be("Aviso");
        mapped.ReceivedAt.Day.Should().Be(2);
    }

    // ── Body extraction: base64url, plain wins over html ────────────────────────────────
    [Fact]
    public void Map_Base64UrlBody_IsDecoded()
    {
        var mapped = GmailMessageMapper.Map(WithBody(
            new GmailMessagePart("text/plain", null, new GmailBody(B64("Importe: 45,20 €")), null)));

        mapped.Body.Should().Be("Importe: 45,20 €");
    }

    [Fact]
    public void Map_Multipart_PrefersTextPlainRegardlessOfDocumentOrder()
    {
        var payload = new GmailMessagePart("multipart/alternative", null, null,
        [
            new GmailMessagePart("text/html", null, new GmailBody(B64("<p>html</p>")), null),
            new GmailMessagePart("text/plain", null, new GmailBody(B64("plano")), null)
        ]);

        GmailMessageMapper.Map(WithBody(payload)).Body.Should().Be("plano");
    }

    [Fact]
    public void Map_MultipartWithOnlyHtml_FallsBackToHtml()
    {
        var payload = new GmailMessagePart("multipart/alternative", null, null,
        [
            new GmailMessagePart("text/html", null, new GmailBody(B64("<p>html</p>")), null)
        ]);

        GmailMessageMapper.Map(WithBody(payload)).Body.Should().Be("<p>html</p>");
    }

    [Fact]
    public void Map_NestedMultipart_PrefersThePlainPartInsideTheContainer()
    {
        var payload = new GmailMessagePart("multipart/mixed", null, null,
        [
            new GmailMessagePart("multipart/alternative", null, null,
            [
                new GmailMessagePart("text/html", null, new GmailBody(B64("<p>html</p>")), null),
                new GmailMessagePart("text/plain", null, new GmailBody(B64("plano")), null)
            ])
        ]);

        GmailMessageMapper.Map(WithBody(payload)).Body.Should().Be("plano");
    }

    [Fact]
    public void Map_MalformedBase64Body_DegradesToEmptyInsteadOfThrowing()
    {
        // One broken message must never take down the whole sync batch.
        var act = () => GmailMessageMapper.Map(WithBody(
            new GmailMessagePart("text/plain", null, new GmailBody("!!!not base64!!!"), null)));

        act.Should().NotThrow();
        act().Body.Should().BeEmpty();
    }

    [Fact]
    public void Map_NullPayload_YieldsEmptyBody()
    {
        GmailMessageMapper.Map(new GmailMessageResponse("m", "t", null)).Body.Should().BeEmpty();
    }

    [Fact]
    public void Map_Ids_ArePassedThrough()
    {
        var mapped = GmailMessageMapper.Map(new GmailMessageResponse("id-42", "thread-7", null));

        mapped.MessageId.Should().Be("id-42");
        mapped.ThreadId.Should().Be("thread-7");
    }
}
