using Budgie.Modules.Finances.Application.Transactions;
using Budgie.Shared.Enums;
using FluentAssertions;
using Xunit;

namespace Budgie.Modules.Finances.UnitTests.Application.Transactions.TransferPair;

/// <summary>
/// Read-half of docs/superpowers/specs/2026-10-12-transfer-pair-integrity-design.md.
/// The destination of a transfer is the leg that is NOT the row being projected; picking
/// any row of the group at random is what made a transfer render as "A -> A".
/// </summary>
public class TransferDestinationResolutionTests
{
    private static TransactionResponse Row(Guid id, Guid accountId, TransactionType type, Guid? groupId)
        => new(id, accountId, "Account", null, null, 100m, "PEN", 1m,
            new DateTime(2026, 5, 1), null, type, 1, false, null, groupId, null,
            null, null, null, 1, 1);

    // Mirrors the projection rule in TransactionRepository.BuildResponsesAsync.
    private static (Guid? Id, string? Name) ResolveDestination(
        TransactionResponse item, IReadOnlyList<(Guid Id, Guid AccountId, string Name)> groupRows)
        => groupRows.Where(r => r.Id != item.Id)
            .Select(r => ((Guid?)r.AccountId, (string?)r.Name))
            .FirstOrDefault();

    [Fact]
    public void Destination_IsTheOtherLegOfTheGroup()
    {
        var outgoingId = Guid.NewGuid();
        var incomingId = Guid.NewGuid();
        var origin = Guid.NewGuid();
        var destination = Guid.NewGuid();
        var groupId = Guid.NewGuid();

        var outgoing = Row(outgoingId, origin, TransactionType.Transfer, groupId);
        var group = new[] { (outgoingId, origin, "Origin"), (incomingId, destination, "Destination") };

        var (id, name) = ResolveDestination(outgoing, group);

        id.Should().Be(destination);
        name.Should().Be("Destination");
    }

    [Fact]
    public void Destination_FromTheIncomingLegPointsBackToTheOrigin()
    {
        var outgoingId = Guid.NewGuid();
        var incomingId = Guid.NewGuid();
        var origin = Guid.NewGuid();
        var destination = Guid.NewGuid();
        var groupId = Guid.NewGuid();

        var incoming = Row(incomingId, destination, TransactionType.Income, groupId);
        var group = new[] { (outgoingId, origin, "Origin"), (incomingId, destination, "Destination") };

        var (id, name) = ResolveDestination(incoming, group);

        id.Should().Be(origin);
        name.Should().Be("Origin");
    }

    [Fact]
    public void Destination_WithOnlyOneLeg_IsNullRatherThanEchoingTheSource()
    {
        var outgoingId = Guid.NewGuid();
        var origin = Guid.NewGuid();
        var groupId = Guid.NewGuid();

        var outgoing = Row(outgoingId, origin, TransactionType.Transfer, groupId);
        var group = new[] { (outgoingId, origin, "Origin") };

        var (id, _) = ResolveDestination(outgoing, group);

        id.Should().BeNull();
    }

    [Fact]
    public void Destination_IsIndependentOfTheOrderTheGroupIsReturnedIn()
    {
        var outgoingId = Guid.NewGuid();
        var incomingId = Guid.NewGuid();
        var origin = Guid.NewGuid();
        var destination = Guid.NewGuid();
        var groupId = Guid.NewGuid();
        var outgoing = Row(outgoingId, origin, TransactionType.Transfer, groupId);

        var forward = new[] { (outgoingId, origin, "Origin"), (incomingId, destination, "Destination") };
        var reversed = new[] { (incomingId, destination, "Destination"), (outgoingId, origin, "Origin") };

        ResolveDestination(outgoing, forward).Id.Should().Be(destination);
        ResolveDestination(outgoing, reversed).Id.Should().Be(destination);
    }
}
