using Budgie.Modules.Finances.Application;
using Budgie.Modules.Finances.Domain.Accounts;
using Budgie.Modules.Finances.Domain.Categories;
using Budgie.Modules.Finances.Domain.Events;
using Budgie.Modules.Finances.Domain.Transactions;
using Budgie.Shared.Abstractions;
using Budgie.Shared.Domain.ValueObjects;
using Budgie.Shared.Enums;
using ErrorOr;
using Mediator;

namespace Budgie.Modules.Finances.Application.Accounts.CreateAccount;

public record CreateAccountCommand(
    string Name, string Currency, decimal InitialBalance, AccountType Type,
    decimal? CreditLimit = null, CreditConsumptionMode? CreditConsumptionMode = null,
    int? BillingCycleDay = null, int? PaymentDueDay = null, decimal? AnnualInterestRate = null,
    bool BillingCycleDayInclusive = true, InterestCalculationMethod? InterestCalculationMethod = null,
    DateTime? CreatedAt = null, DateTime? ControlStartDate = null) : IRequest<ErrorOr<Guid>>;

internal sealed class CreateAccountCommandHandler(
    IAccountRepository accountRepository,
    ICurrentUserProvider currentUserProvider,
    IMediator mediator)
    : IRequestHandler<CreateAccountCommand, ErrorOr<Guid>>
{
    public async ValueTask<ErrorOr<Guid>> Handle(CreateAccountCommand request, CancellationToken ct)
    {
        var userId = currentUserProvider.GetUserId();

        CreditCardSettings? creditCardSettings = null;
        if (request.Type == AccountType.Credit)
        {
            creditCardSettings = new CreditCardSettings(
                request.CreditLimit ?? 0m,
                request.CreditConsumptionMode ?? CreditConsumptionMode.Total,
                request.BillingCycleDay,
                request.PaymentDueDay,
                request.AnnualInterestRate ?? 0m,
                request.BillingCycleDayInclusive,
                CurrencyCode.From(request.Currency),
                request.InterestCalculationMethod ?? Budgie.Shared.Enums.InterestCalculationMethod.FrenchAmortization);
        }

        var account = Account.Create(userId, request.Name, request.Currency, 0m,
            request.Type, creditCardSettings, request.CreatedAt, request.ControlStartDate);

        await accountRepository.AddAsync(account, ct);

        await mediator.Publish(new AccountCreatedEvent(
            account.Id, userId, account.Name.Value, request.InitialBalance, request.CreditLimit,
            account.Balance.Currency.Code, account.Type, account.ControlStartDate), ct);

        return account.Id;
    }
}
