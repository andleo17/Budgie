using Budgie.Modules.Finances.Application;
using Budgie.Modules.Finances.Application.Transactions.UpdateTransaction;
using Budgie.Modules.Finances.Domain.Accounts;
using Budgie.Modules.Finances.Domain.Transactions;
using Budgie.Shared.Abstractions;
using Budgie.Shared.Enums;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace Budgie.Modules.Finances.UnitTests.Application.Transactions.UpdateTransaction;

public class UpdateTransactionInstallmentsTests
{
    private readonly ITransactionRepository _txRepository = Substitute.For<ITransactionRepository>();
    private readonly IAccountRepository _accountRepository = Substitute.For<IAccountRepository>();
    private readonly ICurrentUserProvider _currentUserProvider = Substitute.For<ICurrentUserProvider>();
    private readonly UpdateTransactionCommandHandler _handler;

    public UpdateTransactionInstallmentsTests()
    {
        _handler = new UpdateTransactionCommandHandler(_txRepository, _accountRepository, _currentUserProvider);
        _txRepository.GetChildTransactionsForDeleteAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(new List<TransactionInfo>());
    }

    private static Account CreateCheckingAccount(Guid userId, decimal balance = 1000m)
        => Account.Create(userId, "Checking", "USD", balance);

    [Fact]
    public async Task Handle_UpdateInstallment_ShouldPreserveGroupReferenceAndMetadata()
    {
        var userId = Guid.NewGuid();
        var account = CreateCheckingAccount(userId);
        var txId = Guid.NewGuid();
        var groupId = Guid.NewGuid();

        _currentUserProvider.GetUserId().Returns(userId);
        _accountRepository.GetByIdAsync(account.Id, userId, Arg.Any<CancellationToken>()).Returns(account);
        _txRepository.GetByIdForUpdateAsync(txId, userId, Arg.Any<CancellationToken>())
            .Returns(new TransactionForUpdate(txId, TransactionType.Expense, false,
                40m, "USD", 1m, 1, account.Id, null, DateTime.UtcNow,
                groupId, 2, 3, 0.02m));

        var cmd = new UpdateTransactionCommand(txId, null, 45m, "USD", 1m, DateTime.UtcNow,
            "Ajuste cuota", TransactionType.Expense, 1);

        var result = await _handler.Handle(cmd, CancellationToken.None);

        result.IsError.Should().BeFalse();
        await _txRepository.Received(1).UpdateAsync(Arg.Is<Transaction>(t =>
            t.Id == txId &&
            t.Amount.Amount == 45m &&
            t.InstallmentGroupId == groupId &&
            t.InstallmentNumber == 2 &&
            t.TotalInstallments == 3 &&
            t.InterestRate.Value == 0.02m), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_UpdateSingleInstallment_ShouldKeepNullGroupAndDefaults()
    {
        var userId = Guid.NewGuid();
        var account = CreateCheckingAccount(userId);
        var txId = Guid.NewGuid();

        _currentUserProvider.GetUserId().Returns(userId);
        _accountRepository.GetByIdAsync(account.Id, userId, Arg.Any<CancellationToken>()).Returns(account);
        _txRepository.GetByIdForUpdateAsync(txId, userId, Arg.Any<CancellationToken>())
            .Returns(new TransactionForUpdate(txId, TransactionType.Expense, false,
                40m, "USD", 1m, 1, account.Id, null, DateTime.UtcNow));

        var cmd = new UpdateTransactionCommand(txId, null, 55m, "USD", 1m, DateTime.UtcNow,
            "Edición simple", TransactionType.Expense, 1);

        var result = await _handler.Handle(cmd, CancellationToken.None);

        result.IsError.Should().BeFalse();
        await _txRepository.Received(1).UpdateAsync(Arg.Is<Transaction>(t =>
            t.Id == txId &&
            t.Amount.Amount == 55m &&
            t.InstallmentGroupId == null &&
            t.InstallmentNumber == 1 &&
            t.TotalInstallments == 1 &&
            t.InterestRate.Value == 0m), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_UpdateInstallment_ShouldKeepPerInstallmentInterestRate()
    {
        var userId = Guid.NewGuid();
        var account = CreateCheckingAccount(userId);
        var txId = Guid.NewGuid();
        var groupId = Guid.NewGuid();

        _currentUserProvider.GetUserId().Returns(userId);
        _accountRepository.GetByIdAsync(account.Id, userId, Arg.Any<CancellationToken>()).Returns(account);
        _txRepository.GetByIdForUpdateAsync(txId, userId, Arg.Any<CancellationToken>())
            .Returns(new TransactionForUpdate(txId, TransactionType.Expense, false,
                106.62m, "USD", 1m, 1, account.Id, null, DateTime.UtcNow,
                groupId, 1, 12, 0.01m));

        var cmd = new UpdateTransactionCommand(txId, null, 120m, "USD", 1m, DateTime.UtcNow,
            "Ajuste con interés", TransactionType.Expense, 1);

        await _handler.Handle(cmd, CancellationToken.None);

        await _txRepository.Received(1).UpdateAsync(Arg.Is<Transaction>(t =>
            t.InstallmentGroupId == groupId &&
            t.InstallmentNumber == 1 &&
            t.TotalInstallments == 12 &&
            t.InterestRate.Value == 0.01m), Arg.Any<CancellationToken>());
    }
}
