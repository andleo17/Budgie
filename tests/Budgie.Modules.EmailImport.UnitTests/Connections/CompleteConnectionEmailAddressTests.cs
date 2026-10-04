using Budgie.Modules.EmailImport.Application;
using Budgie.Modules.EmailImport.Application.Abstractions;
using Budgie.Modules.EmailImport.Application.Connections;
using Budgie.Modules.EmailImport.Application.Connections.CompleteConnection;
using Budgie.Modules.EmailImport.Domain.Connections;
using Budgie.Modules.EmailImport.UnitTests.TestDoubles;
using Budgie.Shared.Enums;
using ErrorOr;
using FluentAssertions;
using Mediator;
using Microsoft.Extensions.Configuration;
using NSubstitute;
using Xunit;

namespace Budgie.Modules.EmailImport.UnitTests.Connections;

/// <summary>
/// Slice 2 (§2.2): the connection records the mailbox's real address instead of the placeholder
/// <c>"connected"</c>. Before this, <see cref="EmailCredentials"/> did not carry the address at
/// all, so the UI could not tell the user which mailbox they had connected — and the placeholder
/// sat inside <c>UNIQUE("UserId","Provider","Email")</c>.
/// </summary>
public class CompleteConnectionEmailAddressTests
{
    private readonly IEmailConnectionRepository _connections = Substitute.For<IEmailConnectionRepository>();
    private readonly IEmailProviderResolver _resolver = Substitute.For<IEmailProviderResolver>();
    private readonly IEmailProviderClient _client = Substitute.For<IEmailProviderClient>();

    private static readonly Guid UserId = Guid.NewGuid();
    private const string RedirectUri = "https://budgie.test/api/email-import/connections/gmail/callback";

    /// <summary>
    /// One service, one issued state, shared by the test body and the handler. A real signed state
    /// is used rather than a substitute because the state IS the only thing binding a redirect
    /// that arrives without a JWT to its owner — replacing it here would test nothing about the
    /// flow being verified.
    /// </summary>
    private sealed class Flow(OAuthStateService stateService, string state)
    {
        public string State { get; } = state;
        public CompleteConnectionCommandHandler Handler(
            IEmailConnectionRepository connections, IEmailProviderResolver resolver) =>
            new(connections, resolver, stateService);
    }

    private static Flow FreshFlow()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?> { ["Jwt:Secret"] = "clave-de-firma-de-prueba-para-los-tests" }).Build();

        var stateService = new OAuthStateService(configuration, new TestTimeProvider());
        return new Flow(stateService, stateService.Issue(UserId, EmailProvider.Gmail, RedirectUri));
    }

    private static EmailCredentials Credentials(string? email) =>
        new("access", "v1:bm9uY2U=:Y2lwaGVy:dGFn", DateTimeOffset.UtcNow.AddHours(1), email);

    private void ProviderReturns(EmailCredentials credentials)
    {
        _resolver.Resolve(EmailProvider.Gmail).Returns(_client);
        _client.ExchangeCodeAsync(Arg.Any<string>(), "code", Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(credentials);
        _connections.AddAsync(Arg.Any<EmailConnection>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
    }

    private void NoConnectionYet() =>
        _connections.ExistsByProviderAsync(UserId, EmailProvider.Gmail, Arg.Any<CancellationToken>())
            .Returns(false);

    [Fact]
    public async Task Handle_ProviderReportsTheAddress_StoresTheRealMailbox()
    {
        var flow = FreshFlow();
        NoConnectionYet();
        ProviderReturns(Credentials("usuario@banco.es"));

        var result = await flow.Handler(_connections, _resolver).Handle(
            new CompleteConnectionCommand(flow.State, "code"), CancellationToken.None);

        result.IsError.Should().BeFalse();
        await _connections.Received(1).AddAsync(
            Arg.Is<EmailConnection>(c => c.Email == "usuario@banco.es"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ProviderReportsNoAddress_FallsBackToThePlaceholder()
    {
        var flow = FreshFlow();
        NoConnectionYet();
        ProviderReturns(Credentials(email: null));

        var result = await flow.Handler(_connections, _resolver).Handle(
            new CompleteConnectionCommand(flow.State, "code"), CancellationToken.None);

        // The mailbox IS connected either way. Refusing the connection because an informational
        // profile read failed would be a worse outcome than not knowing the address.
        result.IsError.Should().BeFalse();
        await _connections.Received(1).AddAsync(
            Arg.Is<EmailConnection>(c => c.Email == "connected"),
            Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Handle_ProviderReportsABlankAddress_FallsBackToThePlaceholder(string email)
    {
        var flow = FreshFlow();
        NoConnectionYet();
        ProviderReturns(Credentials(email));

        await flow.Handler(_connections, _resolver).Handle(
            new CompleteConnectionCommand(flow.State, "code"), CancellationToken.None);

        await _connections.Received(1).AddAsync(
            Arg.Is<EmailConnection>(c => c.Email == "connected"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_OneConnectionPerProvider_IsStillEnforced()
    {
        var flow = FreshFlow();
        ProviderReturns(Credentials("usuario@banco.es"));
        // Pretend one already exists for this provider.
        _connections.ExistsByProviderAsync(UserId, EmailProvider.Gmail, Arg.Any<CancellationToken>())
            .Returns(true);

        var result = await flow.Handler(_connections, _resolver).Handle(
            new CompleteConnectionCommand(flow.State, "code"), CancellationToken.None);

        // Storing a real address does NOT relax this: the DDL constrains the triple
        // (UserId, Provider, Email), not the pair, so the application-level check is what keeps
        // "one mailbox per provider per user" true. Removing it would allow several connections
        // whose addresses happen to be identical.
        result.IsError.Should().BeTrue();
        result.FirstError.Type.Should().Be(ErrorType.Conflict);
        await _connections.DidNotReceive().AddAsync(Arg.Any<EmailConnection>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ReplayedState_IsRejected()
    {
        // The single-use property of the state must survive this slice: a leaked authorization
        // code replayed against the callback must not hang a connection on someone else's account.
        var flow = FreshFlow();
        NoConnectionYet();
        ProviderReturns(Credentials("usuario@banco.es"));
        var handler = flow.Handler(_connections, _resolver);

        await handler.Handle(new CompleteConnectionCommand(flow.State, "code"), CancellationToken.None);
        var replay = await handler.Handle(new CompleteConnectionCommand(flow.State, "code"), CancellationToken.None);

        replay.IsError.Should().BeTrue();
        replay.FirstError.Code.Should().Be("EmailImport.InvalidState");
        await _connections.Received(1).AddAsync(Arg.Any<EmailConnection>(), Arg.Any<CancellationToken>());
    }
}