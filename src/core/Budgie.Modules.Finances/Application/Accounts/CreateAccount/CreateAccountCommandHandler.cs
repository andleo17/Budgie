using Budgie.Modules.Finances.Application;
using Budgie.Modules.Finances.Domain.Accounts;
using Budgie.Shared.Abstractions;
using Budgie.Shared.Domain.ValueObjects;
using Budgie.Shared.Enums;
using ErrorOr;
using Mediator;

namespace Budgie.Modules.Finances.Application.Accounts.CreateAccount;

public record CreateAccountCommand(
    string Name, string Currency, decimal InitialBalance, int Type, decimal? CreditLimit)
    : IRequest<ErrorOr<Guid>>;

internal sealed class CreateAccountCommandHandler(
    IAccountRepository accountRepository,
    ICurrentUserProvider currentUserProvider)
    : IRequestHandler<CreateAccountCommand, ErrorOr<Guid>>
{
    public async ValueTask<ErrorOr<Guid>> Handle(CreateAccountCommand request, CancellationToken ct)
    {
        var userId = currentUserProvider.GetUserId();
        CreditCardSettings? ccs = null;
        if (request.Type == 2 && request.CreditLimit.HasValue)
            ccs = new CreditCardSettings(request.CreditLimit.Value, CreditConsumptionMode.Total,
                null, null, 0, true, CurrencyCode.From(request.Currency));

        var account = Account.Create(userId, request.Name, request.Currency, request.InitialBalance,
            (AccountType)request.Type, ccs);

        await accountRepository.AddAsync(account, ct);
        return account.Id;
    }
}
