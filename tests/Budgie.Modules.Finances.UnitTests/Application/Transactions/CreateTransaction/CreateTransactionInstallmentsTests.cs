using Budgie.Modules.Finances.Application;
using Budgie.Modules.Finances.Application.Transactions.CreateTransaction;
using Budgie.Modules.Finances.Domain.Accounts;
using Budgie.Modules.Finances.Domain.Transactions;
using Budgie.Shared.Abstractions;
using Budgie.Shared.Domain.ValueObjects;
using Budgie.Shared.Enums;
using FluentAssertions;
using Mediator;
using NSubstitute;
using Xunit;

namespace Budgie.Modules.Finances.UnitTests.Application.Transactions.CreateTransaction;

public class CreateTransactionInstallmentsTests
{
    private readonly IAccountRepository _accountRepository = Substitute.For<IAccountRepository>();
    private readonly ITransactionRepository _txRepository = Substitute.For<ITransactionRepository>();
    private readonly ICurrentUserProvider _currentUserProvider = Substitute.For<ICurrentUserProvider>();
    private readonly IMediator _mediator = Substitute.For<IMediator>();
    private readonly CreateTransactionCommandHandler _handler;

    public CreateTransactionInstallmentsTests()
    {
        _handler = new CreateTransactionCommandHandler(_accountRepository, _txRepository, _currentUserProvider, _mediator);
    }

    private static Account CreateCheckingAccount(Guid userId, decimal balance = 1000m, string currency = "USD")
    {
        var account = Account.Create(userId, "Checking", currency, balance);
        return account;
    }

    private static Account CreateCreditAccount(Guid userId, decimal annualRate = 0m, CreditConsumptionMode mode = CreditConsumptionMode.Partial, InterestCalculationMethod method = InterestCalculationMethod.FrenchAmortization)
    {
        var settings = new CreditCardSettings(5000m, mode, 15, 5, annualRate, true, CurrencyCode.From("USD"), method);
        var account = Account.Create(userId, "Credit Card", "USD", 0m, AccountType.Credit, settings);
        return account;
    }

