using Budgie.Modules.Finances.Application;
using Budgie.Modules.Finances.Application.Transactions.Transfer;
using Budgie.Modules.Finances.Application.Transactions.UpdateTransaction;
using Budgie.Modules.Finances.Domain.Accounts;
using Budgie.Shared.Abstractions;
using Budgie.Shared.Enums;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace Budgie.Modules.Finances.UnitTests.Application.Transactions.TransferPair;

/// <summary>
/// Covers the invariants of docs/superpowers/specs/2026-10-12-transfer-pair-integrity-design.md:
/// a transfer is a group of legs sharing a LinkedTransactionId and must be read, updated,
/// deleted and created as one unit.
/// </summary>
public class TransferPairIntegrityTests
{
    private readonly ITransactionRepository _txRepository = Substitute.For<ITransactionRepository>();
    private readonly IAccountRepository _accountRepository = Substitute.For<IAccountRepository>();
    private readonly ICurrentUserProvider _currentUserProvider = Substitute.For<ICurrentUserProvider>();

    private static (Account Origin, Account Destination) Accounts(Guid userId, decimal origin = 500m, decimal destination = 100m)
    {
        var from = Account.Create(userId, "Origin", "PEN", origin);
        var to = Account.Create(userId, "Destination", "PEN", destination);
        return (from, to);
    }

    private static TransactionForUpdate Leg(Guid id, Guid accountId, TransactionType type,
        decimal amount, DateTime date, Guid groupId, int installments = 1)
        => new(id, type, false, amount, "PEN", 1m, installments, accountId, null, date,
            null, 1, 1, 0m, groupId);

