using Budgie.Modules.Finances.Application;
using Budgie.Modules.Finances.Application.Transactions.DeleteTransaction;
using Budgie.Modules.Finances.Domain.Accounts;
using Budgie.Shared.Abstractions;
using Budgie.Shared.Enums;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace Budgie.Modules.Finances.UnitTests.Application.Transactions.TransferPair;

/// <summary>
/// Delete-half of docs/superpowers/specs/2026-10-12-transfer-pair-integrity-design.md:
/// deleting any leg of a transfer must remove the whole group and leave every balance
/// consistent.
/// </summary>
public class TransferDeleteTests
{
    private readonly ITransactionRepository _txRepository = Substitute.For<ITransactionRepository>();
    private readonly IAccountRepository _accountRepository = Substitute.For<IAccountRepository>();
    private readonly ICurrentUserProvider _currentUserProvider = Substitute.For<ICurrentUserProvider>();

    private static TransactionForUpdate Leg(Guid id, Guid accountId, TransactionType type,
        decimal amount, Guid groupId)
        => new(id, type, false, amount, "PEN", 1m, 1, accountId, null, new DateTime(2026, 5, 1),
            null, 1, 1, 0m, groupId);

    [Fact]
    public async Task Delete_AnyLeg_ShouldRemoveTheWholeGroup()
    {
        var userId = Guid.NewGuid();
        var from = Account.Create(userId, "Origin", "PEN", 500m);
        var to = Account.Create(userId, "Destination", "PEN", 100m);
        var groupId = Guid.NewGuid();
        var outId = Guid.NewGuid();
        var inId = Guid.NewGuid();

        _currentUserProvider.GetUserId().Returns(userId);
        foreach (var a in new[] { from, to })
            _accountRepository.GetByIdAsync(a.Id, userId, Arg.Any<CancellationToken>()).Returns(a);
        // the caller names the INCOMING leg on purpose: the old code deleted only this row
        _txRepository.GetByIdForDeleteAsync(inId, userId, Arg.Any<CancellationToken>())
            .Returns(new TransactionInfo(inId, TransactionType.Income, false, 100m, "PEN", 1m, 1,
                to.Id, null, groupId));
        _txRepository.GetTransferGroupAsync(groupId, userId, Arg.Any<CancellationToken>())
            .Returns(new List<TransactionForUpdate>
            {
                Leg(outId, from.Id, TransactionType.Transfer, 100m, groupId),
                Leg(inId, to.Id, TransactionType.Income, 100m, groupId),
            });

        var handler = new DeleteTransactionCommandHandler(_txRepository, _accountRepository, _currentUserProvider);
        var result = await handler.Handle(new DeleteTransactionCommand(inId), CancellationToken.None);

        result.IsError.Should().BeFalse();

        // every leg deleted, not just the one requested
        await _txRepository.Received(1).DeleteTransactionAsync(outId, Arg.Any<CancellationToken>());
        await _txRepository.Received(1).DeleteTransactionAsync(inId, Arg.Any<CancellationToken>());

        // both balances reverted: money back on the origin, off the destination
        from.Balance.Amount.Should().Be(600m);
        to.Balance.Amount.Should().Be(0m);
        await _accountRepository.Received(1).UpdateBalanceAsync(userId, from.Id, 600m, Arg.Any<CancellationToken>());
        await _accountRepository.Received(1).UpdateBalanceAsync(userId, to.Id, 0m, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Delete_GroupWithFeeLeg_ShouldRevertTheFeeToo()
    {
        var userId = Guid.NewGuid();
        var from = Account.Create(userId, "Origin", "PEN", 500m);
        var to = Account.Create(userId, "Destination", "PEN", 100m);
        var groupId = Guid.NewGuid();
        var outId = Guid.NewGuid();
        var inId = Guid.NewGuid();
        var feeId = Guid.NewGuid();

        _currentUserProvider.GetUserId().Returns(userId);
        _accountRepository.GetByIdAsync(from.Id, userId, Arg.Any<CancellationToken>()).Returns(from);
        _accountRepository.GetByIdAsync(to.Id, userId, Arg.Any<CancellationToken>()).Returns(to);
        _txRepository.GetByIdForDeleteAsync(outId, userId, Arg.Any<CancellationToken>())
            .Returns(new TransactionInfo(outId, TransactionType.Transfer, false, 100m, "PEN", 1m, 1,
                from.Id, null, groupId));
        _txRepository.GetTransferGroupAsync(groupId, userId, Arg.Any<CancellationToken>())
            .Returns(new List<TransactionForUpdate>
            {
                Leg(outId, from.Id, TransactionType.Transfer, 100m, groupId),
                Leg(inId, to.Id, TransactionType.Income, 100m, groupId),
                Leg(feeId, from.Id, TransactionType.Expense, 3m, groupId),
            });

        var handler = new DeleteTransactionCommandHandler(_txRepository, _accountRepository, _currentUserProvider);
        var result = await handler.Handle(new DeleteTransactionCommand(outId), CancellationToken.None);

        result.IsError.Should().BeFalse();
        await _txRepository.Received(1).DeleteTransactionAsync(feeId, Arg.Any<CancellationToken>());

        // 500 back from the transfer plus 3 back from the fee
        from.Balance.Amount.Should().Be(603m);
        to.Balance.Amount.Should().Be(0m);
    }

    [Fact]
    public async Task Delete_WhenGroupHasASingleLeg_ShouldNotUseTheGroupPath()
    {
        var userId = Guid.NewGuid();
        var account = Account.Create(userId, "Origin", "PEN", 500m);
        var groupId = Guid.NewGuid();
        var outId = Guid.NewGuid();

        _currentUserProvider.GetUserId().Returns(userId);
        _accountRepository.GetByIdAsync(account.Id, userId, Arg.Any<CancellationToken>()).Returns(account);
        _txRepository.GetByIdForDeleteAsync(outId, userId, Arg.Any<CancellationToken>())
            .Returns(new TransactionInfo(outId, TransactionType.Transfer, false, 100m, "PEN", 1m, 1,
                account.Id, null, groupId));
        _txRepository.GetTransferGroupAsync(groupId, userId, Arg.Any<CancellationToken>())
            .Returns(new List<TransactionForUpdate> { Leg(outId, account.Id, TransactionType.Transfer, 100m, groupId) });
        _txRepository.GetChildTransactionsForDeleteAsync(outId, Arg.Any<CancellationToken>())
            .Returns(new List<TransactionInfo>());

        var handler = new DeleteTransactionCommandHandler(_txRepository, _accountRepository, _currentUserProvider);
        var result = await handler.Handle(new DeleteTransactionCommand(outId), CancellationToken.None);

        result.IsError.Should().BeFalse();

        // falls through to the ordinary single-row path, so the row is still deleted
        await _txRepository.Received(1).DeleteTransactionAsync(outId, Arg.Any<CancellationToken>());
        account.Balance.Amount.Should().Be(600m);
    }
}
