using Budgie.Shared.Enums;
using ErrorOr;
using Mediator;

namespace Budgie.Modules.Finances.Application.Transactions.GetTransactions;

public record PagedResponse<T>(
    List<T> Items,
    int TotalCount,
    int Page,
    int PageSize);

public record GetTransactionsQuery(
    IReadOnlyList<Guid>? AccountIds = null,
    IReadOnlyList<Guid>? CategoryIds = null,
    IReadOnlyList<TransactionType>? TransactionTypes = null,
    DateTime? StartDate = null,
    DateTime? EndDate = null,
    string? Search = null,
    bool IncludeRunningBalance = false,
    int? Page = null,
    int? PageSize = null,
    Guid? PersonId = null,
    IReadOnlyList<Guid>? TagIds = null) : IRequest<ErrorOr<PagedResponse<TransactionResponse>>>;
