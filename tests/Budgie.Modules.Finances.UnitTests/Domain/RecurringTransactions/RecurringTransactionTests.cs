using Budgie.Modules.Finances.Domain.RecurringTransactions;
using Budgie.Shared.Domain.ValueObjects;
using Budgie.Shared.Enums;
using FluentAssertions;
using Xunit;

namespace Budgie.Modules.Finances.UnitTests.Domain.RecurringTransactions;

public class RecurringTransactionTests
{
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid AccountId = Guid.NewGuid();

    private static readonly DateTime SampleDate = new(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    private static RecurringTransaction CreateValid(RecurringApplicability applicability = RecurringApplicability.Automatic, RecurringFrequency? frequency = RecurringFrequency.Monthly)
    {
        return RecurringTransaction.Create(
            UserId, "Rent", AccountId, null,
            1200m, "USD", 1m, TransactionType.Expense, "Monthly rent", 1, true,
            applicability, frequency, null, RecurringEndType.Never, null, SampleDate, SampleDate);
    }

    [Fact]
    public void Create_ShouldCreateWithAllRequiredFields()
    {
        var startDate = SampleDate;

        var rt = RecurringTransaction.Create(
            UserId, "Rent", AccountId, Guid.NewGuid(),
            1200m, "USD", 1m, TransactionType.Expense, "Monthly rent", 1, true,
            RecurringApplicability.Automatic, RecurringFrequency.Monthly, null,
            RecurringEndType.AfterCount, 12, SampleDate, startDate);

        rt.Id.Should().NotBeEmpty();
        rt.UserId.Should().Be(UserId);
        rt.Name.Value.Should().Be("Rent");
        rt.AccountId.Should().Be(AccountId);
        rt.CategoryId.Should().NotBeNull();
        rt.Amount.Amount.Should().Be(1200m);
        rt.Amount.Currency.Code.Should().Be("USD");
        rt.ExchangeRate.Should().Be(1m);
        rt.Type.Should().Be(TransactionType.Expense);
        rt.Notes.Should().Be("Monthly rent");
        rt.Installments.Should().Be(1);
        rt.IsInterestFree.Should().BeTrue();
        rt.Applicability.Should().Be(RecurringApplicability.Automatic);
        rt.Frequency.Should().Be(RecurringFrequency.Monthly);
        rt.EndType.Should().Be(RecurringEndType.AfterCount);
        rt.EndAfterCount.Should().Be(12);
        rt.EndDate.Should().Be(SampleDate);
        rt.StartDate.Should().Be(startDate);
        rt.NextDueDate.Should().Be(startDate);
        rt.TimesApplied.Should().Be(0);
        rt.LastAppliedDate.Should().BeNull();
        rt.IsActive.Should().BeTrue();
    }

    [Fact]
    public void Create_ShouldNotRequireFrequency_WhenTemplate()
    {
        var rt = RecurringTransaction.Create(
            UserId, "Template", AccountId, null,
            100m, "USD", 1m, TransactionType.Expense, null, 1, true,
            RecurringApplicability.Template, null, null, null, null, null, null);

        rt.Applicability.Should().Be(RecurringApplicability.Template);
        rt.Frequency.Should().BeNull();
        rt.CustomIntervalDays.Should().BeNull();
        rt.EndType.Should().BeNull();
        rt.EndAfterCount.Should().BeNull();
        rt.EndDate.Should().BeNull();
        rt.StartDate.Should().BeNull();
        rt.NextDueDate.Should().BeNull();
    }

    [Fact]
    public void Create_ShouldThrow_WhenNonTemplateAndFrequencyIsNull()
    {
        var act = () => RecurringTransaction.Create(
            UserId, "Rent", AccountId, null,
            1200m, "USD", 1m, TransactionType.Expense, null, 1, true,
            RecurringApplicability.Automatic, null, null, RecurringEndType.Never, null, SampleDate, SampleDate);

        act.Should().Throw<ArgumentException>().WithMessage("Frequency is required when applicability is not Template.*");
    }

    [Fact]
    public void Create_ShouldThrow_WhenInstallmentsNotPositive()
    {
        var act = () => RecurringTransaction.Create(
            UserId, "Rent", AccountId, null,
            1200m, "USD", 1m, TransactionType.Expense, null, 0, true,
            RecurringApplicability.Automatic, RecurringFrequency.Monthly, null,
            RecurringEndType.Never, null, SampleDate, SampleDate);

        act.Should().Throw<ArgumentException>().WithMessage("installments must be positive.*");
    }

    [Fact]
    public void Create_ShouldDefaultExchangeRateToOne_WhenZero()
    {
        var rt = RecurringTransaction.Create(
            UserId, "Rent", AccountId, null,
            1200m, "USD", 0m, TransactionType.Expense, null, 1, true,
            RecurringApplicability.Automatic, RecurringFrequency.Monthly, null,
            RecurringEndType.Never, null, SampleDate, SampleDate);

        rt.ExchangeRate.Should().Be(1m);
    }

    [Fact]
    public void Create_ShouldDefaultExchangeRateToOne_WhenNegative()
    {
        var rt = RecurringTransaction.Create(
            UserId, "Rent", AccountId, null,
            1200m, "USD", -3m, TransactionType.Expense, null, 1, true,
            RecurringApplicability.Automatic, RecurringFrequency.Monthly, null,
            RecurringEndType.Never, null, SampleDate, SampleDate);

        rt.ExchangeRate.Should().Be(1m);
    }

    [Fact]
    public void Create_ShouldAutoUppercaseCurrency()
    {
        var rt = RecurringTransaction.Create(
            UserId, "Rent", AccountId, null,
            1200m, "usd", 1m, TransactionType.Expense, null, 1, true,
            RecurringApplicability.Automatic, RecurringFrequency.Monthly, null,
            RecurringEndType.Never, null, SampleDate, SampleDate);

        rt.Amount.Currency.Code.Should().Be("USD");
    }

    [Fact]
    public void Create_ShouldTrimNameAndNotes()
    {
        var rt = RecurringTransaction.Create(
            UserId, "  Rent  ", AccountId, null,
            1200m, "USD", 1m, TransactionType.Expense, "  Monthly  ", 1, true,
            RecurringApplicability.Automatic, RecurringFrequency.Monthly, null,
            RecurringEndType.Never, null, SampleDate, SampleDate);

        rt.Name.Value.Should().Be("Rent");
        rt.Notes.Should().Be("Monthly");
    }

    [Fact]
    public void Create_ShouldDefaultIsActiveToTrue()
    {
        var rt = CreateValid();
        rt.IsActive.Should().BeTrue();
    }

    [Fact]
    public void Update_ShouldUpdateAllFields()
    {
        var rt = CreateValid();
        var newStartDate = new DateTime(2025, 6, 1, 0, 0, 0, DateTimeKind.Utc);

        rt.Update("New Rent", Guid.NewGuid(), Guid.NewGuid(), 1500m, "EUR", 1.1m, TransactionType.Income, "Updated", 2, false,
                  RecurringApplicability.Manual, RecurringFrequency.Weekly, 7,
                  RecurringEndType.OnDate, null, new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc), newStartDate);

        rt.Name.Value.Should().Be("New Rent");
        rt.Amount.Amount.Should().Be(1500m);
        rt.Amount.Currency.Code.Should().Be("EUR");
        rt.ExchangeRate.Should().Be(1.1m);
        rt.Type.Should().Be(TransactionType.Income);
        rt.Notes.Should().Be("Updated");
        rt.Installments.Should().Be(2);
        rt.IsInterestFree.Should().BeFalse();
        rt.Applicability.Should().Be(RecurringApplicability.Manual);
        rt.Frequency.Should().Be(RecurringFrequency.Weekly);
        rt.EndType.Should().Be(RecurringEndType.OnDate);
    }

