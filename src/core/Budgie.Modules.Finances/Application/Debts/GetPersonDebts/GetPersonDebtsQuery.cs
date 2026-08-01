using ErrorOr;
using MediatR;

namespace Budgie.Modules.Finances.Application.Debts.GetPersonDebts;

public record GetPersonDebtsQuery(Guid PersonId) : IRequest<ErrorOr<List<DebtResponse>>>;

public record DebtResponse(
    Guid ParticipantId,
    Guid TransactionId,
    Guid PersonId,
    decimal ContributedAmount,
    decimal RemainingAmount,
    DateTime Date,
    string? Notes,
    string Currency);
