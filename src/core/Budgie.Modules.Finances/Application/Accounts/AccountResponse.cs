namespace Budgie.Modules.Finances.Application.Accounts;

public record AccountResponse(Guid Id, string Name, decimal Balance, string Currency,
    int Type, decimal? CreditLimit, string? CreditLimitCurrency, int CreditConsumptionMode,
    decimal? AnnualInterestRate, bool BillingCycleDayInclusive, DateTime CreatedAt);
