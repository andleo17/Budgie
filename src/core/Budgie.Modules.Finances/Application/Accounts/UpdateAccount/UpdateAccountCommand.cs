using Budgie.Shared.Enums;
using ErrorOr;
using Mediator;

namespace Budgie.Modules.Finances.Application.Accounts.UpdateAccount;

public record UpdateAccountCommand(
    Guid Id,
    string Name,
    decimal? CreditLimit,
    CreditConsumptionMode? CreditConsumptionMode,
    int? BillingCycleDay = null,
    int? PaymentDueDay = null,
    decimal? AnnualInterestRate = null,
    bool BillingCycleDayInclusive = true,
    InterestCalculationMethod? InterestCalculationMethod = null) : IRequest<ErrorOr<Success>>;
