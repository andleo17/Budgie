using Budgie.Modules.EmailImport.Application;
using Budgie.Modules.EmailImport.Application.Abstractions;
using Budgie.Modules.EmailImport.Domain.Connections;
using Budgie.Modules.EmailImport.Domain.Rules;
using Budgie.Modules.EmailImport.Infrastructure.Email.Gmail;
using Budgie.Modules.EmailImport.Infrastructure.Security;
using Budgie.Shared.Enums;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Xunit;

namespace Budgie.Modules.EmailImport.UnitTests.Email;

/// <summary>
/// Slice 2 (§2.2): the provider reports the connected mailbox's address during the authorization
/// exchange.
/// </summary>
/// <remarks>
/// <b>One extra call per connect, not zero.</b> The profile endpoint already exists in this class —
/// it is what anchors the <c>historyId</c> cursor, but that happens on the first
/// <c>ListNewAsync</c> tick, i.e. AFTER the exchange. So connecting a mailbox now costs a second
/// round trip to the same endpoint: no new endpoint, no new scope (<c>gmail.readonly</c> already
/// covers it), and the response carries both <c>emailAddress</c> and <c>historyId</c>. Only one
/// profile read happens per tick and it is best-effort.
/// </remarks>
public class GmailEmailProviderMailboxAddressTests
{
    private const string Key = "MDEyMzQ1Njc4OWFiY2RlZjAxMjM0NTY3ODlhYmNkZWY="; // base64 32 bytes

    private readonly IGmailMailboxApi _api = Substitute.For<IGmailMailboxApi>();
    private readonly ISecretProtector _protector = Substitute.For<ISecretProtector>();

    private GmailEmailProvider Provider() =>
        new(_protector, _api, NullLogger<GmailEmailProvider>.Instance);

    private void TokenExchangeSucceeds()
    {
        _api.ExchangeCodeAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new GmailTokenResponse("access-token", "refresh-token", 3600));
        _protector.Protect(Arg.Any<string>()).Returns("v1:bm9uY2U=:Y2lwaGVy:dGFn");
    }

    private static EmailConnection Connection() =>
        EmailConnection.Create(
            Guid.NewGuid(), EmailProvider.Gmail, "usuario@banco.es", "v1:bm9uY2U=:Y2lwaGVy:dGFn");

    [Fact]
    public async Task ExchangeCodeAsync_ProfileReportsAnAddress_ReturnsItOnTheCredentials()
    {
        TokenExchangeSucceeds();
        _api.GetProfileAsync("access-token", Arg.Any<CancellationToken>())
            .Returns(new GmailProfileResponse("usuario@banco.es", "12345"));

        var credentials = await Provider().ExchangeCodeAsync(
            string.Empty, "code", "https://budgie.test/cb", CancellationToken.None);

        credentials.Email.Should().Be("usuario@banco.es",
            "this is what CompleteConnectionCommandHandler stores as EmailConnection.Email");
    }

    [Fact]
    public async Task ExchangeCodeAsync_ProfileHasNoAddress_ReturnsNull()
    {
        TokenExchangeSucceeds();
        _api.GetProfileAsync("access-token", Arg.Any<CancellationToken>())
            .Returns(new GmailProfileResponse(null, "12345"));

        var credentials = await Provider().ExchangeCodeAsync(
            string.Empty, "code", "https://budgie.test/cb", CancellationToken.None);

        credentials.Email.Should().BeNull();
    }

    [Fact]
    public async Task ExchangeCodeAsync_ProfileCallFails_StillCompletesTheExchangeWithNoAddress()
    {
        TokenExchangeSucceeds();
        _api.GetProfileAsync("access-token", Arg.Any<CancellationToken>())
            .ThrowsAsync(new EmailProviderException(EmailProviderError.Transient, "profile unavailable"));

        var credentials = await Provider().ExchangeCodeAsync(
            string.Empty, "code", "https://budgie.test/cb", CancellationToken.None);

        // The mailbox IS connected. Failing the whole authorization because an INFORMATIONAL
        // endpoint was unreachable would be strictly worse than not knowing the address — and the
        // empty catch that produces this null must therefore be documented, which is why it is
        // not written as a silent catch-all.
        credentials.Should().NotBeNull();
        credentials.AccessToken.Should().Be("access-token");
        credentials.EncryptedRefreshToken.Should().Be("v1:bm9uY2U=:Y2lwaGVy:dGFn");
        credentials.Email.Should().BeNull();
    }

    [Fact]
    public async Task ExchangeCodeAsync_ProfileAddressIsBlank_ReturnsNull()
    {
        TokenExchangeSucceeds();
        _api.GetProfileAsync("access-token", Arg.Any<CancellationToken>())
            .Returns(new GmailProfileResponse("   ", "12345"));

        var credentials = await Provider().ExchangeCodeAsync(
            string.Empty, "code", "https://budgie.test/cb", CancellationToken.None);

        credentials.Email.Should().BeNull("a blank address is not an address");
    }

    [Fact]
    public async Task ExchangeCodeAsync_StillRejectsAnExchangeWithNoRefreshToken()
    {
        _api.ExchangeCodeAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new GmailTokenResponse("access-token", null, 3600));

        var act = () => Provider().ExchangeCodeAsync(
            string.Empty, "code", "https://budgie.test/cb", CancellationToken.None);

        // The best-effort profile read must not have weakened the one validation that matters
        // here: without a refresh token there is nothing to sync with later.
        await act.Should().ThrowAsync<EmailProviderException>();
    }

    [Fact]
    public async Task RefreshAsync_DoesNotReReadTheAddress()
    {
        // Property names are PascalCase because StoredCredential is deserialized with the default
        // options; an ad-hoc JSON shape here would fail the "incomplete credential" guard for the
        // wrong reason.
        _protector.Unprotect(Arg.Any<string>())
            .Returns("""{"AccessToken":"old","RefreshToken":"refresh","ExpiresAt":"2030-01-01T00:00:00+00:00"}""");
        _api.RefreshAsync("refresh", Arg.Any<CancellationToken>())
            .Returns(new GmailTokenResponse("new-access", "new-refresh", 3600));
        _protector.Protect(Arg.Any<string>()).Returns("v1:bm9uY2U=:Y2lwaGVyOm5ld3M=");

        var credentials = await Provider().RefreshAsync("v1:bm9uY2U=:Y2lwaGVy:dGFn", CancellationToken.None);

        // By the refresh path the address is already persisted on EmailConnection.Email, so there
        // is nothing to re-fetch — and one fewer API call per tick.
        credentials.Email.Should().BeNull();
        await _api.DidNotReceive().GetProfileAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }
}