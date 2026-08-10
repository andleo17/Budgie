using Budgie.Modules.Finances.Application;
using ErrorOr;
using Mediator;

namespace Budgie.Modules.Finances.Application.Transactions.GetTransactions;

internal sealed class GetTransactionsQueryHandler(ITransactionRepository txRepository)
    : IRequestHandler<GetTransactionsQuery, ErrorOr<PagedTransactionResponse>>
{
    public async ValueTask<ErrorOr<PagedTransactionResponse>> Handle(GetTransactionsQuery request, CancellationToken ct)
    {
        var totalCount = await txRepository.GetTransactionCountAsync(request, ct);
        var items = await txRepository.GetTransactionsAsync(request, ct);
        return new PagedTransactionResponse(items, totalCount, request.Page ?? 1, request.PageSize ?? 100);
    }
}
