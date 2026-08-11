using Budgie.Modules.Finances.Application;
using Budgie.Modules.Finances.Domain.Categories;
using Budgie.Modules.Finances.Domain.Transactions;
using Budgie.Shared.Abstractions;
using Budgie.Shared.Enums;
using ErrorOr;
using Mediator;

namespace Budgie.Modules.Finances.Application.Transactions.BulkRepay;

public record DebtRepayment(Guid ParticipantId, decimal Amount, Guid TransactionId);

public record BulkRepayCommand(Guid AccountId, decimal Amount, string Currency,
    decimal ExchangeRate, DateTime Date, string? Notes, List<DebtRepayment> Debts)
    : IRequest<ErrorOr<Guid>>;

internal sealed class BulkRepayCommandHandler(
    IAccountRepository accountRepository,
    ITransactionRepository txRepository,
    ICurrentUserProvider currentUserProvider)
    : IRequestHandler<BulkRepayCommand, ErrorOr<Guid>>
{
    public async ValueTask<ErrorOr<Guid>> Handle(BulkRepayCommand request, CancellationToken ct)
    {
        var userId = currentUserProvider.GetUserId();
        var account = await accountRepository.GetByIdAsync(request.AccountId, userId, ct);
        if (account is null) return Error.NotFound("Account.NotFound", "Account not found.");

        var repayment = Transaction.Create(userId, request.AccountId, SystemCategoryIds.LoanRepayment,
            request.Amount, request.Currency, request.ExchangeRate, request.Date,
            request.Notes, TransactionType.Repayment);

        await txRepository.AddAsync(repayment, ct);

        foreach (var debt in request.Debts)
        {
            await txRepository.ReduceParticipantRemainingAsync(debt.ParticipantId, debt.Amount, ct);
            await txRepository.ReduceParentParticipantRemainingAsync(debt.ParticipantId, debt.Amount, ct);
            await txRepository.AddRepaymentDetailAsync(repayment.Id, debt.ParticipantId, debt.Amount, ct);
        }

        var newBalance = account.Balance.Amount + request.Amount;
        await accountRepository.UpdateBalanceAsync(account.Id, newBalance, ct);

        return repayment.Id;
    }
}
