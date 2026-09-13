using Budgie.Modules.Finances.Application;
using Budgie.Modules.Finances.Domain.Accounts;
using Budgie.Modules.Finances.Domain.Categories;
using Budgie.Modules.Finances.Domain.Events;
using Budgie.Modules.Finances.Domain.Transactions;
using Budgie.Shared.Abstractions;
using Budgie.Shared.Domain.ValueObjects;
using Budgie.Shared.Enums;
using ErrorOr;
using Mediator;
using System.Globalization;

namespace Budgie.Modules.Finances.Application.Transactions.CreateTransaction;

public record TransactionParticipantDto(
    Guid PersonId,
    decimal ContributedAmount,
    decimal RemainingAmount,
    bool IsTrackingOnly,
    Guid? DestinationAccountId);

public record CreateTransactionCommand(
    Guid AccountId,
    Guid? CategoryId,
    decimal Amount,
    string Currency,
    decimal ExchangeRate,
    DateTime Date,
    string? Notes,
    TransactionType Type,
    List<TransactionParticipantDto>? Participants = null,
    int Installments = 1,
    Guid? ParentTransactionId = null,
    bool IsInitialBalance = false,
    bool IsInterestFree = true) : IRequest<ErrorOr<Guid>>;

internal sealed class CreateTransactionCommandHandler(
    IAccountRepository accountRepository,
    ITransactionRepository txRepository,
    ICurrentUserProvider currentUserProvider,
    IMediator mediator)
    : IRequestHandler<CreateTransactionCommand, ErrorOr<Guid>>
{
    public async ValueTask<ErrorOr<Guid>> Handle(CreateTransactionCommand request, CancellationToken ct)
    {
        var userId = currentUserProvider.GetUserId();
        var account = await accountRepository.GetByIdAsync(request.AccountId, userId, ct);
        if (account is null) return Error.NotFound("Account.NotFound", "Account not found.");

        // Strict Ledger Rule: ControlStartDate validation
        if (!account.CanAcceptTransactionOn(request.Date))
        {
            return CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "en"
                ? Error.Validation("Transaction.BeforeControlStartDate", $"Transaction date ({request.Date:yyyy-MM-dd}) cannot precede the account control start date ({account.ControlStartDate:yyyy-MM-dd}).")
                : Error.Validation("Transaction.BeforeControlStartDate", $"La fecha de la transacción ({request.Date:yyyy-MM-dd}) no puede ser anterior a la fecha de control de la cuenta ({account.ControlStartDate:yyyy-MM-dd}).");
        }

        if (request.Participants is { Count: > 1 } &&
            request.Participants.Count(p => p.RemainingAmount == 0 && !p.IsTrackingOnly && p.DestinationAccountId.HasValue) > 1)
        {
            return CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "en"
                ? Error.Validation("Transaction.TooManyRepaid", "Only one participant can be repaid immediately per transaction.")
                : Error.Validation("Transaction.TooManyRepaid", "Solo un participante puede ser liquidado inmediatamente por transacción.");
        }

        if (request.Installments <= 1)
        {
            return await CreateSingleAsync(userId, account, request, ct);
        }

        return await CreateInstallmentsAsync(userId, account, request, ct);
    }

    private async ValueTask<ErrorOr<Guid>> CreateSingleAsync(Guid userId, Account account, CreateTransactionCommand request, CancellationToken ct)
    {
        var tx = Transaction.Create(userId, request.AccountId, request.CategoryId,
            request.Amount, request.Currency, request.ExchangeRate, request.Date, request.Notes,
            request.Type, request.Installments, parentTransactionId: request.ParentTransactionId,
            isInitialBalance: request.IsInitialBalance);

        await txRepository.AddAsync(tx, ct);

        if (request.Participants is { Count: > 0 })
        {
            foreach (var p in request.Participants)
            {
                var participant = tx.CreateParticipant(p.PersonId, p.ContributedAmount,
                    p.RemainingAmount, p.IsTrackingOnly, p.DestinationAccountId);
                await txRepository.AddParticipantAsync(participant, ct);
            }
        }

        await mediator.Publish(new TransactionCreatedEvent(
            tx.Id, userId, request.AccountId, request.CategoryId,
            request.Amount, request.Currency, request.ExchangeRate, request.Date,
            request.Type, request.Participants, request.Installments,
            request.ParentTransactionId, request.IsInitialBalance), ct);

        return tx.Id;
    }

    private async ValueTask<ErrorOr<Guid>> CreateInstallmentsAsync(Guid userId, Account account, CreateTransactionCommand request, CancellationToken ct)
    {
        var n = request.Installments;
        var installmentGroupId = Guid.NewGuid();
        var categoryId = request.IsInitialBalance ? SystemCategoryIds.InitialBalance : request.CategoryId;

        // Calculate per-installment amounts (interest-aware when not free and credit card)
        var installmentAmounts = new List<decimal>(n);
        var monthlyRate = 0m;

        if (!request.IsInterestFree && account.Type == AccountType.Credit && account.CreditCardSettings?.AnnualInterestRate is { } annualRate && annualRate.Value > 0)
        {
            var rate = annualRate.Value;
            var method = account.CreditCardSettings.InterestCalculationMethod;
            monthlyRate = rate / 12m / 100m;

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

        var installmentList = new List<Transaction>(n);

        for (var i = 0; i < n; i++)
        {
            var installmentNumber = i + 1;
            var amt = installmentAmounts[i];
            var date = request.Date.AddMonths(i);

            var tx = Transaction.Create(
                userId, request.AccountId, categoryId,
                amt, request.Currency, request.ExchangeRate,
                date, request.Notes, request.Type,
                1, installmentGroupId: installmentGroupId,
                installmentNumber: installmentNumber, totalInstallments: n,
                interestRate: monthlyRate);

            await txRepository.AddAsync(tx, ct);
            installmentList.Add(tx);

            if (request.Participants is not null)
            {
                foreach (var p in request.Participants)
                {
                    var proratedContributed = Math.Round(p.ContributedAmount / n, 2, MidpointRounding.AwayFromZero);
                    var proratedRemaining = Math.Round(p.RemainingAmount / n, 2, MidpointRounding.AwayFromZero);
                    if (installmentNumber == n)
                    {
                        var totalPrevContributed = Math.Round(p.ContributedAmount / n, 2, MidpointRounding.AwayFromZero) * (n - 1);
                        var totalPrevRemaining = Math.Round(p.RemainingAmount / n, 2, MidpointRounding.AwayFromZero) * (n - 1);
                        proratedContributed = Math.Round(p.ContributedAmount - totalPrevContributed, 2, MidpointRounding.AwayFromZero);
                        proratedRemaining = Math.Round(p.RemainingAmount - totalPrevRemaining, 2, MidpointRounding.AwayFromZero);
                    }
                    var participant = tx.CreateParticipant(p.PersonId, proratedContributed, proratedRemaining, p.IsTrackingOnly, null);
                    // DestinationAccount is cleared for installments split; immediate settlement only via first installment child
                    await txRepository.AddParticipantAsync(participant, ct);
                }
            }
        }

        // Create child virtual loan transactions per installment for credit shares
        if (request.Participants is not null)
        {
            foreach (var p in request.Participants)
            {
                if (p.IsTrackingOnly) continue;

                if (p.RemainingAmount > 0 && request.Type != TransactionType.Loan)
                {
                    for (var i = 0; i < n; i++)
                    {
                        var installmentNumber = i + 1;
                        var installmentTx = installmentList[i];
                        var proratedRemaining = Math.Round(p.RemainingAmount / n, 2, MidpointRounding.AwayFromZero);
                        var proratedContributed = Math.Round(p.ContributedAmount / n, 2, MidpointRounding.AwayFromZero);
                        if (installmentNumber == n)
                        {
                            var totalPrevRemaining = Math.Round(p.RemainingAmount / n, 2, MidpointRounding.AwayFromZero) * (n - 1);
                            var totalPrevContributed = Math.Round(p.ContributedAmount / n, 2, MidpointRounding.AwayFromZero) * (n - 1);
                            proratedRemaining = Math.Round(p.RemainingAmount - totalPrevRemaining, 2, MidpointRounding.AwayFromZero);
                            proratedContributed = Math.Round(p.ContributedAmount - totalPrevContributed, 2, MidpointRounding.AwayFromZero);
                        }
                        var childDate = request.Date.AddMonths(i);
                        var childTx = Transaction.Create(
                            userId, request.AccountId, null,
                            proratedRemaining, request.Currency, request.ExchangeRate,
                            childDate, request.Notes, TransactionType.Loan, 1,
                            parentTransactionId: installmentTx.Id);
                        childTx.CreateParticipant(p.PersonId, proratedContributed, proratedRemaining, false, null);
                        await txRepository.AddAsync(childTx, ct);
                        // Add participant directly via repository (CreateParticipant already added to collection but need DB)
                        foreach (var cp in childTx.Participants)
                            await txRepository.AddParticipantAsync(cp, ct);
                    }
                }
                else if (p.RemainingAmount == 0 && p.DestinationAccountId.HasValue)
                {
                    // Immediate settlement only for first installment
                    var firstInstallment = installmentList[0];
                    var childTx = Transaction.Create(
                        userId, request.AccountId, null,
                        p.ContributedAmount, request.Currency, request.ExchangeRate,
                        request.Date, request.Notes, TransactionType.Income, 1,
                        parentTransactionId: firstInstallment.Id);
                    childTx.CreateParticipant(p.PersonId, p.ContributedAmount, 0, false, p.DestinationAccountId);
                    await txRepository.AddAsync(childTx, ct);
                    foreach (var cp in childTx.Participants)
                        await txRepository.AddParticipantAsync(cp, ct);
                }
            }
        }

        // Update account balance: total impact
        // For credit Total mode: only first installment's full amount is consumed upfront
        // For credit Partial or non-credit: sum of all installments (= request.Amount)
        decimal balanceDelta;
        if (account.Type == AccountType.Credit && account.CreditCardSettings?.CreditConsumptionMode == CreditConsumptionMode.Total)
        {
            // Total consumption: full amount
            var amountToApply = Money.FromPositive(request.Amount, CurrencyCode.From(request.Currency));
            if (amountToApply.Currency != account.Balance.Currency)
                amountToApply = amountToApply.ConvertTo(account.Balance.Currency, request.ExchangeRate);
            balanceDelta = request.Type switch
            {
                TransactionType.Income or TransactionType.Repayment => amountToApply.Amount,
                _ => -amountToApply.Amount
            };
        }
        else
        {
            // Partial or non-credit: sum of installments (equals request.Amount)
            var total = installmentAmounts.Sum();
            var amountToApply = Money.FromPositive(total, CurrencyCode.From(request.Currency));
            if (amountToApply.Currency != account.Balance.Currency)
                amountToApply = amountToApply.ConvertTo(account.Balance.Currency, request.ExchangeRate);
            balanceDelta = request.Type switch
            {
                TransactionType.Income or TransactionType.Repayment => amountToApply.Amount,
                _ => -amountToApply.Amount
            };
        }

        // Include immediate cash participant delta (only once)
        var participantImmediateDelta = 0m;
        if (request.Participants is { Count: > 0 })
        {
            foreach (var p in request.Participants)
            {
                if (!p.IsTrackingOnly && p.RemainingAmount == 0 && p.DestinationAccountId.HasValue && p.DestinationAccountId.Value == request.AccountId)
                {
                    participantImmediateDelta += p.ContributedAmount;
                }
            }
        }

        // Also handle destination account balance if cash repayment to different account
        foreach (var p in request.Participants ?? Enumerable.Empty<TransactionParticipantDto>())
        {
            if (!p.IsTrackingOnly && p.RemainingAmount == 0 && p.DestinationAccountId.HasValue && p.DestinationAccountId.Value != request.AccountId)
            {
                var destAccount = await accountRepository.GetByIdRawAsync(p.DestinationAccountId.Value, ct);
                if (destAccount is not null)
                {
                    await accountRepository.UpdateBalanceAsync(destAccount.UserId, destAccount.Id, destAccount.Balance.Amount + p.ContributedAmount, ct);
                }
            }
        }

        var newBalance = account.Balance.Amount + balanceDelta + participantImmediateDelta;
        await accountRepository.UpdateBalanceAsync(account.UserId, account.Id, newBalance, ct);

        return installmentGroupId;
    }
}
