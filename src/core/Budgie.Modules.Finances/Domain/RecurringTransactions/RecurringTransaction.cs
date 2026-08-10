using Budgie.Modules.Finances.Domain.Accounts;
using Budgie.Modules.Finances.Domain.Categories;
using Budgie.Shared.Abstractions;
using Budgie.Shared.Domain.ValueObjects;
using Budgie.Shared.Enums;

namespace Budgie.Modules.Finances.Domain.RecurringTransactions;

public class RecurringTransaction : Entity
{
    private const int MaxNameLength = 100;

    public Guid Id { get; internal set; }
    public Guid UserId { get; internal set; }
    public Name Name { get; internal set; } = null!;
    public Guid AccountId { get; internal set; }
    public Guid? CategoryId { get; internal set; }
    public Money Amount { get; internal set; } = null!;
    public decimal ExchangeRate { get; internal set; } = 1;
    public TransactionType Type { get; internal set; }
    public string? Notes { get; internal set; }
    public int Installments { get; internal set; } = 1;
    public bool IsInterestFree { get; internal set; } = true;
    public RecurringApplicability Applicability { get; internal set; }
    public RecurringFrequency? Frequency { get; internal set; }
    public int? CustomIntervalDays { get; internal set; }
    public RecurringEndType? EndType { get; internal set; }
    public int? EndAfterCount { get; internal set; }
    public DateTime? EndDate { get; internal set; }
    public DateTime? StartDate { get; internal set; }
    public DateTime? NextDueDate { get; internal set; }
    public int TimesApplied { get; internal set; }
    public DateTime? LastAppliedDate { get; internal set; }
    public bool IsActive { get; internal set; } = true;

    private readonly List<RecurringTransactionParticipant> _participants = new();
    public IReadOnlyCollection<RecurringTransactionParticipant> Participants => _participants.AsReadOnly();

    public Account Account { get; internal set; } = null!;
    public Category? Category { get; internal set; }

    public RecurringTransaction() { }

