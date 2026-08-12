using Budgie.Shared.Enums;

namespace Budgie.Modules.Finances.Application.Transactions.GetTransactions;

public record GetTransactionsQuery(
    IReadOnlyList<Guid>? AccountIds = null, IReadOnlyList<Guid>? CategoryIds = null,
    IReadOnlyList<TransactionType>? TransactionTypes = null, DateTime? StartDate = null,
    DateTime? EndDate = null, string? Search = null, Guid? PersonId = null,
    IReadOnlyList<Guid>? TagIds = null, bool IncludeRunningBalance = false,
    int? Page = null, int? PageSize = null)
    : Mediator.IRequest<ErrorOr.ErrorOr<PagedTransactionResponse>>;

public record PagedTransactionResponse(List<TransactionResponse> Items, int TotalCount, int Page, int PageSize);
