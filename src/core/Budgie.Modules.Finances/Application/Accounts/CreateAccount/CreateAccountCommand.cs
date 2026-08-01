using Budgie.Shared.Enums;
using ErrorOr;
using MediatR;

namespace Budgie.Modules.Finances.Application.Accounts.CreateAccount;

public record CreateAccountCommand(
    string Name,
    string Currency,
    decimal InitialBalance,
    AccountType Type = AccountType.Checking,
    decimal? CreditLimit = null,
    CreditConsumptionMode? CreditConsumptionMode = null,
    int? BillingCycleDay = null,
    int? PaymentDueDay = null,
    decimal? AnnualInterestRate = null,
    bool BillingCycleDayInclusive = true,
    InterestCalculationMethod? InterestCalculationMethod = null,
    DateTime? CreatedAt = null) : IRequest<ErrorOr<Guid>>;