    public static RecurringTransaction Create(
        Guid userId,
        string name,
        Guid accountId,
        Guid? categoryId,
        decimal amount,
        string currency,
        decimal exchangeRate,
        TransactionType type,
        string? notes,
        int installments,
        bool isInterestFree,
        RecurringApplicability applicability,
        RecurringFrequency? frequency,
        int? customIntervalDays,
        RecurringEndType? endType,
        int? endAfterCount,
        DateTime? endDate,
        DateTime? startDate)
    {
        GuardPositive(installments, nameof(installments));

        if (applicability != RecurringApplicability.Template && frequency is null)
            throw new ArgumentException("Frequency is required when applicability is not Template.", nameof(frequency));

        var currencyCode = CurrencyCode.From(currency);
        var normalizedExchangeRate = exchangeRate < 0 ? 1 : exchangeRate;
        if (normalizedExchangeRate == 0)
            normalizedExchangeRate = 1;

        return new RecurringTransaction
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Name = Name.From(name, MaxNameLength),
            AccountId = accountId,
            CategoryId = categoryId,
            Amount = Money.FromPositive(amount, currencyCode),
            ExchangeRate = normalizedExchangeRate,
            Type = type,
            Notes = notes?.Trim(),
            Installments = installments,
            IsInterestFree = isInterestFree,
            Applicability = applicability,
            Frequency = applicability == RecurringApplicability.Template ? null : frequency,
            CustomIntervalDays = applicability == RecurringApplicability.Template ? null : customIntervalDays,
            EndType = applicability == RecurringApplicability.Template ? null : endType,
            EndAfterCount = applicability == RecurringApplicability.Template ? null : endAfterCount,
            EndDate = applicability == RecurringApplicability.Template || !endDate.HasValue ? null : GuardDate(endDate.Value),
            StartDate = applicability == RecurringApplicability.Template ? null : startDate.HasValue ? GuardDate(startDate.Value) : null,
            NextDueDate = applicability == RecurringApplicability.Template ? null : startDate.HasValue ? GuardDate(startDate.Value) : null,
            TimesApplied = 0,
            LastAppliedDate = null,
            IsActive = true
        };
    }

    public void Update(
        string name,
        Guid accountId,
        Guid? categoryId,
        decimal amount,
        string currency,
        decimal exchangeRate,
        TransactionType type,
        string? notes,
        int installments,
        bool isInterestFree,
        RecurringApplicability applicability,
        RecurringFrequency? frequency,
        int? customIntervalDays,
        RecurringEndType? endType,
        int? endAfterCount,
        DateTime? endDate,
        DateTime? startDate)
    {
        GuardPositive(installments, nameof(installments));

        if (applicability != RecurringApplicability.Template && frequency is null)
            throw new ArgumentException("Frequency is required when applicability is not Template.", nameof(frequency));

        var currencyCode = CurrencyCode.From(currency);
        var normalizedExchangeRate = exchangeRate < 0 ? 1 : exchangeRate;
        if (normalizedExchangeRate == 0)
            normalizedExchangeRate = 1;

        Name = Name.From(name, MaxNameLength);
        AccountId = accountId;
        CategoryId = categoryId;
        Amount = Money.FromPositive(amount, currencyCode);
        ExchangeRate = normalizedExchangeRate;
        Type = type;
        Notes = notes?.Trim();
        Installments = installments;
        IsInterestFree = isInterestFree;
        Applicability = applicability;
        Frequency = applicability == RecurringApplicability.Template ? null : frequency;
        CustomIntervalDays = applicability == RecurringApplicability.Template ? null : customIntervalDays;
        EndType = applicability == RecurringApplicability.Template ? null : endType;
        EndAfterCount = applicability == RecurringApplicability.Template ? null : endAfterCount;
        EndDate = applicability == RecurringApplicability.Template ? null : endDate.HasValue ? GuardDate(endDate.Value) : null;
        StartDate = applicability == RecurringApplicability.Template ? null : startDate.HasValue ? GuardDate(startDate.Value) : null;
        if (applicability != RecurringApplicability.Template && NextDueDate == null)
            NextDueDate = startDate.HasValue ? GuardDate(startDate.Value) : null;
    }

    public void Apply()
    {
        TimesApplied++;
        LastAppliedDate = GuardDate(DateTime.UtcNow);
        NextDueDate = ComputeNextDueDate();

        if (EndType == RecurringEndType.AfterCount && TimesApplied >= (EndAfterCount ?? int.MaxValue))
            IsActive = false;
        else if (EndType == RecurringEndType.OnDate && NextDueDate > (EndDate ?? DateTime.MaxValue))
            IsActive = false;
    }

    public RecurringTransactionParticipant CreateParticipant(Guid personId, decimal contributedAmount, decimal remainingAmount = 0, bool isTrackingOnly = false, Guid? destinationAccountId = null)
    {
        var participant = RecurringTransactionParticipant.Create(personId, contributedAmount, Amount.Currency, remainingAmount, isTrackingOnly, destinationAccountId);
        participant.RecurringTransactionId = Id;
        _participants.Add(participant);
        return participant;
    }

    public List<RecurringTransactionParticipant> ClearParticipants()
    {
        var old = _participants.ToList();
        _participants.Clear();
        return old;
    }

    private DateTime? ComputeNextDueDate()
    {
        if (LastAppliedDate == null) return StartDate;
        return Frequency switch
        {
            RecurringFrequency.Daily => LastAppliedDate.Value.AddDays(1),
            RecurringFrequency.Weekly => LastAppliedDate.Value.AddDays(7),
            RecurringFrequency.Biweekly => LastAppliedDate.Value.AddDays(14),
            RecurringFrequency.Monthly => LastAppliedDate.Value.AddMonths(1),
            RecurringFrequency.Yearly => LastAppliedDate.Value.AddYears(1),
            RecurringFrequency.Custom => LastAppliedDate.Value.AddDays(CustomIntervalDays ?? 30),
            _ => null
        };
    }

    private static DateTime GuardDate(DateTime date) =>
        DateTime.SpecifyKind(date, DateTimeKind.Unspecified);

    private static void GuardPositive(int value, string paramName)
    {
        if (value <= 0)
            throw new ArgumentException($"{paramName} must be positive.", paramName);
    }
}
