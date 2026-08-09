using Budgie.Modules.Finances.Domain.Accounts;
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

namespace Budgie.Modules.Finances.Application.Accounts.CreateAccount;

internal sealed class CreateAccountCommandHandler(
    FinancesDbContext dbContext,
    ICurrentUserProvider currentUserProvider,
    IUserSettingsProvider settingsProvider,
    ILanguageProvider languageProvider,
    ITranslationService translationService,
    ILogger<CreateAccountCommandHandler> logger) 
    : IRequestHandler<CreateAccountCommand, ErrorOr<Guid>>
{
    public async ValueTask<ErrorOr<Guid>> Handle(CreateAccountCommand request, CancellationToken cancellationToken)
    {
        var consumptionMode = request.CreditConsumptionMode;
        if (request.Type == AccountType.Credit && consumptionMode == null)
        {
            consumptionMode = await settingsProvider.GetDefaultCreditConsumptionModeAsync();
        }

        var userId = currentUserProvider.GetUserId();
        var lang = languageProvider.LanguageCode;

        CreditCardSettings? creditCardSettings = null;
        if (request.Type == AccountType.Credit)
        {
            creditCardSettings = new CreditCardSettings(
                request.CreditLimit ?? 0m,
                consumptionMode ?? CreditConsumptionMode.Total,
                request.BillingCycleDay,
                request.PaymentDueDay,
                request.AnnualInterestRate ?? 0m,
                request.BillingCycleDayInclusive,
                CurrencyCode.From(request.Currency),
                request.InterestCalculationMethod ?? Budgie.Shared.Enums.InterestCalculationMethod.FrenchAmortization);
        }

        var account = Account.Create(
            userId,
            request.Name,
            request.Currency,
            0m,
            request.Type,
            creditCardSettings,
            request.CreatedAt);

        dbContext.Accounts.Add(account);

        // For credit accounts: first set up the credit line as an Income (available credit)
        if (request.Type == AccountType.Credit && request.CreditLimit > 0)
        {
            var limitAmount = request.CreditLimit.Value;
            var limitTx = Transaction.Create(
                userId,
                account.Id,
                null,
                limitAmount,
                account.Balance.Currency,
                1m,
                account.CreatedAt,
                translationService.Translate("account.credit_line_setup", lang),
                TransactionType.Income);

            account.ApplyTransaction(Money.FromPositive(limitAmount, CurrencyCode.From(request.Currency)), 1m, 1, TransactionType.Income);
            dbContext.Transactions.Add(limitTx);
        }

        // Then apply any initial balance (existing debt for credit, regular balance for others)
        if (request.InitialBalance != 0)
        {
            var txType = request.Type == AccountType.Credit
                ? TransactionType.Expense
                : request.InitialBalance > 0 ? TransactionType.Income : TransactionType.Expense;
            var absAmount = Math.Abs(request.InitialBalance);

            var tx = Transaction.Create(
                userId,
                account.Id,
                SystemCategoryIds.InitialBalance,
                absAmount,
                account.Balance.Currency,
                1m,
                account.CreatedAt,
                translationService.Translate("account.initial_balance", lang),
                txType,
                isInitialBalance: true);

            account.ApplyTransaction(Money.FromPositive(absAmount, CurrencyCode.From(request.Currency)), 1m, 1, txType);
            dbContext.Transactions.Add(tx);
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        LogMessages.AccountCreated(logger, account.Id, account.Name);

        return account.Id;
    }
}
