using Budgie.Modules.Finances.Application;
using Budgie.Modules.Finances.Domain.Categories;
using Budgie.Modules.Finances.Domain.Transactions;
using Budgie.Shared.Abstractions;
using Budgie.Shared.Domain.ValueObjects;
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

        var reducedAny = false;
        foreach (var debt in request.Debts)
        {
            var remaining = await txRepository.GetParticipantRemainingAsync(debt.ParticipantId, userId, ct);
            if (remaining is null) continue;

            if (debt.Amount > remaining.Value)
                return Error.Validation("Transaction.Overpayment", "Amount exceeds remaining amount.");

            reducedAny = true;
        }

        if (!reducedAny)
            return Error.Validation("Transaction.NoDebts", "No valid debts selected for repayment.");

        var repayment = Transaction.Create(userId, request.AccountId, SystemCategoryIds.LoanRepayment,
            request.Amount, request.Currency, request.ExchangeRate, request.Date,
            request.Notes, TransactionType.Repayment);

        await txRepository.AddAsync(repayment, ct);

        foreach (var debt in request.Debts)
        {
            var remaining = await txRepository.GetParticipantRemainingAsync(debt.ParticipantId, userId, ct);
            if (remaining is null) continue;

            await txRepository.ReduceParticipantRemainingAsync(debt.ParticipantId, debt.Amount, ct);
            await txRepository.ReduceParentParticipantRemainingAsync(debt.ParticipantId, debt.Amount, ct);
            await txRepository.AddRepaymentDetailAsync(repayment.Id, debt.ParticipantId, debt.Amount, ct);
        }

        account.ApplyTransaction(Money.FromPositive(request.Amount, CurrencyCode.From(request.Currency)),
            request.ExchangeRate, 1, TransactionType.Repayment);
        await accountRepository.UpdateBalanceAsync(account.Id, account.Balance.Amount, ct);

        return repayment.Id;
    }
}
