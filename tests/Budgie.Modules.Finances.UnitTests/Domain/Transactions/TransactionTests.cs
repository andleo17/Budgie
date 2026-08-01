using Budgie.Modules.Finances.Domain.Transactions;
using Budgie.Shared.Domain.ValueObjects;
using Budgie.Shared.Enums;
using FluentAssertions;
using Xunit;

namespace Budgie.Modules.Finances.UnitTests.Domain.Transactions;

public class TransactionTests
{
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid AccountId = Guid.NewGuid();

    [Fact]
    public void Create_ShouldSetAllProperties()
    {
        var date = new DateTime(2025, 6, 15, 0, 0, 0, DateTimeKind.Utc);
        var transaction = Transaction.Create(UserId, AccountId, null, 100m, "USD", 1m, date, "Test", TransactionType.Expense);

        transaction.Id.Should().NotBeEmpty();
        transaction.UserId.Should().Be(UserId);
        transaction.AccountId.Should().Be(AccountId);
        transaction.CategoryId.Should().BeNull();
        transaction.Amount.Amount.Should().Be(100m);
        transaction.Amount.Currency.Code.Should().Be("USD");
        transaction.ExchangeRate.Should().Be(1m);
        transaction.Notes.Should().Be("Test");
        transaction.Type.Should().Be(TransactionType.Expense);
        transaction.Installments.Should().Be(1);
        transaction.IsInitialBalance.Should().BeFalse();
        transaction.InstallmentNumber.Should().Be(1);
        transaction.TotalInstallments.Should().Be(1);
        transaction.InterestRate.Value.Should().Be(0);
    }

    [Fact]
    public void Create_ShouldWorkWithPositiveAmount()
    {
        var transaction = Transaction.Create(UserId, AccountId, null, 250m, "USD", 1m, DateTime.UtcNow, null, TransactionType.Income);
        transaction.Amount.Amount.Should().Be(250m);
    }

    [Fact]
    public void Create_ShouldThrow_WhenAmountIsZero()
    {
        var act = () => Transaction.Create(UserId, AccountId, null, 0m, "USD", 1m, DateTime.UtcNow, null, TransactionType.Income);
        act.Should().Throw<ArgumentException>().WithMessage("Amount must be positive.*");
    }

    [Fact]
    public void Create_ShouldThrow_WhenAmountIsNegative()
    {
        var act = () => Transaction.Create(UserId, AccountId, null, -50m, "USD", 1m, DateTime.UtcNow, null, TransactionType.Income);
        act.Should().Throw<ArgumentException>().WithMessage("Amount must be positive.*");
    }

    [Fact]
    public void Create_ShouldDefaultExchangeRateToOne_WhenZero()
    {
        var transaction = Transaction.Create(UserId, AccountId, null, 100m, "USD", 0m, DateTime.UtcNow, null, TransactionType.Income);
        transaction.ExchangeRate.Should().Be(1m);
    }

    [Fact]
    public void Create_ShouldDefaultExchangeRateToOne_WhenNegative()
    {
        var transaction = Transaction.Create(UserId, AccountId, null, 100m, "USD", -5m, DateTime.UtcNow, null, TransactionType.Income);
        transaction.ExchangeRate.Should().Be(1m);
    }

    [Fact]
    public void Create_ShouldAutoUppercaseCurrency()
    {
        var transaction = Transaction.Create(UserId, AccountId, null, 100m, "usd", 1m, DateTime.UtcNow, null, TransactionType.Income);
        transaction.Amount.Currency.Code.Should().Be("USD");
    }

    [Fact]
    public void Create_ShouldTrimNotes()
    {
        var transaction = Transaction.Create(UserId, AccountId, null, 100m, "USD", 1m, DateTime.UtcNow, "  Test Notes  ", TransactionType.Income);
        transaction.Notes.Should().Be("Test Notes");
    }

    [Fact]
    public void Create_ShouldNormalizeDate()
    {
        var date = DateTime.UtcNow;
        var transaction = Transaction.Create(UserId, AccountId, null, 100m, "USD", 1m, date, null, TransactionType.Income);
        transaction.Date.Kind.Should().Be(DateTimeKind.Unspecified);
    }

    [Fact]
    public void Update_ShouldUpdateCoreFields()
    {
        var transaction = Transaction.Create(UserId, AccountId, null, 100m, "USD", 1m, DateTime.UtcNow, "Old", TransactionType.Income);
        var newDate = new DateTime(2025, 7, 1, 0, 0, 0, DateTimeKind.Utc);

        transaction.Update(Guid.NewGuid(), 200m, "EUR", 1.2m, newDate, "Updated", TransactionType.Expense, 2);

        transaction.CategoryId.Should().NotBeNull();
        transaction.Amount.Amount.Should().Be(200m);
        transaction.Amount.Currency.Code.Should().Be("EUR");
        transaction.ExchangeRate.Should().Be(1.2m);
        transaction.Notes.Should().Be("Updated");
        transaction.Type.Should().Be(TransactionType.Expense);
        transaction.Installments.Should().Be(2);
    }

