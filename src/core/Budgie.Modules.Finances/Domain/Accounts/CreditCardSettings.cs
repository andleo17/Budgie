using Budgie.Shared.Abstractions;
using Budgie.Shared.Domain.ValueObjects;
using Budgie.Shared.Enums;

namespace Budgie.Modules.Finances.Domain.Accounts;

public class CreditCardSettings
{
    public Money CreditLimit { get; internal set; } = null!;
    public CreditConsumptionMode CreditConsumptionMode { get; internal set; }
    public int? BillingCycleDay { get; internal set; }
    public int? PaymentDueDay { get; internal set; }
    public Percentage AnnualInterestRate { get; internal set; } = Percentage.From(0);
    public bool BillingCycleDayInclusive { get; internal set; }
    public InterestCalculationMethod InterestCalculationMethod { get; internal set; }

    public CreditCardSettings() { }

    public CreditCardSettings(
        decimal creditLimit,
        CreditConsumptionMode creditConsumptionMode,
        int? billingCycleDay,
        int? paymentDueDay,
        decimal annualInterestRate,
        bool billingCycleDayInclusive,
        CurrencyCode currency,
        InterestCalculationMethod interestCalculationMethod = InterestCalculationMethod.FrenchAmortization)
    {
        if (billingCycleDay is < 1 or > 31)
            throw new ArgumentException("Billing cycle day must be between 1 and 31.", nameof(billingCycleDay));
        if (paymentDueDay is < 1 or > 31)
            throw new ArgumentException("Payment due day must be between 1 and 31.", nameof(paymentDueDay));

        CreditLimit = Money.FromNonNegative(creditLimit, currency);
        CreditConsumptionMode = creditConsumptionMode;
        BillingCycleDay = billingCycleDay;
        PaymentDueDay = paymentDueDay;
        AnnualInterestRate = Percentage.From(annualInterestRate);
        BillingCycleDayInclusive = billingCycleDayInclusive;
        InterestCalculationMethod = interestCalculationMethod;
    }

    public (DateTime Start, DateTime End)? GetBillingPeriod(DateTime asOf)
    {
        if (BillingCycleDay is not { } cycleDay)
            return null;

        var cycleDayClamped = Math.Clamp(cycleDay, 1, 28);
        DateTime periodEnd;

        if (asOf.Day >= cycleDayClamped)
            periodEnd = new DateTime(asOf.Year, asOf.Month, cycleDayClamped, 23, 59, 59, DateTimeKind.Unspecified);
        else
            periodEnd = new DateTime(asOf.Year, asOf.Month - 1, cycleDayClamped, 23, 59, 59, DateTimeKind.Unspecified).AddMonths(1);

        var periodStart = periodEnd.AddMonths(-1).AddDays(1);

        if (!BillingCycleDayInclusive)
            periodEnd = periodEnd.AddDays(-1);

        return (periodStart.Date, periodEnd.Date);
    }

    public Money CalculateInterest(Money balance, int days)
    {
        if (AnnualInterestRate.Value <= 0)
            return Money.FromNonNegative(0, balance.Currency);
        var interest = balance.Amount * (AnnualInterestRate.Value / 100m / 365m) * days;
        return Money.FromAny(interest, balance.Currency);
    }
}
