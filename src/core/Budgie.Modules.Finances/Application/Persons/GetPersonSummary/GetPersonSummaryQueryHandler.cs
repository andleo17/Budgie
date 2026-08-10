using Budgie.Shared.Infrastructure;
using ErrorOr;
using Mediator;

namespace Budgie.Modules.Finances.Application.Persons.GetPersonSummary;

public record GetPersonSummaryQuery(Guid PersonId) : IRequest<ErrorOr<PersonSummaryResponse>>;

public record PersonSummaryResponse(int TotalTransactions, decimal TotalContributed,
    decimal TotalRepaid, decimal TotalPending, string Currency);

internal sealed class GetPersonSummaryQueryHandler(IDbConnectionFactory factory)
    : IRequestHandler<GetPersonSummaryQuery, ErrorOr<PersonSummaryResponse>>
{
    public async ValueTask<ErrorOr<PersonSummaryResponse>> Handle(GetPersonSummaryQuery request, CancellationToken ct)
    {
        const string sql = @"
            WITH person_participants AS (
                SELECT tp.*, t.""ParentTransactionId"" FROM ""TransactionParticipants"" tp
                JOIN ""Transactions"" t ON t.""Id"" = tp.""TransactionId""
                WHERE tp.""PersonId"" = @PersonId
            ), parent_ids AS (
                SELECT DISTINCT p.""TransactionId"" AS parent_tx_id FROM person_participants p
                WHERE p.""ParentTransactionId"" IS NULL AND EXISTS (
                    SELECT 1 FROM ""Transactions"" c JOIN ""TransactionParticipants"" cp ON cp.""TransactionId"" = c.""Id""
                    WHERE c.""ParentTransactionId"" = p.""TransactionId"" AND cp.""PersonId"" = @PersonId)
            ), effective AS (
                SELECT * FROM person_participants
                WHERE ""TransactionId"" NOT IN (SELECT parent_tx_id FROM parent_ids)
            )
            SELECT COUNT(*) AS TotalTransactions,
                COALESCE(SUM(""ContributedAmount""),0) AS TotalContributed,
                COALESCE(SUM(CASE WHEN ""RemainingAmount""<=0 AND NOT ""IsTrackingOnly"" THEN ""ContributedAmount"" ELSE 0 END),0) AS TotalRepaid,
                COALESCE(SUM(""ContributedAmount"" - CASE WHEN ""RemainingAmount""<=0 AND NOT ""IsTrackingOnly"" THEN ""ContributedAmount"" ELSE 0 END),0) AS TotalPending,
                COALESCE(MIN(""RemainingCurrency""),'USD') AS Currency
            FROM effective";
        return await factory.QuerySingleAsync<PersonSummaryResponse>(sql, new { request.PersonId }, ct);
    }
}
