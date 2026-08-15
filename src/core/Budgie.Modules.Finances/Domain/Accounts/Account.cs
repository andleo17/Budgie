using Budgie.Modules.Finances.Domain.Transactions;
using Budgie.Shared.Abstractions;
using Budgie.Shared.Domain.ValueObjects;
using Budgie.Shared.Enums;

namespace Budgie.Modules.Finances.Domain.Accounts;

public class Account : Entity
{
    private const int MaxNameLength = 100;

    public Guid Id { get; internal set; }
    public Guid UserId { get; internal set; }
    public Name Name { get; internal set; } = null!;
    public Money Balance { get; internal set; } = null!;
    public AccountType Type { get; internal set; }
    public DateTime CreatedAt { get; internal set; }
    public DateTime ControlStartDate { get; internal set; }
    public CreditCardSettings? CreditCardSettings { get; internal set; }

    public Account() { }

    public static Account Create(
        Guid userId,
        string name,
        string currency,
        decimal initialBalance,
        AccountType type = AccountType.Checking,
        CreditCardSettings? creditCardSettings = null,
        DateTime? createdAt = null,
        DateTime? controlStartDate = null)
    {
        var currencyCode = CurrencyCode.From(currency);

        if (type == AccountType.Credit && creditCardSettings is null)
            throw new ArgumentException("Credit card settings are required for credit accounts.", nameof(creditCardSettings));

        var createdDate = GuardDate(createdAt ?? DateTime.UtcNow);
        var startDate = GuardDate(controlStartDate ?? createdDate.Date);

        return new Account
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Name = Name.From(name, MaxNameLength),
            Balance = Money.FromNonNegative(initialBalance, currencyCode),
            Type = type,
            CreatedAt = createdDate,
            ControlStartDate = startDate,
            CreditCardSettings = creditCardSettings
        };
    }

    public void Update(string name, CreditCardSettings? creditCardSettings, DateTime? controlStartDate = null)
    {
        Name = Name.From(name, MaxNameLength);
        CreditCardSettings = creditCardSettings;
        if (controlStartDate.HasValue)
            ControlStartDate = GuardDate(controlStartDate.Value.Date);
    }

    public bool CanAcceptTransactionOn(DateTime date) => date.Date >= ControlStartDate.Date;

    public (DateTime Start, DateTime End)? GetBillingPeriod(DateTime asOf)
    {
        if (CreditCardSettings is not { } settings)
            return null;

        return settings.GetBillingPeriod(asOf);
    }

    public Money CalculateInterest(Money balance, int days)
    {
        if (CreditCardSettings is not { } settings)
            return Money.FromNonNegative(0, Balance.Currency);

        return settings.CalculateInterest(balance, days);
    }

    public void ApplyTransaction(Money amount, decimal exchangeRate, int installments, TransactionType type)
    {
        var amountToApply = amount;
        if (amount.Currency != Balance.Currency)
        {
            amountToApply = amount.ConvertTo(Balance.Currency, exchangeRate);
        }

        if (Type == AccountType.Credit && CreditCardSettings?.CreditConsumptionMode == CreditConsumptionMode.Partial && installments > 1)
        {
            amountToApply = amountToApply * (1m / installments);
        }

        Balance = type switch
        {
            TransactionType.Income or TransactionType.Repayment => Balance + amountToApply,
            TransactionType.Expense or TransactionType.Transfer or TransactionType.Loan => Balance - amountToApply,
            _ => Balance
        };
    }

    public void RevertTransaction(Money amount, decimal exchangeRate, int installments, TransactionType type)
    {
        var amountToRevert = amount;
        if (amount.Currency != Balance.Currency)
        {
            amountToRevert = amount.ConvertTo(Balance.Currency, exchangeRate);
        }

        if (Type == AccountType.Credit && CreditCardSettings?.CreditConsumptionMode == CreditConsumptionMode.Partial && installments > 1)
        {
            amountToRevert = amountToRevert * (1m / installments);
        }

        Balance = type switch
        {
            TransactionType.Income or TransactionType.Repayment => Balance - amountToRevert,
            TransactionType.Expense or TransactionType.Transfer or TransactionType.Loan => Balance + amountToRevert,
            _ => Balance
        };
    }

    private static DateTime GuardDate(DateTime date) =>
        DateTime.SpecifyKind(date, DateTimeKind.Unspecified);

    internal void SetInitialBalance(Money initialBalance)
    {
        Balance = Money.FromNonNegative(initialBalance.Amount, initialBalance.Currency);
    }
}
