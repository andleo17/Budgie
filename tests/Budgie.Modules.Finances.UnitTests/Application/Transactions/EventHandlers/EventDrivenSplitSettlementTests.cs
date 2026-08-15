using Budgie.Modules.Finances.Application;
using Budgie.Modules.Finances.Application.Transactions.CreateTransaction;
using Budgie.Modules.Finances.Application.Transactions.EventHandlers;
using Budgie.Modules.Finances.Domain.Accounts;
using Budgie.Modules.Finances.Domain.Events;
using Budgie.Modules.Finances.Domain.Transactions;
using Budgie.Shared.Domain.ValueObjects;
using Budgie.Shared.Enums;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace Budgie.Modules.Finances.UnitTests.Application.Transactions.EventHandlers;

public class RecordSplitDebtsOnTransactionCreatedTests
{
    private readonly ITransactionRepository _txRepository = Substitute.For<ITransactionRepository>();
    private readonly IAccountRepository _accountRepository = Substitute.For<IAccountRepository>();
    private readonly RecordSplitDebtsOnTransactionCreated _handler;

    public RecordSplitDebtsOnTransactionCreatedTests()
    {
        _handler = new RecordSplitDebtsOnTransactionCreated(_txRepository, _accountRepository);
    }

    [Fact]
    public async Task Handle_DeferredParticipant_CreatesChildLoanTransaction()
    {
        var parentTxId = Guid.NewGuid();
        var personId = Guid.NewGuid();
        var notification = new TransactionCreatedEvent(
            parentTxId, Guid.NewGuid(), Guid.NewGuid(), null,
            100m, "USD", 1m, DateTime.UtcNow, TransactionType.Expense,
            [new TransactionParticipantDto(personId, 30m, 30m, false, null)],
            1, null, false);

        await _handler.Handle(notification, CancellationToken.None);

        await _txRepository.Received(1).AddAsync(Arg.Is<Transaction>(t =>
            t.Type == TransactionType.Loan && t.ParentTransactionId == parentTxId), Arg.Any<CancellationToken>());
        await _txRepository.Received(1).AddParticipantAsync(Arg.Is<TransactionParticipant>(p =>
            p.PersonId == personId && p.RemainingAmount.Amount == 30m), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ImmediateSettlement_CreatesChildIncomeOnDestinationAccount()
    {
        var parentTxId = Guid.NewGuid();
        var personId = Guid.NewGuid();
        var sourceAccountId = Guid.NewGuid();
        var destAccountId = Guid.NewGuid();

        var destAccount = Account.Create(Guid.NewGuid(), "Cash", "USD", 500m);
        destAccount.Id = destAccountId;
        _accountRepository.GetByIdRawAsync(destAccountId, Arg.Any<CancellationToken>()).Returns(destAccount);

        var notification = new TransactionCreatedEvent(
            parentTxId, Guid.NewGuid(), sourceAccountId, null,
            100m, "USD", 1m, DateTime.UtcNow, TransactionType.Expense,
            [new TransactionParticipantDto(personId, 30m, 0m, false, destAccountId)],
            1, null, false);

        await _handler.Handle(notification, CancellationToken.None);

        await _txRepository.Received(1).AddAsync(Arg.Is<Transaction>(t =>
            t.Type == TransactionType.Income && t.AccountId == destAccountId && t.ParentTransactionId == parentTxId), Arg.Any<CancellationToken>());
        await _accountRepository.Received(1).UpdateBalanceAsync(destAccountId, 530m, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_NoParticipants_DoesNothing()
    {
        var notification = new TransactionCreatedEvent(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), null,
            100m, "USD", 1m, DateTime.UtcNow, TransactionType.Expense,
            null, 1, null, false);

        await _handler.Handle(notification, CancellationToken.None);

        await _txRepository.DidNotReceive().AddAsync(Arg.Any<Transaction>(), Arg.Any<CancellationToken>());
    }
}

public class UpdateAccountBalanceOnTransactionCreatedTests
{
    private readonly IAccountRepository _accountRepository = Substitute.For<IAccountRepository>();
    private readonly UpdateAccountBalanceOnTransactionCreated _handler;

    public UpdateAccountBalanceOnTransactionCreatedTests()
    {
        _handler = new UpdateAccountBalanceOnTransactionCreated(_accountRepository);
    }

    [Fact]
    public async Task Handle_Expense_DeductsAmountFromBalance()
    {
        var accountId = Guid.NewGuid();
        var account = Account.Create(Guid.NewGuid(), "Checking", "USD", 1000m);
        _accountRepository.GetByIdRawAsync(accountId, Arg.Any<CancellationToken>()).Returns(account);

        var notification = new TransactionCreatedEvent(
            Guid.NewGuid(), Guid.NewGuid(), accountId, null,
            50m, "USD", 1m, DateTime.UtcNow, TransactionType.Expense,
            null, 1, null, false);

        await _handler.Handle(notification, CancellationToken.None);

        await _accountRepository.Received(1).UpdateBalanceAsync(accountId, 950m, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_Income_AddsAmountToBalance()
    {
        var accountId = Guid.NewGuid();
        var account = Account.Create(Guid.NewGuid(), "Checking", "USD", 1000m);
        _accountRepository.GetByIdRawAsync(accountId, Arg.Any<CancellationToken>()).Returns(account);

        var notification = new TransactionCreatedEvent(
            Guid.NewGuid(), Guid.NewGuid(), accountId, null,
            200m, "USD", 1m, DateTime.UtcNow, TransactionType.Income,
            null, 1, null, false);

        await _handler.Handle(notification, CancellationToken.None);

        await _accountRepository.Received(1).UpdateBalanceAsync(accountId, 1200m, Arg.Any<CancellationToken>());
    }
}