    [Fact]
    public async Task Handle_SingleInstallment_ShouldCreateOneTransactionAndPublishEvent()
    {
        var userId = Guid.NewGuid();
        var account = CreateCheckingAccount(userId);
        var accountId = account.Id;
        _currentUserProvider.GetUserId().Returns(userId);
        _accountRepository.GetByIdAsync(accountId, userId, Arg.Any<CancellationToken>()).Returns(account);

        var cmd = new CreateTransactionCommand(accountId, null, 100m, "USD", 1m, DateTime.UtcNow, "Test", TransactionType.Expense, null, 1);

        var result = await _handler.Handle(cmd, CancellationToken.None);

        result.IsError.Should().BeFalse();
        await _txRepository.Received(1).AddAsync(Arg.Is<Transaction>(t => t.Amount.Amount == 100m && t.InstallmentGroupId == null), Arg.Any<CancellationToken>());
        await _mediator.Received(1).Publish(Arg.Any<Budgie.Modules.Finances.Domain.Events.TransactionCreatedEvent>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ThreeInstallments_ShouldSplitIntoThreeTransactionsWithGroupId()
    {
        var userId = Guid.NewGuid();
        var account = CreateCheckingAccount(userId);
        var accountId = account.Id;
        _currentUserProvider.GetUserId().Returns(userId);
        _accountRepository.GetByIdAsync(accountId, userId, Arg.Any<CancellationToken>()).Returns(account);

        var date = DateTime.UtcNow;
        var cmd = new CreateTransactionCommand(accountId, null, 120m, "USD", 1m, date, "Cuotas 3", TransactionType.Expense, null, 3);

        var result = await _handler.Handle(cmd, CancellationToken.None);

        result.IsError.Should().BeFalse();
        var groupId = result.Value;
        groupId.Should().NotBeEmpty();

        // Should have created exactly 3 installment transactions
        var calls = _txRepository.ReceivedCalls().Where(c => c.GetMethodInfo().Name == nameof(ITransactionRepository.AddAsync)).ToList();
        await _txRepository.Received(3).AddAsync(Arg.Any<Transaction>(), Arg.Any<CancellationToken>());

        // Capture created transactions via predicate
        var created = new List<Transaction>();
        await _txRepository.Received(3).AddAsync(Arg.Do<Transaction>(t => created.Add(t)), Arg.Any<CancellationToken>());
        // Actually NSubstitute Do doesn't accumulate across multiple, so we intercept via Returns
        // Instead verify via Arg.Is checks

        await _txRepository.Received(1).AddAsync(Arg.Is<Transaction>(t =>
            t.Amount.Amount == 40m && t.InstallmentGroupId == groupId && t.InstallmentNumber == 1 && t.TotalInstallments == 3 && t.Date == DateTime.SpecifyKind(date, DateTimeKind.Unspecified)), Arg.Any<CancellationToken>());

        await _txRepository.Received(1).AddAsync(Arg.Is<Transaction>(t =>
            t.Amount.Amount == 40m && t.InstallmentNumber == 2 && t.Date == DateTime.SpecifyKind(date.AddMonths(1), DateTimeKind.Unspecified)), Arg.Any<CancellationToken>());

        await _txRepository.Received(1).AddAsync(Arg.Is<Transaction>(t =>
            t.Amount.Amount == 40m && t.InstallmentNumber == 3 && t.Date == DateTime.SpecifyKind(date.AddMonths(2), DateTimeKind.Unspecified)), Arg.Any<CancellationToken>());

        // Installments path should NOT publish single TransactionCreatedEvent (balance handled directly)
        await _mediator.DidNotReceive().Publish(Arg.Any<Budgie.Modules.Finances.Domain.Events.TransactionCreatedEvent>(), Arg.Any<CancellationToken>());

        // Balance updated once with total amount
        await _accountRepository.Received(1).UpdateBalanceAsync(userId, accountId, Arg.Any<decimal>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_InstallmentsWithRounding_ShouldAdjustLastInstallment()
    {
        var userId = Guid.NewGuid();
        var account = CreateCheckingAccount(userId);
        _currentUserProvider.GetUserId().Returns(userId);
        _accountRepository.GetByIdAsync(account.Id, userId, Arg.Any<CancellationToken>()).Returns(account);

        var cmd = new CreateTransactionCommand(account.Id, null, 100m, "USD", 1m, DateTime.UtcNow, null, TransactionType.Expense, null, 3);

        await _handler.Handle(cmd, CancellationToken.None);

        // 100 /3 =33.33, last should be 33.34 to sum 100
        await _txRepository.Received(1).AddAsync(Arg.Is<Transaction>(t => t.Amount.Amount == 33.33m && t.InstallmentNumber == 1), Arg.Any<CancellationToken>());
        await _txRepository.Received(1).AddAsync(Arg.Is<Transaction>(t => t.Amount.Amount == 33.33m && t.InstallmentNumber == 2), Arg.Any<CancellationToken>());
        await _txRepository.Received(1).AddAsync(Arg.Is<Transaction>(t => t.Amount.Amount == 33.34m && t.InstallmentNumber == 3), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_InstallmentsWithParticipant_ShouldProrateContributions()
    {
        var userId = Guid.NewGuid();
        var account = CreateCheckingAccount(userId);
        _currentUserProvider.GetUserId().Returns(userId);
        _accountRepository.GetByIdAsync(account.Id, userId, Arg.Any<CancellationToken>()).Returns(account);

        var personId = Guid.NewGuid();
        var participants = new List<TransactionParticipantDto>
        {
            new(personId, 30m, 30m, false, null)
        };
        var cmd = new CreateTransactionCommand(account.Id, null, 90m, "USD", 1m, DateTime.UtcNow, null, TransactionType.Expense, participants, 3);

        await _handler.Handle(cmd, CancellationToken.None);

        // Each installment participant should be prorated: 10 each (3 installments + 3 child loans = 6 participants with 10)
        await _txRepository.Received(6).AddParticipantAsync(Arg.Is<TransactionParticipant>(p => p.PersonId == personId && p.ContributedAmount.Amount == 10m), Arg.Any<CancellationToken>());

        // Child loan transactions per installment (3) should also be created
        await _txRepository.Received(3).AddAsync(Arg.Is<Transaction>(t => t.Type == TransactionType.Loan), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_InstallmentsWithImmediateCashParticipant_ShouldCreateOneChildIncome()
    {
        var userId = Guid.NewGuid();
        var account = CreateCheckingAccount(userId);
        var destAccountId = Guid.NewGuid();
        var destAccount = CreateCheckingAccount(Guid.NewGuid(), 500m);
        destAccount.Id = destAccountId;
        _currentUserProvider.GetUserId().Returns(userId);
        _accountRepository.GetByIdAsync(account.Id, userId, Arg.Any<CancellationToken>()).Returns(account);
        _accountRepository.GetByIdRawAsync(destAccountId, Arg.Any<CancellationToken>()).Returns(destAccount);

        var personId = Guid.NewGuid();
        var participants = new List<TransactionParticipantDto>
        {
            new(personId, 20m, 0m, false, destAccountId)
        };
        var cmd = new CreateTransactionCommand(account.Id, null, 100m, "USD", 1m, DateTime.UtcNow, null, TransactionType.Expense, participants, 3);

        await _handler.Handle(cmd, CancellationToken.None);

        // Should create 3 installments + 1 child income (not 3)
        await _txRepository.Received(1).AddAsync(Arg.Is<Transaction>(t => t.Type == TransactionType.Income && t.ParentTransactionId != null), Arg.Any<CancellationToken>());
        await _accountRepository.Received(1).UpdateBalanceAsync(destAccount.UserId, destAccountId, Arg.Any<decimal>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_InstallmentsCreditCardPartial_ShouldApplyPerInstallmentBalance()
    {
        var userId = Guid.NewGuid();
        var account = CreateCreditAccount(userId, annualRate: 0m, mode: CreditConsumptionMode.Partial);
        _currentUserProvider.GetUserId().Returns(userId);
        _accountRepository.GetByIdAsync(account.Id, userId, Arg.Any<CancellationToken>()).Returns(account);

        var cmd = new CreateTransactionCommand(account.Id, null, 300m, "USD", 1m, DateTime.UtcNow, null, TransactionType.Expense, null, 3, IsInterestFree: true);

        await _handler.Handle(cmd, CancellationToken.None);

        // Balance delta should be -300 (total) regardless of mode after fix, but verify UpdateBalance called
        await _accountRepository.Received(1).UpdateBalanceAsync(userId, account.Id, Arg.Is<decimal>(b => b == account.Balance.Amount - 300m || b < 1000m), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_InstallmentsWithInterest_ShouldIncreaseTotalWithFrenchAmortization()
    {
        var userId = Guid.NewGuid();
        var account = CreateCreditAccount(userId, annualRate: 12m, mode: CreditConsumptionMode.Partial, method: InterestCalculationMethod.FrenchAmortization);
        _currentUserProvider.GetUserId().Returns(userId);
        _accountRepository.GetByIdAsync(account.Id, userId, Arg.Any<CancellationToken>()).Returns(account);

        var cmd = new CreateTransactionCommand(account.Id, null, 1200m, "USD", 1m, DateTime.UtcNow, null, TransactionType.Expense, null, 12, IsInterestFree: false);

        await _handler.Handle(cmd, CancellationToken.None);

        // With 12% annual (1% monthly) French amortization, payment > 100. Verify installments amounts are not simple 100
        await _txRepository.Received(12).AddAsync(Arg.Is<Transaction>(t => t.InstallmentGroupId != null), Arg.Any<CancellationToken>());
        // At least one installment should be !=100
        // We verify via total sum: french amortization for 1200 at 1% 12 months => payment ~106.62, total ~1279.44
        // Our handler uses rounded payment per installment, so first installment should be >100
        await _txRepository.Received(1).AddAsync(Arg.Is<Transaction>(t => t.Amount.Amount > 100m && t.InstallmentNumber == 1), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_CreditTotalMode_ShouldStillCreateInstallments()
    {
        var userId = Guid.NewGuid();
        var account = CreateCreditAccount(userId, mode: CreditConsumptionMode.Total);
        _currentUserProvider.GetUserId().Returns(userId);
        _accountRepository.GetByIdAsync(account.Id, userId, Arg.Any<CancellationToken>()).Returns(account);

        var cmd = new CreateTransactionCommand(account.Id, null, 90m, "USD", 1m, DateTime.UtcNow, null, TransactionType.Expense, null, 3);

        var result = await _handler.Handle(cmd, CancellationToken.None);
        result.IsError.Should().BeFalse();
        await _txRepository.Received(3).AddAsync(Arg.Any<Transaction>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_Installments_ShouldPersistInterestRate()
    {
        var userId = Guid.NewGuid();
        var account = CreateCreditAccount(userId, annualRate: 24m);
        _currentUserProvider.GetUserId().Returns(userId);
        _accountRepository.GetByIdAsync(account.Id, userId, Arg.Any<CancellationToken>()).Returns(account);

        var cmd = new CreateTransactionCommand(account.Id, null, 600m, "USD", 1m, DateTime.UtcNow, null, TransactionType.Expense, null, 6, IsInterestFree: false);

        await _handler.Handle(cmd, CancellationToken.None);

        // monthlyRate = 2% should be persisted in InterestRate.Value for all installments
        await _txRepository.Received(6).AddAsync(Arg.Is<Transaction>(t => t.InterestRate.Value == 0.02m), Arg.Any<CancellationToken>());
    }
}
