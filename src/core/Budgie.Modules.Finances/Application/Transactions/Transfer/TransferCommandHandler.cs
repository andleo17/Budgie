using Budgie.Modules.Finances.Domain.Categories;
using Budgie.Modules.Finances.Domain.Transactions;
using Budgie.Modules.Finances.Infrastructure;
using Budgie.Modules.Finances.Infrastructure.Database;
using Budgie.Modules.Finances.Infrastructure.Logging;
using Budgie.Shared.Abstractions;
using Budgie.Shared.Domain.ValueObjects;
using Budgie.Shared.Enums;
using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Budgie.Modules.Finances.Application.Transactions.Transfer;

internal sealed class TransferCommandHandler(
    FinancesDbContext dbContext,
    ICurrentUserProvider currentUserProvider,
    ILanguageProvider languageProvider,
    ITranslationService translationService,
    ILogger<TransferCommandHandler> logger)
    : IRequestHandler<TransferCommand, ErrorOr<Guid>>
{
    public async Task<ErrorOr<Guid>> Handle(TransferCommand request, CancellationToken cancellationToken)
    {
        var fromAccount = await dbContext.Accounts.FirstOrDefaultAsync(a => a.Id == request.FromAccountId, cancellationToken);
        var toAccount = await dbContext.Accounts.FirstOrDefaultAsync(a => a.Id == request.ToAccountId, cancellationToken);

        if (fromAccount is null || toAccount is null)
        {
            return Error.NotFound("Account.NotFound", "One or both accounts were not found.");
        }

        var transferId = Guid.NewGuid();
        var userId = currentUserProvider.GetUserId();
        var lang = languageProvider.LanguageCode;

        // 1. Create outgoing transaction
        var outgoing = Transaction.Create(
            userId,
            fromAccount.Id,
            SystemCategoryIds.Transfer,
            request.Amount,
            request.Currency,
            request.ExchangeRate,
            request.Date,
            request.Notes ?? translationService.Translate("transfer.to_account", lang, toAccount.Name),
            TransactionType.Transfer,
            linkedTransactionId: transferId);

        fromAccount.ApplyTransaction(Money.FromPositive(request.Amount, CurrencyCode.From(request.Currency)), request.ExchangeRate, 1, TransactionType.Transfer);

        // 2. Create incoming transaction
        var incoming = Transaction.Create(
            userId,
            toAccount.Id,
            SystemCategoryIds.Transfer,
            request.Amount,
            request.Currency,
            request.ExchangeRate,
            request.Date,
            request.Notes ?? translationService.Translate("transfer.from_account", lang, fromAccount.Name),
            TransactionType.Income,
            linkedTransactionId: transferId);

        toAccount.ApplyTransaction(Money.FromPositive(request.Amount, CurrencyCode.From(request.Currency)), request.ExchangeRate, 1, TransactionType.Income);

        // 3. Create fee transaction if applicable
        if (request.Fee > 0)
        {
            var feeTransaction = Transaction.Create(
                userId,
                fromAccount.Id,
                SystemCategoryIds.TransferFee,
                request.Fee,
                request.Currency,
                request.ExchangeRate,
                request.Date,
                translationService.Translate("transfer.fee", lang),
                TransactionType.Expense,
                linkedTransactionId: transferId);

            fromAccount.ApplyTransaction(Money.FromPositive(request.Fee, CurrencyCode.From(request.Currency)), request.ExchangeRate, 1, TransactionType.Expense);
            dbContext.Transactions.Add(feeTransaction);
        }

        dbContext.Transactions.Add(outgoing);
        dbContext.Transactions.Add(incoming);

        await dbContext.SaveChangesAsync(cancellationToken);

        LogMessages.TransferCompleted(logger, request.FromAccountId, request.ToAccountId, request.Amount, request.Currency);

        return transferId;
    }
}