    // ── update ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Update_ShouldApplyNewValuesToBothLegsAndKeepTheLink()
    {
        var userId = Guid.NewGuid();
        var (from, to) = Accounts(userId);
        var groupId = Guid.NewGuid();
        var outId = Guid.NewGuid();
        var inId = Guid.NewGuid();
        var date = new DateTime(2026, 5, 1);
        var newDate = new DateTime(2026, 5, 15);

        _currentUserProvider.GetUserId().Returns(userId);
        _accountRepository.GetByIdAsync(from.Id, userId, Arg.Any<CancellationToken>()).Returns(from);
        _accountRepository.GetByIdAsync(to.Id, userId, Arg.Any<CancellationToken>()).Returns(to);
        _txRepository.GetByIdForUpdateAsync(outId, userId, Arg.Any<CancellationToken>())
            .Returns(Leg(outId, from.Id, TransactionType.Transfer, 100m, date, groupId));
        _txRepository.GetTransferGroupAsync(groupId, userId, Arg.Any<CancellationToken>())
            .Returns(new List<TransactionForUpdate>
            {
                Leg(outId, from.Id, TransactionType.Transfer, 100m, date, groupId),
                Leg(inId, to.Id, TransactionType.Income, 100m, date, groupId),
            });

        var handler = new UpdateTransactionCommandHandler(_txRepository, _accountRepository, _currentUserProvider);
        var result = await handler.Handle(new UpdateTransactionCommand(
            outId, null, 250m, "PEN", 1m, newDate, "movido", TransactionType.Transfer, 1,
            null, null, null, null), CancellationToken.None);

        result.IsError.Should().BeFalse();

        // both legs written with the new values
        await _txRepository.Received(1).UpdateTransferLegAsync(
            outId, from.Id, 250m, "PEN", 1m, newDate, "movido", Arg.Any<CancellationToken>());
        await _txRepository.Received(1).UpdateTransferLegAsync(
            inId, to.Id, 250m, "PEN", 1m, newDate, "movido", Arg.Any<CancellationToken>());

        // balances reverted the old 100 and applied the new 250
        from.Balance.Amount.Should().Be(500m + 100m - 250m);   // revert 100, apply 250 out
        to.Balance.Amount.Should().Be(100m - 100m + 250m);     // revert 100 in, apply 250 in
        await _accountRepository.Received(1).UpdateBalanceAsync(userId, from.Id, Arg.Any<decimal>(), Arg.Any<CancellationToken>());
        await _accountRepository.Received(1).UpdateBalanceAsync(userId, to.Id, Arg.Any<decimal>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Update_ChangingDestination_ShouldMoveTheIncomeLegAndAdjustBothBalances()
    {
        var userId = Guid.NewGuid();
        var (from, to) = Accounts(userId);
        var third = Account.Create(userId, "Third", "PEN", 0m);
        var groupId = Guid.NewGuid();
        var outId = Guid.NewGuid();
        var inId = Guid.NewGuid();
        var date = new DateTime(2026, 5, 1);

        _currentUserProvider.GetUserId().Returns(userId);
        foreach (var a in new[] { from, to, third })
            _accountRepository.GetByIdAsync(a.Id, userId, Arg.Any<CancellationToken>()).Returns(a);
        _txRepository.GetByIdForUpdateAsync(outId, userId, Arg.Any<CancellationToken>())
            .Returns(Leg(outId, from.Id, TransactionType.Transfer, 100m, date, groupId));
        _txRepository.GetTransferGroupAsync(groupId, userId, Arg.Any<CancellationToken>())
            .Returns(new List<TransactionForUpdate>
            {
                Leg(outId, from.Id, TransactionType.Transfer, 100m, date, groupId),
                Leg(inId, to.Id, TransactionType.Income, 100m, date, groupId),
            });

        var handler = new UpdateTransactionCommandHandler(_txRepository, _accountRepository, _currentUserProvider);
        var result = await handler.Handle(new UpdateTransactionCommand(
            outId, null, 100m, "PEN", 1m, date, null, TransactionType.Transfer, 1,
            null, null, null, third.Id), CancellationToken.None);

        result.IsError.Should().BeFalse();

        // income leg landed on the new destination, outgoing stays put
        await _txRepository.Received(1).UpdateTransferLegAsync(
            outId, from.Id, 100m, "PEN", 1m, date, Arg.Any<string?>(), Arg.Any<CancellationToken>());
        await _txRepository.Received(1).UpdateTransferLegAsync(
            inId, third.Id, 100m, "PEN", 1m, date, Arg.Any<string?>(), Arg.Any<CancellationToken>());

        // money left `to` and arrived at `third`
        to.Balance.Amount.Should().Be(100m - 100m);
        third.Balance.Amount.Should().Be(100m);
    }

    [Fact]
    public async Task Update_ChangingOrigin_ShouldMoveTheOutgoingLeg()
    {
        var userId = Guid.NewGuid();
        var (from, to) = Accounts(userId);
        var third = Account.Create(userId, "Third", "PEN", 0m);
        var groupId = Guid.NewGuid();
        var outId = Guid.NewGuid();
        var inId = Guid.NewGuid();
        var date = new DateTime(2026, 5, 1);

        _currentUserProvider.GetUserId().Returns(userId);
        foreach (var a in new[] { from, to, third })
            _accountRepository.GetByIdAsync(a.Id, userId, Arg.Any<CancellationToken>()).Returns(a);
        _txRepository.GetByIdForUpdateAsync(outId, userId, Arg.Any<CancellationToken>())
            .Returns(Leg(outId, from.Id, TransactionType.Transfer, 100m, date, groupId));
        _txRepository.GetTransferGroupAsync(groupId, userId, Arg.Any<CancellationToken>())
            .Returns(new List<TransactionForUpdate>
            {
                Leg(outId, from.Id, TransactionType.Transfer, 100m, date, groupId),
                Leg(inId, to.Id, TransactionType.Income, 100m, date, groupId),
            });

        var handler = new UpdateTransactionCommandHandler(_txRepository, _accountRepository, _currentUserProvider);
        var result = await handler.Handle(new UpdateTransactionCommand(
            outId, null, 100m, "PEN", 1m, date, null, TransactionType.Transfer, 1,
            null, null, third.Id, null), CancellationToken.None);

        result.IsError.Should().BeFalse();
        await _txRepository.Received(1).UpdateTransferLegAsync(
            outId, third.Id, 100m, "PEN", 1m, date, Arg.Any<string?>(), Arg.Any<CancellationToken>());
        from.Balance.Amount.Should().Be(600m);   // reverted only
        third.Balance.Amount.Should().Be(-100m); // applied the outgoing
    }

    [Fact]
    public async Task Update_WithNonTransferType_ShouldBeRejected()
    {
        var userId = Guid.NewGuid();
        var (from, _) = Accounts(userId);
        var groupId = Guid.NewGuid();
        var outId = Guid.NewGuid();
        var date = new DateTime(2026, 5, 1);

        _currentUserProvider.GetUserId().Returns(userId);
        _txRepository.GetByIdForUpdateAsync(outId, userId, Arg.Any<CancellationToken>())
            .Returns(Leg(outId, from.Id, TransactionType.Transfer, 100m, date, groupId));
        _txRepository.GetTransferGroupAsync(groupId, userId, Arg.Any<CancellationToken>())
            .Returns(new List<TransactionForUpdate>
            {
                Leg(outId, from.Id, TransactionType.Transfer, 100m, date, groupId),
                Leg(Guid.NewGuid(), from.Id, TransactionType.Income, 100m, date, groupId),
            });

        var handler = new UpdateTransactionCommandHandler(_txRepository, _accountRepository, _currentUserProvider);
        var result = await handler.Handle(new UpdateTransactionCommand(
            outId, null, 10m, "PEN", 1m, date, null, TransactionType.Expense, 1),
            CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.Errors[0].Code.Should().Be("Transfer.TypeImmutable");
        await _txRepository.DidNotReceive().UpdateTransferLegAsync(
            Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<decimal>(), Arg.Any<string>(),
            Arg.Any<decimal>(), Arg.Any<DateTime>(), Arg.Any<string?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Update_WithInstallments_ShouldBeRejected()
    {
        var userId = Guid.NewGuid();
        var (from, _) = Accounts(userId);
        var groupId = Guid.NewGuid();
        var outId = Guid.NewGuid();
        var date = new DateTime(2026, 5, 1);

        _currentUserProvider.GetUserId().Returns(userId);
        _txRepository.GetByIdForUpdateAsync(outId, userId, Arg.Any<CancellationToken>())
            .Returns(Leg(outId, from.Id, TransactionType.Transfer, 100m, date, groupId));

        var handler = new UpdateTransactionCommandHandler(_txRepository, _accountRepository, _currentUserProvider);
        var result = await handler.Handle(new UpdateTransactionCommand(
            outId, null, 100m, "PEN", 1m, date, null, TransactionType.Transfer, 3),
            CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.Errors[0].Code.Should().Be("Transfer.NoInstallments");
        await _txRepository.DidNotReceive().GetTransferGroupAsync(
            Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Update_WithMalformedGroup_ShouldBeRejectedInsteadOfWriting()
    {
        var userId = Guid.NewGuid();
        var (from, _) = Accounts(userId);
        var groupId = Guid.NewGuid();
        var outId = Guid.NewGuid();
        var date = new DateTime(2026, 5, 1);

        _currentUserProvider.GetUserId().Returns(userId);
        _txRepository.GetByIdForUpdateAsync(outId, userId, Arg.Any<CancellationToken>())
            .Returns(Leg(outId, from.Id, TransactionType.Transfer, 100m, date, groupId));
        // only the outgoing leg survived: the pair was already broken
        _txRepository.GetTransferGroupAsync(groupId, userId, Arg.Any<CancellationToken>())
            .Returns(new List<TransactionForUpdate>
            {
                Leg(outId, from.Id, TransactionType.Transfer, 100m, date, groupId),
            });

        var handler = new UpdateTransactionCommandHandler(_txRepository, _accountRepository, _currentUserProvider);
        var result = await handler.Handle(new UpdateTransactionCommand(
            outId, null, 100m, "PEN", 1m, date, null, TransactionType.Transfer, 1),
            CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.Errors[0].Code.Should().Be("Transfer.MalformedGroup");
        await _txRepository.DidNotReceive().UpdateTransferLegAsync(
            Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<decimal>(), Arg.Any<string>(),
            Arg.Any<decimal>(), Arg.Any<DateTime>(), Arg.Any<string?>(), Arg.Any<CancellationToken>());
        await _accountRepository.DidNotReceive().UpdateBalanceAsync(
            Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<decimal>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Update_WithoutLinkedGroup_ShouldBeRejected()
    {
        var userId = Guid.NewGuid();
        var (from, _) = Accounts(userId);
        var outId = Guid.NewGuid();
        var date = new DateTime(2026, 5, 1);

        _currentUserProvider.GetUserId().Returns(userId);
        _txRepository.GetByIdForUpdateAsync(outId, userId, Arg.Any<CancellationToken>())
            .Returns(Leg(outId, from.Id, TransactionType.Transfer, 100m, date, Guid.Empty) with { LinkedTransactionId = null });

        var handler = new UpdateTransactionCommandHandler(_txRepository, _accountRepository, _currentUserProvider);
        var result = await handler.Handle(new UpdateTransactionCommand(
            outId, null, 100m, "PEN", 1m, date, null, TransactionType.Transfer, 1),
            CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.Errors[0].Code.Should().Be("Transfer.MalformedGroup");
    }

    // ── create idempotency ────────────────────────────────────────────────────

    [Fact]
    public async Task Create_WithExistingExternalUid_ShouldReturnExistingIdWithoutWriting()
    {
        var userId = Guid.NewGuid();
        var (from, to) = Accounts(userId);
        var existingId = Guid.NewGuid();

        _currentUserProvider.GetUserId().Returns(userId);
        _txRepository.GetIdByExternalUidAsync(userId, "email:1", Arg.Any<CancellationToken>())
            .Returns(existingId);

        var handler = new TransferCommandHandler(_accountRepository, _txRepository, _currentUserProvider);
        var result = await handler.Handle(new TransferCommand(
            from.Id, to.Id, 100m, 0m, "PEN", 1m, DateTime.UtcNow, "nota", "email:1"),
            CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.Should().Be(existingId);
        await _txRepository.DidNotReceive().AddAsync(
            Arg.Any<Budgie.Modules.Finances.Domain.Transactions.Transaction>(),
            Arg.Any<CancellationToken>(), Arg.Any<System.Data.IDbTransaction?>());
        await _accountRepository.DidNotReceive().UpdateBalanceAsync(
            Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<decimal>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Create_WithNewExternalUid_ShouldReturnTheOutgoingLegId()
    {
        var userId = Guid.NewGuid();
        var (from, to) = Accounts(userId);

        _currentUserProvider.GetUserId().Returns(userId);
        _accountRepository.GetByIdAsync(from.Id, userId, Arg.Any<CancellationToken>()).Returns(from);
        _accountRepository.GetByIdAsync(to.Id, userId, Arg.Any<CancellationToken>()).Returns(to);
        _txRepository.GetIdByExternalUidAsync(userId, "email:2", Arg.Any<CancellationToken>())
            .Returns((Guid?)null);

        var handler = new TransferCommandHandler(_accountRepository, _txRepository, _currentUserProvider);
        var result = await handler.Handle(new TransferCommand(
            from.Id, to.Id, 100m, 0m, "PEN", 1m, DateTime.UtcNow, "nota", "email:2"),
            CancellationToken.None);

        result.IsError.Should().BeFalse();

        // the returned id is a real transaction row, never the phantom group id
        var created = _txRepository.ReceivedCalls()
            .Select(c => c.GetArguments())
            .SelectMany(a => a.OfType<Budgie.Modules.Finances.Domain.Transactions.Transaction>())
            .ToList();
        created.Should().HaveCount(2);
        result.Value.Should().Be(created.Single(t => t.Type == TransactionType.Transfer).Id);
        created.Should().OnlyContain(t => t.LinkedTransactionId.HasValue);

        // ExternalUid lives on the outgoing leg only: the unique index on
        // (UserId, ExternalUid) would reject the incoming leg if it repeated the value,
        // exactly as CreateTransactionCommandHandler documents for installments.
        created.Single(t => t.Type == TransactionType.Transfer).ExternalUid.Should().Be("email:2");
        created.Single(t => t.Type == TransactionType.Income).ExternalUid.Should().BeNull();
    }
}
