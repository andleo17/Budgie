using Budgie.Modules.Finances.Application;
using Budgie.Modules.Finances.Domain.Events;
using Budgie.Modules.Finances.Domain.Transactions;
using Budgie.Shared.Abstractions;
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
    bool IsInitialBalance = false) : IRequest<ErrorOr<Guid>>;

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

        // Publish domain event for decoupled side effects (balance update, splits, budgets)
        await mediator.Publish(new TransactionCreatedEvent(
            tx.Id, userId, request.AccountId, request.CategoryId,
            request.Amount, request.Currency, request.ExchangeRate, request.Date,
            request.Type, request.Participants, request.Installments,
            request.ParentTransactionId, request.IsInitialBalance), ct);

        return tx.Id;
    }
}
