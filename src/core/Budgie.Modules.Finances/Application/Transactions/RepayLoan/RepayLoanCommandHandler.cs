using Budgie.Modules.Finances.Domain.Categories;
using Budgie.Modules.Finances.Domain.Transactions;
using Budgie.Modules.Finances.Infrastructure;
using Budgie.Modules.Finances.Infrastructure.Database;
using Budgie.Modules.Finances.Infrastructure.Logging;
using Budgie.Shared.Abstractions;
using Budgie.Shared.Domain.ValueObjects;
using Budgie.Shared.Enums;
using ErrorOr;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace Budgie.Modules.Finances.Application.Transactions.RepayLoan;

internal sealed class RepayLoanCommandHandler(
    FinancesDbContext dbContext,
    ICurrentUserProvider currentUserProvider,
    ILanguageProvider languageProvider,
    ITranslationService translationService,
    ILogger<RepayLoanCommandHandler> logger)
    : IRequestHandler<RepayLoanCommand, ErrorOr<Guid>>
{
    public async ValueTask<ErrorOr<Guid>> Handle(RepayLoanCommand request, CancellationToken cancellationToken)
    {
        var originalTransaction = await dbContext.Transactions
            .Include(t => t.Participants)
            .FirstOrDefaultAsync(t => t.Id == request.OriginalTransactionId, cancellationToken);

        if (originalTransaction is null)
        {
            return Error.NotFound("Transaction.NotFound", "Original loan transaction not found.");
        }

        var account = await dbContext.Accounts.FirstOrDefaultAsync(a => a.Id == originalTransaction.AccountId, cancellationToken);
        if (account is null)
        {
            return Error.NotFound("Account.NotFound", "Account for repayment not found.");
        }

        var lang = languageProvider.LanguageCode;

        // Create the repayment transaction
        var repayment = Transaction.Create(
            currentUserProvider.GetUserId(),
            originalTransaction.AccountId,
            SystemCategoryIds.LoanRepayment,
            request.Amount,
            request.Currency,
            request.ExchangeRate,
            request.Date,
            request.Notes ?? translationService.Translate("loan.repayment", lang),
            TransactionType.Repayment,
            linkedTransactionId: originalTransaction.Id);

        // Update account balance
        account.ApplyTransaction(Money.FromPositive(request.Amount, CurrencyCode.From(request.Currency)), request.ExchangeRate, 1, TransactionType.Repayment);

        // Update participant remaining amount
        var participant = originalTransaction.Participants.FirstOrDefault();
        if (participant != null)
        {
            participant.ReduceRemaining(request.Amount);
        }

        dbContext.Transactions.Add(repayment);
        await dbContext.SaveChangesAsync(cancellationToken);

        LogMessages.LoanRepaid(logger, repayment.Id, request.Amount, request.Currency);

        return repayment.Id;
    }
}