    [Fact]
    public void Update_ShouldThrow_WhenNonTemplateAndFrequencyIsNull()
    {
        var rt = CreateValid();
        var act = () => rt.Update("Rent", AccountId, null, 1200m, "USD", 1m, TransactionType.Expense, null, 1, true,
            RecurringApplicability.Automatic, null, null, RecurringEndType.Never, null, SampleDate, SampleDate);

        act.Should().Throw<ArgumentException>().WithMessage("Frequency is required when applicability is not Template.*");
    }

    [Fact]
    public void Apply_ShouldIncrementTimesApplied()
    {
        var rt = CreateValid();
        rt.Apply();
        rt.TimesApplied.Should().Be(1);
    }

    [Fact]
    public void Apply_ShouldSetLastAppliedDate()
    {
        var rt = CreateValid();
        rt.Apply();
        rt.LastAppliedDate.Should().NotBeNull();
        rt.LastAppliedDate!.Value.Kind.Should().Be(DateTimeKind.Unspecified);
    }

    [Fact]
    public void Apply_ShouldUpdateNextDueDate()
    {
        var rt = CreateValid();
        rt.Apply();
        rt.NextDueDate.Should().NotBeNull();
    }

    [Fact]
    public void Apply_ShouldDeactivate_WhenEndTypeIsAfterCountAndMaxReached()
    {
        var rt = RecurringTransaction.Create(
            UserId, "Rent", AccountId, null,
            1200m, "USD", 1m, TransactionType.Expense, null, 1, true,
            RecurringApplicability.Automatic, RecurringFrequency.Monthly, null,
            RecurringEndType.AfterCount, 2, SampleDate, SampleDate);

        rt.Apply();
        rt.IsActive.Should().BeTrue();

        rt.Apply();
        rt.IsActive.Should().BeFalse();
    }

    [Fact]
    public void Apply_ShouldDeactivate_WhenEndTypeIsOnDateAndPastEndDate()
    {
        var rt = RecurringTransaction.Create(
            UserId, "Rent", AccountId, null,
            1200m, "USD", 1m, TransactionType.Expense, null, 1, true,
            RecurringApplicability.Automatic, RecurringFrequency.Monthly, null,
            RecurringEndType.OnDate, null, new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2023, 1, 1, 0, 0, 0, DateTimeKind.Utc));

        rt.Apply();
        rt.IsActive.Should().BeFalse();
    }

    [Fact]
    public void CreateParticipant_ShouldCreateAndLink()
    {
        var rt = CreateValid();
        var personId = Guid.NewGuid();

        var participant = rt.CreateParticipant(personId, 600m);

        participant.Should().NotBeNull();
        participant.PersonId.Should().Be(personId);
        participant.ContributedAmount.Amount.Should().Be(600m);
        participant.RecurringTransactionId.Should().Be(rt.Id);
        rt.Participants.Should().Contain(participant);
    }
}
