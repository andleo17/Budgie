using Budgie.Modules.Finances.Domain.Accounts;
using Budgie.Modules.Finances.Domain.Categories;
using Budgie.Modules.Finances.Domain.Transactions;
using Budgie.Modules.Finances.Infrastructure.Database;
using Budgie.Modules.Finances.Infrastructure.Logging;
using Budgie.Shared.Abstractions;
using Budgie.Shared.Domain.ValueObjects;
using Budgie.Shared.Enums;
using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Budgie.Modules.Finances.Application.Transactions.CreateTransaction;

internal sealed class CreateTransactionCommandHandler(
    FinancesDbContext dbContext,
    ICurrentUserProvider currentUserProvider,
    ILogger<CreateTransactionCommandHandler> logger)
    : IRequestHandler<CreateTransactionCommand, ErrorOr<Guid>>
{
    public async Task<ErrorOr<Guid>> Handle(CreateTransactionCommand request, CancellationToken cancellationToken)
    {
        var account = await dbContext.Accounts.FirstOrDefaultAsync(a => a.Id == request.AccountId, cancellationToken);
        if (account is null)
            return Error.NotFound("Account.NotFound", $"Account with ID {request.AccountId} was not found.");

        if (request.CategoryId.HasValue)
        {
            var categoryExists = await dbContext.Categories.AnyAsync(c => c.Id == request.CategoryId, cancellationToken);
            if (!categoryExists)
                return Error.NotFound("Category.NotFound", $"Category with ID {request.CategoryId} was not found.");
        }

        var normalizedDate = request.Date;

        if (normalizedDate < account.CreatedAt.Date)
            return Error.Validation("Transaction.DateBeforeAccountCreation", "Transaction date cannot be before the account was created.");

        var categoryId = request.IsInitialBalance ? SystemCategoryIds.InitialBalance : request.CategoryId;

        if (request.Installments <= 1)
            return await CreateSingleTransaction(request, account, categoryId, normalizedDate, cancellationToken);

        return await CreateInstallments(request, account, categoryId, normalizedDate, cancellationToken);
    }

    private async Task<ErrorOr<Guid>> CreateSingleTransaction(
        CreateTransactionCommand request, Account account, Guid? categoryId,
        DateTime normalizedDate, CancellationToken cancellationToken)
    {
        var userId = currentUserProvider.GetUserId();

        var transaction = Transaction.Create(
            userId, request.AccountId, categoryId,
            request.Amount, request.Currency, request.ExchangeRate,
            normalizedDate, request.Notes, request.Type,
            request.Installments,
            parentTransactionId: request.ParentTransactionId,
            isInitialBalance: request.IsInitialBalance);

        if (request.Participants is not null)
        {
            if (request.Participants.Count(p => p.RemainingAmount == 0 && !p.IsTrackingOnly && p.DestinationAccountId.HasValue) > 1)
                return Error.Validation("Transaction.TooManyRepaid", "Only one participant can be repaid per transaction.");

            foreach (var p in request.Participants)
                transaction.CreateParticipant(p.PersonId, p.ContributedAmount, p.RemainingAmount, p.IsTrackingOnly, p.DestinationAccountId);
        }

        account.ApplyTransaction(Money.FromPositive(request.Amount, CurrencyCode.From(request.Currency)), request.ExchangeRate, request.Installments, request.Type);

        dbContext.Transactions.Add(transaction);
        await dbContext.SaveChangesAsync(cancellationToken);

        await CreateChildTransactions(request, transaction, account, userId, normalizedDate, cancellationToken);

        LogMessages.TransactionCreated(logger, transaction.Id, transaction.Type.ToString(), transaction.Amount.Amount, transaction.Amount.Currency);
        return transaction.Id;
    }

    private async Task<ErrorOr<Guid>> CreateInstallments(
        CreateTransactionCommand request, Account account, Guid? categoryId,
        DateTime normalizedDate, CancellationToken cancellationToken)
    {
        var userId = currentUserProvider.GetUserId();
        var installmentGroupId = Guid.NewGuid();
        var n = request.Installments;

        var installmentAmounts = new List<decimal>();
        var monthlyRate = 0m;

        if (!request.IsInterestFree && account.Type == AccountType.Credit && account.CreditCardSettings?.AnnualInterestRate is { } rate && rate > 0)
        {
            var annualRate = account.CreditCardSettings.AnnualInterestRate;
            var method = account.CreditCardSettings.InterestCalculationMethod;
            monthlyRate = annualRate / 12m / 100m;

            if (method == InterestCalculationMethod.FrenchAmortization && monthlyRate > 0)
            {
                var factor = (decimal)Math.Pow((double)(1 + monthlyRate), n);
                var payment = request.Amount * monthlyRate * factor / (factor - 1);
                var rounded = Math.Round(payment, 2, MidpointRounding.AwayFromZero);

                for (var i = 1; i <= n; i++)
                {
                    var isLast = i == n;
                    var amt = isLast
                        ? Math.Round(request.Amount - rounded * (n - 1), 2, MidpointRounding.AwayFromZero)
                        : rounded;
                    installmentAmounts.Add(amt);
                }
            }
            else if (method == InterestCalculationMethod.SimpleInterest)
            {
                var totalWithInterest = request.Amount * (1 + monthlyRate * n);
                var payment = totalWithInterest / n;
                var rounded = Math.Round(payment, 2, MidpointRounding.AwayFromZero);

                for (var i = 1; i <= n; i++)
                {
                    var isLast = i == n;
                    var amt = isLast
                        ? Math.Round(totalWithInterest - rounded * (n - 1), 2, MidpointRounding.AwayFromZero)
                        : rounded;
                    installmentAmounts.Add(amt);
                }
            }
            else
            {
                var baseAmt = request.Amount / n;
                var roundedBase = Math.Round(baseAmt, 2, MidpointRounding.AwayFromZero);
                for (var i = 1; i <= n; i++)
                {
                    var isLast = i == n;
                    var amt = isLast
                        ? Math.Round(request.Amount - roundedBase * (n - 1), 2, MidpointRounding.AwayFromZero)
                        : roundedBase;
                    installmentAmounts.Add(amt);
                }
            }
        }
        else
        {
            var baseAmt = request.Amount / n;
            var roundedBase = Math.Round(baseAmt, 2, MidpointRounding.AwayFromZero);
            for (var i = 1; i <= n; i++)
            {
                var isLast = i == n;
                var amt = isLast
                    ? Math.Round(request.Amount - roundedBase * (n - 1), 2, MidpointRounding.AwayFromZero)
                    : roundedBase;
                installmentAmounts.Add(amt);
            }
        }

        var installmentList = new List<Transaction>();

        for (var i = 0; i < n; i++)
        {
            var installmentNumber = i + 1;
            var amt = installmentAmounts[i];
            var date = normalizedDate.AddMonths(i);

            var tx = Transaction.Create(
                userId, request.AccountId, categoryId,
                amt, request.Currency, request.ExchangeRate,
                date, request.Notes, request.Type,
                1, installmentGroupId: installmentGroupId,
                installmentNumber: installmentNumber, totalInstallments: n,
                interestRate: monthlyRate);

            if (request.Participants is not null)
            {
                foreach (var p in request.Participants)
                {
                    var proratedContributed = Math.Round(p.ContributedAmount / n, 2, MidpointRounding.AwayFromZero);
                    var proratedRemaining = Math.Round(p.RemainingAmount / n, 2, MidpointRounding.AwayFromZero);

                    if (installmentNumber == n)
                    {
                        var totalPrevContributed = proratedContributed * (n - 1);
                        var totalPrevRemaining = proratedRemaining * (n - 1);
                        proratedContributed = Math.Round(p.ContributedAmount - totalPrevContributed, 2, MidpointRounding.AwayFromZero);
                        proratedRemaining = Math.Round(p.RemainingAmount - totalPrevRemaining, 2, MidpointRounding.AwayFromZero);
                    }

                    tx.CreateParticipant(p.PersonId, proratedContributed, proratedRemaining, p.IsTrackingOnly, null);
                }
            }

            if (installmentNumber == 1)
                ApplyInstallmentImpact(tx, account, request.Amount, request.Type, request.Installments);
            else if (account.Type != AccountType.Credit ||
                     account.CreditCardSettings?.CreditConsumptionMode == CreditConsumptionMode.Partial)
                ApplyInstallmentImpact(tx, account, amt, request.Type, 1);

            dbContext.Transactions.Add(tx);
            installmentList.Add(tx);
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        if (request.Participants is not null)
        {
            foreach (var p in request.Participants)
            {
                if (p.IsTrackingOnly) continue;

                if (p.RemainingAmount > 0 && !p.IsTrackingOnly && request.Type != TransactionType.Loan)
                {
                    for (var i = 0; i < n; i++)
                    {
                        var installmentNumber = i + 1;
                        var installmentTx = installmentList[i];
                        var proratedRemaining = Math.Round(p.RemainingAmount / n, 2, MidpointRounding.AwayFromZero);
                        var proratedContributed = Math.Round(p.ContributedAmount / n, 2, MidpointRounding.AwayFromZero);

                        if (installmentNumber == n)
                        {
                            var totalPrevRemaining = proratedRemaining * (n - 1);
                            var totalPrevContributed = proratedContributed * (n - 1);
                            proratedRemaining = Math.Round(p.RemainingAmount - totalPrevRemaining, 2, MidpointRounding.AwayFromZero);
                            proratedContributed = Math.Round(p.ContributedAmount - totalPrevContributed, 2, MidpointRounding.AwayFromZero);
                        }

                        var date = normalizedDate.AddMonths(i);
                        var childTx = Transaction.Create(
                            userId, request.AccountId, null,
                            proratedRemaining, request.Currency, request.ExchangeRate,
                            date, request.Notes, TransactionType.Loan, 1,
                            parentTransactionId: installmentTx.Id);

                        childTx.CreateParticipant(p.PersonId, proratedContributed, proratedRemaining, false, null);
                        dbContext.Transactions.Add(childTx);
                    }
                }
                else if (p.RemainingAmount == 0 && p.DestinationAccountId.HasValue)
                {
                    var firstInstallment = installmentList[0];
                    var date = normalizedDate;
                    var childTx = Transaction.Create(
                        userId, request.AccountId, null,
                        p.ContributedAmount, request.Currency, request.ExchangeRate,
                        date, request.Notes, TransactionType.Income, 1,
                        parentTransactionId: firstInstallment.Id);

                    childTx.CreateParticipant(p.PersonId, p.ContributedAmount, 0, false, p.DestinationAccountId);
                    dbContext.Transactions.Add(childTx);

                    account.ApplyTransaction(Money.FromPositive(p.ContributedAmount, CurrencyCode.From(request.Currency)), request.ExchangeRate, 1, TransactionType.Income);
                }
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        LogMessages.TransactionCreated(logger, installmentGroupId, "Installment", request.Amount, request.Currency);
        return installmentGroupId;
    }

    private static void ApplyInstallmentImpact(Transaction tx, Account account, decimal amount, TransactionType type, int installments)
    {
        account.ApplyTransaction(Money.FromPositive(amount, tx.Amount.Currency), tx.ExchangeRate, installments, type);
    }

    private async Task CreateChildTransactions(
        CreateTransactionCommand request, Transaction transaction, Account account,
        Guid userId, DateTime normalizedDate, CancellationToken cancellationToken)
    {
        if (request.Participants is null) return;

        foreach (var p in request.Participants)
        {
            if (p.IsTrackingOnly) continue;

            if (p.RemainingAmount > 0 && !p.IsTrackingOnly && request.Type != TransactionType.Loan)
            {
                var childTx = Transaction.Create(
                    userId, transaction.AccountId, null,
                    p.RemainingAmount, transaction.Amount.Currency, transaction.ExchangeRate,
                    normalizedDate, request.Notes, TransactionType.Loan, 1,
                    parentTransactionId: transaction.Id);

                childTx.CreateParticipant(p.PersonId, p.RemainingAmount, p.RemainingAmount, false, null);
                dbContext.Transactions.Add(childTx);
            }
            else if (p.RemainingAmount == 0 && p.DestinationAccountId.HasValue)
            {
                var childTx = Transaction.Create(
                    userId, transaction.AccountId, null,
                    p.ContributedAmount, transaction.Amount.Currency, transaction.ExchangeRate,
                    normalizedDate, request.Notes, TransactionType.Income, 1,
                    parentTransactionId: transaction.Id);

                childTx.CreateParticipant(p.PersonId, p.ContributedAmount, 0, false, p.DestinationAccountId);
                dbContext.Transactions.Add(childTx);

                account.ApplyTransaction(Money.FromPositive(p.ContributedAmount, transaction.Amount.Currency), transaction.ExchangeRate, 1, TransactionType.Income);
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
