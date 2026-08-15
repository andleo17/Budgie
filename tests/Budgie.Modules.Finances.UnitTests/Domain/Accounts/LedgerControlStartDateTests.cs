using Budgie.Modules.Finances.Domain.Accounts;
using Budgie.Modules.Finances.Domain.Categories;
using Budgie.Modules.Finances.Domain.Transactions;
using Budgie.Shared.Domain.ValueObjects;
using Budgie.Shared.Enums;
using FluentAssertions;

namespace Budgie.Modules.Finances.UnitTests.Domain.Accounts;

public class LedgerControlStartDateTests
{
    [Fact]
    public void Create_WithoutControlStartDate_DefaultsToCreationDate()
    {
        var account = Account.Create(Guid.NewGuid(), "Checking", "USD", 0m);

        account.ControlStartDate.Should().Be(account.CreatedAt.Date);
    }

    [Fact]
    public void Create_WithControlStartDate_UsesProvidedDate()
    {
        var controlStartDate = new DateTime(2026, 1, 1);

        var account = Account.Create(Guid.NewGuid(), "Checking", "USD", 0m,
            AccountType.Checking, null, null, controlStartDate);

        account.ControlStartDate.Should().Be(controlStartDate.Date);
    }

    [Fact]
    public void CanAcceptTransactionOn_DateAtOrAfterControlStart_ReturnsTrue()
    {
        var controlStartDate = new DateTime(2026, 1, 15);
        var account = Account.Create(Guid.NewGuid(), "Checking", "USD", 0m,
            AccountType.Checking, null, null, controlStartDate);

        account.CanAcceptTransactionOn(new DateTime(2026, 1, 15)).Should().BeTrue();
        account.CanAcceptTransactionOn(new DateTime(2026, 2, 1)).Should().BeTrue();
    }

    [Fact]
    public void CanAcceptTransactionOn_DateBeforeControlStart_ReturnsFalse()
    {
        var controlStartDate = new DateTime(2026, 1, 15);
        var account = Account.Create(Guid.NewGuid(), "Checking", "USD", 0m,
            AccountType.Checking, null, null, controlStartDate);

        account.CanAcceptTransactionOn(new DateTime(2026, 1, 14)).Should().BeFalse();
    }

    [Fact]
    public void Update_WithNewControlStartDate_SetsIt()
    {
        var account = Account.Create(Guid.NewGuid(), "Checking", "USD", 0m);
        var newDate = new DateTime(2026, 3, 1);

        account.Update("Checking", null, newDate);

        account.ControlStartDate.Should().Be(newDate.Date);
    }

    [Fact]
    public void InitialBalanceTransaction_IsRecordedOnControlStartDate()
    {
        var controlStartDate = new DateTime(2026, 1, 1);
        var account = Account.Create(Guid.NewGuid(), "Checking", "USD", 0m,
            AccountType.Checking, null, null, controlStartDate);

        var tx = Transaction.Create(account.Id, account.Id, SystemCategoryIds.InitialBalance,
            500m, "USD", 1m, account.ControlStartDate, null, TransactionType.Income,
            isInitialBalance: true);

        tx.Date.Should().Be(controlStartDate.Date);
        tx.IsInitialBalance.Should().BeTrue();
        tx.Type.Should().Be(TransactionType.Income);
    }
}
