using Budgie.Modules.Finances.Application;
using Budgie.Modules.Finances.Application.Debts.GetPersonDebts;
using Budgie.Modules.Finances.Domain.Events;
using Budgie.Modules.Finances.Domain.Transactions;
using Budgie.Shared.Abstractions;
using Budgie.Shared.Enums;
using ErrorOr;
using Mediator;
using System.Globalization;

namespace Budgie.Modules.Finances.Application.Debts.SettleDebt;

public record SettleDebtCommand(
    Guid PersonId,
    Guid DestinationAccountId,
    decimal Amount,
    string Currency,
    DateTime Date,
    List<Guid>? SpecificParticipantIds = null,
    string? Notes = null) : IRequest<ErrorOr<Guid>>;

internal sealed class SettleDebtCommandHandler(
    ITransactionRepository txRepository,
    IAccountRepository accountRepository,
    ICurrentUserProvider currentUserProvider,
    IMediator mediator)
    : IRequestHandler<SettleDebtCommand, ErrorOr<Guid>>
{
    public async ValueTask<ErrorOr<Guid>> Handle(SettleDebtCommand request, CancellationToken ct)
    {
        var userId = currentUserProvider.GetUserId();
        var account = await accountRepository.GetByIdAsync(request.DestinationAccountId, userId, ct);
        if (account is null) return Error.NotFound("Account.NotFound", "Destination account not found.");

        if (!account.CanAcceptTransactionOn(request.Date))
        {
            return CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "en"
                ? Error.Validation("Transaction.BeforeControlStartDate", $"Transaction date ({request.Date:yyyy-MM-dd}) cannot precede the account control start date ({account.ControlStartDate:yyyy-MM-dd}).")
                : Error.Validation("Transaction.BeforeControlStartDate", $"La fecha de la transacción ({request.Date:yyyy-MM-dd}) no puede ser anterior a la fecha de control de la cuenta ({account.ControlStartDate:yyyy-MM-dd}).");
        }

        if (request.Amount <= 0)
        {
            return CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "en"
                ? Error.Validation("Debt.InvalidAmount", "Settlement amount must be greater than zero.")
                : Error.Validation("Debt.InvalidAmount", "El monto a liquidar debe ser mayor a cero.");
        }

        var repaymentTx = Transaction.Create(
            userId,
            request.DestinationAccountId,
            null,
            request.Amount,
            request.Currency,
            1m,
            request.Date,
            request.Notes ?? "Debt settlement",
            TransactionType.Repayment);

        await txRepository.AddAsync(repaymentTx, ct);

        var remainingToSettle = request.Amount;
        var debts = await txRepository.GetPersonDebtsAsync(request.PersonId, ct);

        if (request.SpecificParticipantIds is { Count: > 0 })
        {
            debts = debts.Where(d => request.SpecificParticipantIds.Contains(d.ParticipantId)).ToList();
        }

        foreach (var debt in debts.OrderBy(d => d.Date))
        {
            if (remainingToSettle <= 0) break;

            var settlePortion = Math.Min(debt.RemainingAmount, remainingToSettle);
            await txRepository.ReduceParticipantRemainingAsync(debt.ParticipantId, settlePortion, ct);
            await txRepository.ReduceParentParticipantRemainingAsync(debt.ParticipantId, settlePortion, ct);
            await txRepository.AddRepaymentDetailAsync(repaymentTx.Id, debt.ParticipantId, settlePortion, ct);

            remainingToSettle -= settlePortion;
        }

        // Publish event for decoupled balance adjustment
        await mediator.Publish(new DebtSettledEvent(
            repaymentTx.Id, userId, account.Id, request.PersonId, request.Amount, request.Currency, request.Date), ct);

        return repaymentTx.Id;
    }
}
