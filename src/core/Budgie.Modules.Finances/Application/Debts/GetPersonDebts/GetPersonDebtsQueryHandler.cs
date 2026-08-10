using Budgie.Shared.Enums;
using Budgie.Shared.Infrastructure;
using ErrorOr;
using Mediator;

namespace Budgie.Modules.Finances.Application.Debts.GetPersonDebts;

public record GetPersonDebtsQuery(Guid PersonId) : IRequest<ErrorOr<List<DebtResponse>>>;

public record DebtResponse(Guid ParticipantId, Guid TransactionId, Guid PersonId,
    decimal ContributedAmount, decimal RemainingAmount, DateTime Date, string? Notes, string Currency);

internal sealed class GetPersonDebtsQueryHandler(IDbConnectionFactory factory)
    : IRequestHandler<GetPersonDebtsQuery, ErrorOr<List<DebtResponse>>>
{
    public async ValueTask<ErrorOr<List<DebtResponse>>> Handle(GetPersonDebtsQuery request, CancellationToken ct)
    {
        const string sql = @"SELECT tp.""Id"" AS ParticipantId, tp.""TransactionId"", tp.""PersonId"",
            tp.""ContributedAmount"", tp.""RemainingAmount"", t.""Date"", t.""Notes"", tp.""RemainingCurrency"" AS Currency
            FROM ""TransactionParticipants"" tp JOIN ""Transactions"" t ON t.""Id"" = tp.""TransactionId""
            WHERE tp.""PersonId"" = @PersonId AND tp.""RemainingAmount"" > 0 AND NOT tp.""IsTrackingOnly""
              AND t.""Type"" = @LoanType";
        return (await factory.QueryListAsync<DebtResponse>(sql,
            new { request.PersonId, LoanType = (int)TransactionType.Loan }, ct)).ToList();
    }
}
