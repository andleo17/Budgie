using Budgie.Shared.Enums;

namespace Budgie.Modules.Finances.Application.Accounts;

public record AccountResponse(
    Guid Id,
    string Name,
    string Currency,
    decimal Balance,
    AccountType Type,
    decimal? CreditLimit,
    CreditConsumptionMode? CreditConsumptionMode,
    DateTime CreatedAt,
    int? BillingCycleDay = null,
    int? PaymentDueDay = null,
    decimal? AnnualInterestRate = null,
    bool BillingCycleDayInclusive = true,
    InterestCalculationMethod? InterestCalculationMethod = null);
