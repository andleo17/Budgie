using System.Text.Json;
using Budgie.Modules.EmailImport.Application;
using Budgie.Modules.EmailImport.Application.Abstractions;
using Budgie.Modules.EmailImport.Domain.Connections;
using Budgie.Modules.EmailImport.Domain.Parsing;
using Budgie.Modules.EmailImport.Infrastructure.Email.Gmail;
using Budgie.Shared.Enums;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace Budgie.Modules.EmailImport.UnitTests.Email;

/// <summary>
/// A freshly connected mailbox has <c>SyncCursor == null</c>. Without re-anchoring to the
/// mailbox's current historyId on that first tick, every sync would re-download and re-parse
/// the whole 7-day recovery window forever, relying on the staging UNIQUE to discard it —
/// growing cost, quota burn and an inflated LastSkippedCount that never recovers.
/// </summary>
public class GmailEmailProviderCursorAnchoringTests
{
    private static EmailConnection Connection() => EmailConnection.Create(
        userId: Guid.NewGuid(),
        provider: EmailProvider.Gmail,
        email: "user@gmail.com",
        encryptedCredentials: "v1:xxx");

    private const string CredentialJson =
        """{"AccessToken":"at","RefreshToken":"rt","ExpiresAt":"2999-01-01T00:00:00Z"}""";

    private static GmailMessageResponse Envelope(string id) => new(
        Id: id,
        ThreadId: null,
        Payload: new GmailMessagePart(
            MimeType: "text/plain",
            Headers:
            [
                new GmailHeader("From", "banco@example.com"),
                new GmailHeader("Subject", "Aviso"),
                new GmailHeader("Date", "Thu, 2 Oct 2026 10:15:30 +0200"),
            ],
            Body: new GmailBody(Convert.ToBase64String(
                System.Text.Encoding.UTF8.GetBytes("Importe: 45,20 EUR"))),
            Parts: null));

    private static EmailMessage Message(string id) => new(
        MessageId: id,
        ThreadId: null,
        FromAddress: "banco@example.com",
        Subject: "Aviso",
        Body: "Importe: 45,20 EUR",
        ReceivedAt: DateTimeOffset.UtcNow);

    [Fact]
    public async Task FirstTickWithNullCursor_AnchorsToTheCurrentHistoryId()
    {
        var connection = Connection();
        connection.SyncCursor.Should().BeNull("this is a freshly connected mailbox");

        var protector = Substitute.For<ISecretProtector>();
        protector.Unprotect(Arg.Any<string>()).Returns(
            """{"AccessToken":"at","RefreshToken":"rt","ExpiresAt":"2999-01-01T00:00:00Z"}""");

        var api = Substitute.For<IGmailMailboxApi>();
        api.GetProfileAsync("at", Arg.Any<CancellationToken>())
            .Returns(new GmailProfileResponse("user@gmail.com", "history-current"));
        api.ListMessageIdsAsync("at", Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(["m1", "m2"]);
        api.GetMessageAsync("at", Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(ci => Envelope(ci.ArgAt<string>(1)));

        var provider = new GmailEmailProvider(protector, api, NullLogger<GmailEmailProvider>.Instance);
        var messages = await provider.ListNewAsync(connection, CancellationToken.None);

        messages.Should().HaveCount(2);
        connection.SyncCursor.Should().Be("history-current",
            "otherwise every tick would re-read the 7-day window forever");

        await api.Received(1).GetProfileAsync("at", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task FirstTickAnchors_BeforeListingTheRecoveryWindow()
    {
        // Anchoring first is deliberate: the window listing is query-based (newer_than:7d)
        // and ignores the cursor, so anchoring first cannot shrink it, and every message it
        // returns has a history position <= the profile's, so the next history.list sees them.
        var connection = Connection();
        var protector = Substitute.For<ISecretProtector>();
        protector.Unprotect(Arg.Any<string>()).Returns(
            """{"AccessToken":"at","RefreshToken":"rt","ExpiresAt":"2999-01-01T00:00:00Z"}""");

        var order = new List<string>();
        var api = Substitute.For<IGmailMailboxApi>();
        api.GetProfileAsync("at", Arg.Any<CancellationToken>()).Returns(_ =>
        {
            order.Add("profile");
            return new GmailProfileResponse("user@gmail.com", "history-current");
        });
        api.ListMessageIdsAsync("at", Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(_ =>
        {
            order.Add("list");
            return ["m1"];
        });
        api.GetMessageAsync("at", Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(ci => Envelope(ci.ArgAt<string>(1)));

        var provider = new GmailEmailProvider(protector, api, NullLogger<GmailEmailProvider>.Instance);
        await provider.ListNewAsync(connection, CancellationToken.None);

        order.Should().Equal("profile", "list");
    }

    [Fact]
    public async Task TickWithAnExistingCursor_UsesHistoryAndDoesNotTouchTheProfile()
    {
        var connection = Connection();
        connection.AdvanceCursor("history-previous");

        var protector = Substitute.For<ISecretProtector>();
        protector.Unprotect(Arg.Any<string>()).Returns(
            """{"AccessToken":"at","RefreshToken":"rt","ExpiresAt":"2999-01-01T00:00:00Z"}""");

        var api = Substitute.For<IGmailMailboxApi>();
        api.ListHistoryAsync("at", "history-previous", Arg.Any<CancellationToken>())
            .Returns(new GmailHistoryPage(["m9"], "history-newest"));
        api.GetMessageAsync("at", Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(ci => Envelope(ci.ArgAt<string>(1)));

        var provider = new GmailEmailProvider(protector, api, NullLogger<GmailEmailProvider>.Instance);
        var messages = await provider.ListNewAsync(connection, CancellationToken.None);

        messages.Should().ContainSingle();
        connection.SyncCursor.Should().Be("history-newest");
        await api.DidNotReceive().GetProfileAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task FailedProfileRead_LeavesTheCursorNullButStillListsTheWindow()
    {
        var connection = Connection();
        var protector = Substitute.For<ISecretProtector>();
        protector.Unprotect(Arg.Any<string>()).Returns(
            """{"AccessToken":"at","RefreshToken":"rt","ExpiresAt":"2999-01-01T00:00:00Z"}""");

        var api = Substitute.For<IGmailMailboxApi>();
        api.GetProfileAsync("at", Arg.Any<CancellationToken>()).Returns<Task<GmailProfileResponse>>(_ =>
            throw new EmailProviderException(EmailProviderError.Transient, "boom"));
        api.ListMessageIdsAsync("at", Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(["m1"]);
        api.GetMessageAsync("at", Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(ci => Envelope(ci.ArgAt<string>(1)));

        var provider = new GmailEmailProvider(protector, api, NullLogger<GmailEmailProvider>.Instance);
        var messages = await provider.ListNewAsync(connection, CancellationToken.None);

        // Best-effort: a failed anchor is safe (the staging UNIQUE dedupes the re-read),
        // just wasteful. It must not lose the window's messages.
        messages.Should().ContainSingle();
        connection.SyncCursor.Should().BeNull();
    }
}
