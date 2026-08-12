using Budgie.Modules.Finances.Application;
using Budgie.Modules.Finances.Application.Accounts;
using Budgie.Shared.Abstractions;
using ErrorOr;
using Mediator;

namespace Budgie.Modules.Finances.Application.Accounts.GetAccountById;

public record GetAccountByIdQuery(Guid Id) : IRequest<ErrorOr<AccountResponse>>;

internal sealed class GetAccountByIdQueryHandler(
    IAccountRepository accountRepository,
    ICurrentUserProvider currentUserProvider)
    : IRequestHandler<GetAccountByIdQuery, ErrorOr<AccountResponse>>
{
    public async ValueTask<ErrorOr<AccountResponse>> Handle(GetAccountByIdQuery request, CancellationToken ct)
    {
        var account = await accountRepository.GetByIdAsync(request.Id, currentUserProvider.GetUserId(), ct);
        return account is not null
            ? new AccountResponse(account.Id, account.Name, account.Balance.Amount, account.Balance.Currency.Code,
                (int)account.Type, account.CreditCardSettings?.CreditLimit.Amount,
                account.CreditCardSettings?.CreditLimit.Currency.Code,
                (int)(account.CreditCardSettings?.CreditConsumptionMode ?? 0),
                account.CreditCardSettings?.AnnualInterestRate.Value,
                account.CreditCardSettings?.BillingCycleDayInclusive ?? true,
                account.CreatedAt)
            : Error.NotFound("Account.NotFound", "Account not found.");
    }
}
