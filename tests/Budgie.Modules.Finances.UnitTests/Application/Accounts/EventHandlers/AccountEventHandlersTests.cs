using Budgie.Modules.Finances.Application;
using Budgie.Modules.Finances.Application.Accounts.EventHandlers;
using Budgie.Modules.Finances.Application.Debts.SettleDebt.EventHandlers;
using Budgie.Modules.Finances.Domain.Accounts;
using Budgie.Modules.Finances.Domain.Events;
using Budgie.Modules.Finances.Domain.Transactions;
using Budgie.Shared.Enums;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace Budgie.Modules.Finances.UnitTests.Application.Accounts.EventHandlers;

public class RecordInitialBalanceOnAccountCreatedTests
{
    private readonly ITransactionRepository _txRepository = Substitute.For<ITransactionRepository>();
    private readonly IAccountRepository _accountRepository = Substitute.For<IAccountRepository>();
    private readonly RecordInitialBalanceOnAccountCreated _handler;

    public RecordInitialBalanceOnAccountCreatedTests()
    {
        _handler = new RecordInitialBalanceOnAccountCreated(_txRepository, _accountRepository);
    }

    [Fact]
    public async Task Handle_PositiveInitialBalance_CreatesIncomeTransactionAndUpdatesBalance()
    {
        var accountId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var controlDate = new DateTime(2026, 1, 1);
        var account = Account.Create(userId, "Checking", "USD", 0m);
        account.Id = accountId;
        _accountRepository.GetByIdRawAsync(accountId, Arg.Any<CancellationToken>()).Returns(account);

        var notification = new AccountCreatedEvent(accountId, userId, "Checking",
            1000m, null, "USD", AccountType.Checking, controlDate);

        await _handler.Handle(notification, CancellationToken.None);

        await _txRepository.Received(1).AddAsync(Arg.Is<Transaction>(t =>
            t.Type == TransactionType.Income && t.IsInitialBalance &&
            t.Date == controlDate.Date), Arg.Any<CancellationToken>());
        await _accountRepository.Received(1).UpdateBalanceAsync(userId, accountId, 1000m, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ZeroInitialBalance_DoesNotCreateTransaction()
    {
        var notification = new AccountCreatedEvent(Guid.NewGuid(), Guid.NewGuid(), "Checking",
            0m, null, "USD", AccountType.Checking, DateTime.UtcNow);

        await _handler.Handle(notification, CancellationToken.None);

        await _txRepository.DidNotReceive().AddAsync(Arg.Any<Transaction>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_CreditAccountWithLimit_CreatesLimitIncome()
    {
        var accountId = Guid.NewGuid();
        var account = Account.Create(Guid.NewGuid(), "Credit", "USD", 0m);
        _accountRepository.GetByIdRawAsync(accountId, Arg.Any<CancellationToken>()).Returns(account);

        var notification = new AccountCreatedEvent(accountId, Guid.NewGuid(), "Credit",
            0m, 5000m, "USD", AccountType.Credit, DateTime.UtcNow);

        await _handler.Handle(notification, CancellationToken.None);

        await _txRepository.Received(1).AddAsync(Arg.Is<Transaction>(t =>
            t.Type == TransactionType.Income && t.Amount.Amount == 5000m), Arg.Any<CancellationToken>());
    }
}

public class AdjustAccountBalanceOnDebtSettledTests
{
    private readonly IAccountRepository _accountRepository = Substitute.For<IAccountRepository>();
    private readonly AdjustAccountBalanceOnDebtSettled _handler;

    public AdjustAccountBalanceOnDebtSettledTests()
    {
        _handler = new AdjustAccountBalanceOnDebtSettled(_accountRepository);
    }

    [Fact]
    public async Task Handle_AddsSettlementAmountToDestinationAccount()
    {
        var accountId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var account = Account.Create(userId, "Cash", "USD", 500m);
        account.Id = accountId;
        _accountRepository.GetByIdRawAsync(accountId, Arg.Any<CancellationToken>()).Returns(account);

        var notification = new DebtSettledEvent(
            Guid.NewGuid(), Guid.NewGuid(), accountId, Guid.NewGuid(),
            40m, "USD", DateTime.UtcNow);

        await _handler.Handle(notification, CancellationToken.None);

        await _accountRepository.Received(1).UpdateBalanceAsync(userId, accountId, 540m, Arg.Any<CancellationToken>());
    }
}
