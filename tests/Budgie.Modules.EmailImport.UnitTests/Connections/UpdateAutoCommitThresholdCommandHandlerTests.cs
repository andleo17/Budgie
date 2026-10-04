using Budgie.Modules.EmailImport.Application;
using Budgie.Modules.EmailImport.Application.Connections.UpdateThreshold;
using Budgie.Modules.EmailImport.Domain.Connections;
using Budgie.Shared.Abstractions;
using Budgie.Shared.Enums;
using ErrorOr;
using FluentAssertions;
using Mediator;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Xunit;

namespace Budgie.Modules.EmailImport.UnitTests.Connections;

/// <summary>
/// Slice 1 (§2.1): the auto-commit threshold finally has a route. The value existed as a column,
/// a <c>Create</c> parameter and a validated domain method, but nothing could change it — the
/// settings slider had to be read-only with a note saying so.
/// </summary>
public class UpdateAutoCommitThresholdCommandHandlerTests
{
    private readonly IEmailConnectionRepository _connections = Substitute.For<IEmailConnectionRepository>();
    private readonly ICurrentUserProvider _currentUser = Substitute.For<ICurrentUserProvider>();
    private readonly IMediator _mediator = Substitute.For<IMediator>();

    private static readonly Guid UserId = Guid.NewGuid();

    private UpdateAutoCommitThresholdCommandHandler Handler() =>
        new(_connections, _currentUser);

    private EmailConnection Connected()
    {
        _currentUser.GetUserId().Returns(UserId);
        var connection = EmailConnection.Create(
            UserId, EmailProvider.Gmail, "usuario@banco.es", "v1:bm9uY2U=:Y2lwaGVy:dGFn");
        _connections.GetByProviderAsync(UserId, EmailProvider.Gmail, Arg.Any<CancellationToken>())
            .Returns(connection);
        return connection;
    }

    [Theory]
    [InlineData("0.95")]
    [InlineData("0")]
    [InlineData("1")]
    [InlineData("0.5")]
    public async Task Handle_PersistsTheNewThreshold(string value)
    {
        var connection = Connected();

        var result = await Handler().Handle(
            new UpdateAutoCommitThresholdCommand(EmailProvider.Gmail, decimal.Parse(value)),
            CancellationToken.None);

        result.IsError.Should().BeFalse();
        connection.AutoCommitMinConfidence.Should().Be(decimal.Parse(value));
        await _connections.Received(1).UpdateAsync(connection, Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData("1.5")]
    [InlineData("-0.1")]
    [InlineData("2")]
    public async Task Handle_ThresholdOutsideTheRange_IsAValidationErrorAndPersistsNothing(string value)
    {
        var connection = Connected();

        var result = await Handler().Handle(
            new UpdateAutoCommitThresholdCommand(EmailProvider.Gmail, decimal.Parse(value)),
            CancellationToken.None);

        // The domain throws ArgumentException for an out-of-range value. Without the catch this
        // would be a 500, so the catch IS the contract of this endpoint.
        result.IsError.Should().BeTrue();
        result.FirstError.Type.Should().Be(ErrorType.Validation);
        result.FirstError.Code.Should().Be("EmailImport.InvalidThreshold");

        await _connections.DidNotReceive().UpdateAsync(Arg.Any<EmailConnection>(), Arg.Any<CancellationToken>());
        connection.AutoCommitMinConfidence.Should().Be(
            EmailConnection.DefaultAutoCommitMinConfidence,
            "a rejected threshold must not leave the connection half-updated");
    }

    [Fact]
    public async Task Handle_NoConnectionForTheProvider_IsNotFound()
    {
        _currentUser.GetUserId().Returns(UserId);
        _connections.GetByProviderAsync(UserId, EmailProvider.Gmail, Arg.Any<CancellationToken>())
            .Returns((EmailConnection?)null);

        var result = await Handler().Handle(
            new UpdateAutoCommitThresholdCommand(EmailProvider.Gmail, 0.5m), CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Type.Should().Be(ErrorType.NotFound);
        result.FirstError.Code.Should().Be("EmailImport.NotConnected");
        await _connections.DidNotReceive().UpdateAsync(Arg.Any<EmailConnection>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_PersistFails_PropagatesTheInfrastructureException()
    {
        var connection = Connected();
        _connections.UpdateAsync(connection, Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("connection reset"));

        var act = async () => await Handler().Handle(
            new UpdateAutoCommitThresholdCommand(EmailProvider.Gmail, 0.5m), CancellationToken.None);

        // A failed write must NOT be reported as success: the domain already holds the new value
        // in memory, so swallowing this would leave the caller believing the threshold changed.
        await act.Should().ThrowAsync<InvalidOperationException>();
    }
}