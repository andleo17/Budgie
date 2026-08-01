using Budgie.Modules.Finances.Domain.Accounts;
using Budgie.Modules.Finances.Domain.Categories;
using Budgie.Shared.Abstractions;
using Budgie.Shared.Domain.ValueObjects;
using Budgie.Shared.Enums;

namespace Budgie.Modules.Finances.Domain.Transactions;

public class Transaction : Entity
{
    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public Guid AccountId { get; private set; }
    public Guid? CategoryId { get; private set; }
    public Money Amount { get; private set; } = null!;
    public decimal ExchangeRate { get; private set; } = 1;
    public DateTime Date { get; private set; }
    public string? Notes { get; private set; }
    public TransactionType Type { get; private set; }
    public int Installments { get; private set; }
    public Guid? LinkedTransactionId { get; private set; }
    public Guid? ParentTransactionId { get; private set; }
    public bool IsInitialBalance { get; private set; }
    public Guid? InstallmentGroupId { get; private set; }
    public int InstallmentNumber { get; private set; } = 1;
    public int TotalInstallments { get; private set; } = 1;
    public Percentage InterestRate { get; private set; } = Percentage.From(0);

    private readonly List<TransactionParticipant> _participants = new();
    public IReadOnlyCollection<TransactionParticipant> Participants => _participants.AsReadOnly();

    private readonly List<Transaction> _childTransactions = new();
    public IReadOnlyCollection<Transaction> ChildTransactions => _childTransactions.AsReadOnly();

    public Account Account { get; private set; } = null!;
    public Category? Category { get; private set; }
    public Transaction? ParentTransaction { get; private set; }

    private Transaction() { }

    public static Transaction Create(
        Guid userId,
        Guid accountId,
        Guid? categoryId,
        decimal amount,
        string currency,
        decimal exchangeRate,
        DateTime date,
        string? notes,
        TransactionType type,
        int installments = 1,
        Guid? linkedTransactionId = null,
        Guid? parentTransactionId = null,
        bool isInitialBalance = false,
        Guid? installmentGroupId = null,
        int installmentNumber = 1,
        int totalInstallments = 1,
        decimal interestRate = 0)
    {
        Guard.Positive(installments, nameof(installments));
        Guard.Positive(installmentNumber, nameof(installmentNumber));
        Guard.Positive(totalInstallments, nameof(totalInstallments));

        var currencyCode = CurrencyCode.From(currency);
        var positiveAmount = Money.FromPositive(amount, currencyCode);
        var normalizedExchangeRate = exchangeRate < 0 ? 1 : exchangeRate;
        if (normalizedExchangeRate == 0)
            normalizedExchangeRate = 1;

        return new Transaction
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            AccountId = accountId,
            CategoryId = categoryId,
            Amount = positiveAmount,
            ExchangeRate = normalizedExchangeRate,
            Date = GuardDate(date),
            Notes = notes?.Trim(),
            Type = type,
            Installments = installments,
            LinkedTransactionId = linkedTransactionId,
            ParentTransactionId = parentTransactionId,
            IsInitialBalance = isInitialBalance,
            InstallmentGroupId = installmentGroupId,
            InstallmentNumber = installmentNumber,
            TotalInstallments = totalInstallments,
            InterestRate = Percentage.From(interestRate)
        };
    }

    public void Update(
        Guid? categoryId,
        decimal amount,
        string currency,
        decimal exchangeRate,
        DateTime date,
        string? notes,
        TransactionType type,
        int installments,
        Guid? parentTransactionId = null)
    {
        Guard.Positive(installments, nameof(installments));

        var currencyCode = CurrencyCode.From(currency);
        var normalizedExchangeRate = exchangeRate < 0 ? 1 : exchangeRate;
        if (normalizedExchangeRate == 0)
            normalizedExchangeRate = 1;

        CategoryId = categoryId;
        Amount = Money.FromPositive(amount, currencyCode);
        ExchangeRate = normalizedExchangeRate;
        Date = GuardDate(date);
        Notes = notes?.Trim();
        Type = type;
        Installments = installments;
        ParentTransactionId = parentTransactionId;
    }

    public TransactionParticipant CreateParticipant(Guid personId, decimal contributedAmount, decimal remainingAmount = 0, bool isTrackingOnly = false, Guid? destinationAccountId = null)
    {
        var participant = TransactionParticipant.Create(personId, contributedAmount, Amount.Currency, remainingAmount, isTrackingOnly, destinationAccountId);
        participant.TransactionId = Id;
        _participants.Add(participant);
        return participant;
    }

    public List<TransactionParticipant> ClearParticipants()
    {
        var old = _participants.ToList();
        _participants.Clear();
        return old;
    }

    private static DateTime GuardDate(DateTime date) =>
        DateTime.SpecifyKind(date, DateTimeKind.Unspecified);
}