    [Fact]
    public void Update_ShouldThrow_WhenInstallmentsIsInvalid()
    {
        var transaction = Transaction.Create(UserId, AccountId, null, 100m, "USD", 1m, DateTime.UtcNow, null, TransactionType.Income);
        var act = () => transaction.Update(null, 100m, "USD", 1m, DateTime.UtcNow, null, TransactionType.Income, 0);
        act.Should().Throw<ArgumentException>().WithMessage("installments must be positive.*");
    }

    [Fact]
    public void CreateParticipant_ShouldCreateAndLinkParticipant()
    {
        var transaction = Transaction.Create(UserId, AccountId, null, 100m, "USD", 1m, DateTime.UtcNow, null, TransactionType.Expense);
        var personId = Guid.NewGuid();

        var participant = transaction.CreateParticipant(personId, 50m);

        participant.Should().NotBeNull();
        participant.PersonId.Should().Be(personId);
        participant.ContributedAmount.Amount.Should().Be(50m);
        participant.ContributedAmount.Currency.Code.Should().Be("USD");
        participant.TransactionId.Should().Be(transaction.Id);
        transaction.Participants.Should().Contain(participant);
    }

    [Fact]
    public void CreateParticipant_ShouldThrow_WhenContributedAmountIsNegative()
    {
        var transaction = Transaction.Create(UserId, AccountId, null, 100m, "USD", 1m, DateTime.UtcNow, null, TransactionType.Expense);
        var act = () => transaction.CreateParticipant(Guid.NewGuid(), -10m);
        act.Should().Throw<ArgumentException>().WithMessage("Amount cannot be negative.*");
    }

    [Fact]
    public void ClearParticipants_ShouldRemoveAllAndReturnOldList()
    {
        var transaction = Transaction.Create(UserId, AccountId, null, 100m, "USD", 1m, DateTime.UtcNow, null, TransactionType.Expense);
        transaction.CreateParticipant(Guid.NewGuid(), 30m);
        transaction.CreateParticipant(Guid.NewGuid(), 70m);

        var old = transaction.ClearParticipants();

        old.Should().HaveCount(2);
        transaction.Participants.Should().BeEmpty();
    }
}

public class TransactionParticipantTests
{
    [Fact]
    public void Create_ShouldSetProperties()
    {
        var participant = TransactionParticipant.Create(Guid.NewGuid(), 100m, CurrencyCode.From("USD"), 50m);

        participant.Id.Should().NotBeEmpty();
        participant.ContributedAmount.Amount.Should().Be(100m);
        participant.ContributedAmount.Currency.Code.Should().Be("USD");
        participant.RemainingAmount.Amount.Should().Be(50m);
        participant.IsTrackingOnly.Should().BeFalse();
        participant.DestinationAccountId.Should().BeNull();
    }

    [Fact]
    public void Create_ShouldAllowZeroContributedAmount()
    {
        var participant = TransactionParticipant.Create(Guid.NewGuid(), 0m, CurrencyCode.From("USD"));
        participant.ContributedAmount.Amount.Should().Be(0);
    }

    [Fact]
    public void Create_ShouldThrow_WhenContributedAmountIsNegative()
    {
        var act = () => TransactionParticipant.Create(Guid.NewGuid(), -10m, CurrencyCode.From("USD"));
        act.Should().Throw<ArgumentException>().WithMessage("Amount cannot be negative.*");
    }

    [Fact]
    public void Create_ShouldAllowZeroRemainingAmount()
    {
        var participant = TransactionParticipant.Create(Guid.NewGuid(), 100m, CurrencyCode.From("USD"), 0m);
        participant.RemainingAmount.Amount.Should().Be(0);
    }

    [Fact]
    public void Create_ShouldThrow_WhenRemainingAmountIsNegative()
    {
        var act = () => TransactionParticipant.Create(Guid.NewGuid(), 100m, CurrencyCode.From("USD"), -5m);
        act.Should().Throw<ArgumentException>().WithMessage("Amount cannot be negative.*");
    }

    [Fact]
    public void ReduceRemaining_ShouldReduceByCorrectAmount()
    {
        var participant = TransactionParticipant.Create(Guid.NewGuid(), 100m, CurrencyCode.From("USD"), 80m);
        participant.ReduceRemaining(30m);
        participant.RemainingAmount.Amount.Should().Be(50m);
    }

    [Fact]
    public void ReduceRemaining_ShouldReduceToZero()
    {
        var participant = TransactionParticipant.Create(Guid.NewGuid(), 100m, CurrencyCode.From("USD"), 50m);
        participant.ReduceRemaining(50m);
        participant.RemainingAmount.Amount.Should().Be(0);
    }

    [Fact]
    public void ReduceRemaining_ShouldThrow_WhenAmountExceedsRemaining()
    {
        var participant = TransactionParticipant.Create(Guid.NewGuid(), 100m, CurrencyCode.From("USD"), 30m);
        var act = () => participant.ReduceRemaining(50m);
        act.Should().Throw<ArgumentException>().WithMessage("Amount exceeds remaining amount.*");
    }
}
