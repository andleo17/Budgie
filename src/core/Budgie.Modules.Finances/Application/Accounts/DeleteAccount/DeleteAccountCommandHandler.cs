using Budgie.Modules.Finances.Infrastructure.Database;
using Budgie.Modules.Finances.Infrastructure.Logging;
using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Budgie.Modules.Finances.Application.Accounts.DeleteAccount;

internal sealed class DeleteAccountCommandHandler(
    FinancesDbContext dbContext,
    ILogger<DeleteAccountCommandHandler> logger)
    : IRequestHandler<DeleteAccountCommand, ErrorOr<Success>>
{
    public async Task<ErrorOr<Success>> Handle(DeleteAccountCommand request, CancellationToken cancellationToken)
    {
        var account = await dbContext.Accounts.FirstOrDefaultAsync(a => a.Id == request.Id, cancellationToken);

        if (account is null)
        {
            return Error.NotFound("Account.NotFound", "Account not found.");
        }

        dbContext.Accounts.Remove(account);
        await dbContext.SaveChangesAsync(cancellationToken);

        LogMessages.AccountDeleted(logger, request.Id);

        return Result.Success;
    }
}
