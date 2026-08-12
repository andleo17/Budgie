using Budgie.Modules.Finances.Application;
using Budgie.Modules.Finances.Domain.Categories;
using Budgie.Modules.Finances.Domain.Transactions;
using Budgie.Shared.Abstractions;
using Budgie.Shared.Domain.ValueObjects;
using Budgie.Shared.Enums;
using ErrorOr;
using Mediator;

namespace Budgie.Modules.Finances.Application.Transactions.Transfer;

public record TransferCommand(
    Guid FromAccountId, Guid ToAccountId, decimal Amount, decimal Fee,
    string Currency, decimal ExchangeRate, DateTime Date, string? Notes)
    : IRequest<ErrorOr<Guid>>;

internal sealed class TransferCommandHandler(
    IAccountRepository accountRepository,
    ITransactionRepository txRepository,
    ICurrentUserProvider currentUserProvider)
    : IRequestHandler<TransferCommand, ErrorOr<Guid>>
{
    public async ValueTask<ErrorOr<Guid>> Handle(TransferCommand request, CancellationToken ct)
    {
        var userId = currentUserProvider.GetUserId();
        var fromAccount = await accountRepository.GetByIdAsync(request.FromAccountId, userId, ct);
        var toAccount = await accountRepository.GetByIdAsync(request.ToAccountId, userId, ct);

        if (fromAccount is null || toAccount is null)
            return Error.NotFound("Account.NotFound", "One or both accounts were not found.");

        var transferId = Guid.NewGuid();

        var outgoing = Transaction.Create(userId, fromAccount.Id, SystemCategoryIds.Transfer,
            request.Amount, request.Currency, request.ExchangeRate, request.Date,
            request.Notes ?? $"Transfer to {toAccount.Name}",
            TransactionType.Transfer, linkedTransactionId: transferId);

        var incoming = Transaction.Create(userId, toAccount.Id, SystemCategoryIds.Transfer,
            request.Amount, request.Currency, request.ExchangeRate, request.Date,
            request.Notes ?? $"Transfer from {fromAccount.Name}",
            TransactionType.Income, linkedTransactionId: transferId);

        await txRepository.AddAsync(outgoing, ct);
        await txRepository.AddAsync(incoming, ct);

        fromAccount.ApplyTransaction(Money.FromPositive(request.Amount, CurrencyCode.From(request.Currency)),
            request.ExchangeRate, 1, TransactionType.Transfer);
        toAccount.ApplyTransaction(Money.FromPositive(request.Amount, CurrencyCode.From(request.Currency)),
            request.ExchangeRate, 1, TransactionType.Income);

        if (request.Fee > 0)
        {
            var feeTx = Transaction.Create(userId, fromAccount.Id, SystemCategoryIds.TransferFee,
                request.Fee, request.Currency, request.ExchangeRate, request.Date,
                "Transfer fee", TransactionType.Expense, linkedTransactionId: transferId);
            await txRepository.AddAsync(feeTx, ct);
            fromAccount.ApplyTransaction(Money.FromPositive(request.Fee, CurrencyCode.From(request.Currency)),
                request.ExchangeRate, 1, TransactionType.Expense);
        }

        await accountRepository.UpdateBalanceAsync(fromAccount.Id, fromAccount.Balance.Amount, ct);
        await accountRepository.UpdateBalanceAsync(toAccount.Id, toAccount.Balance.Amount, ct);

        return transferId;
    }
}
