using Budgie.Modules.Finances.Application;
using Budgie.Modules.Finances.Application.Accounts;
using Budgie.Shared.Abstractions;
using ErrorOr;
using Mediator;

namespace Budgie.Modules.Finances.Application.Accounts.GetAccounts;

public record GetAccountsQuery : IRequest<ErrorOr<List<AccountResponse>>>;

internal sealed class GetAccountsQueryHandler(
    IAccountRepository accountRepository,
    ICurrentUserProvider currentUserProvider)
    : IRequestHandler<GetAccountsQuery, ErrorOr<List<AccountResponse>>>
{
    public async ValueTask<ErrorOr<List<AccountResponse>>> Handle(GetAccountsQuery request, CancellationToken ct)
        => await accountRepository.GetByUserIdAsync(currentUserProvider.GetUserId(), ct);
}
