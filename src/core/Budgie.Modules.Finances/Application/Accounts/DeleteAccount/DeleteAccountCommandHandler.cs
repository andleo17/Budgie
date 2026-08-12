using Budgie.Modules.Finances.Application;
using Budgie.Shared.Abstractions;
using ErrorOr;
using Mediator;

namespace Budgie.Modules.Finances.Application.Accounts.DeleteAccount;

public record DeleteAccountCommand(Guid Id) : IRequest<ErrorOr<Success>>;

internal sealed class DeleteAccountCommandHandler(
    IAccountRepository accountRepository,
    ICurrentUserProvider currentUserProvider)
    : IRequestHandler<DeleteAccountCommand, ErrorOr<Success>>
{
    public async ValueTask<ErrorOr<Success>> Handle(DeleteAccountCommand request, CancellationToken ct)
    {
        var account = await accountRepository.GetByIdAsync(request.Id, currentUserProvider.GetUserId(), ct);
        if (account is null) return Error.NotFound("Account.NotFound", "Account not found.");

        await accountRepository.DeleteAsync(request.Id, currentUserProvider.GetUserId(), ct);
        return Result.Success;
    }
}
