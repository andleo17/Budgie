using Budgie.Modules.Finances.Application.Transactions.CreateTransaction;
using Budgie.Modules.Finances.Domain.Transactions;
using Budgie.Shared.Abstractions;
using Budgie.Shared.Domain.ValueObjects;
using Budgie.Shared.Enums;
using Budgie.Modules.Finances.Infrastructure.Database;
using ErrorOr;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace Budgie.Modules.Finances.Application.Transactions.GetTransactions;

internal sealed class GetTransactionsQueryHandler(FinancesDbContext dbContext)
    : IRequestHandler<GetTransactionsQuery, ErrorOr<PagedResponse<TransactionResponse>>>
{
    public async ValueTask<ErrorOr<PagedResponse<TransactionResponse>>> Handle(GetTransactionsQuery request, CancellationToken cancellationToken)
    {
        var startDate = request.StartDate is { } sd
            ? Guard.NormalizeDate(sd) : (DateTime?)null;
        var endDate = request.EndDate is { } ed
            ? Guard.NormalizeDate(ed) : (DateTime?)null;

        var query = dbContext.Transactions
            .Include(t => t.Account)
            .Include(t => t.Category)
            .AsQueryable();

        if (request.AccountIds is { Count: > 0 })
            query = query.Where(t => request.AccountIds.Contains(t.AccountId));

        if (request.CategoryIds is { Count: > 0 })
            query = query.Where(t => t.CategoryId != null && request.CategoryIds.Contains(t.CategoryId.Value));

        if (request.TransactionTypes is { Count: > 0 })
            query = query.Where(t => request.TransactionTypes.Contains(t.Type));

        if (startDate.HasValue)
            query = query.Where(t => t.Date >= startDate.Value);

        if (endDate.HasValue)
            query = query.Where(t => t.Date < endDate.Value.Date.AddDays(1));

        if (!string.IsNullOrWhiteSpace(request.Search))
            query = query.Where(t =>
                (t.Notes != null && t.Notes.Contains(request.Search)) ||
                t.Account.Name.Value.Contains(request.Search));

        if (request.PersonId.HasValue)
            query = query.Where(t => t.Participants.Any(p => p.PersonId == request.PersonId.Value));

        if (request.TagIds is { Count: > 0 })
        {
            var tagIds = request.TagIds.ToList();
            var taggedPersonIds = await dbContext.Database
                .SqlQuery<Guid>($"""
                    SELECT DISTINCT pt."PersonId"
                    FROM "PersonTags" pt
                    WHERE pt."TagId" = ANY({tagIds})
                    """)
                .ToListAsync(cancellationToken);

            if (taggedPersonIds.Count > 0)
                query = query.Where(t => t.Participants.Any(p => taggedPersonIds.Contains(p.PersonId)));
            else
                query = query.Where(t => false);
        }

        var totalCount = await query.CountAsync(cancellationToken);

        List<TransactionResponse> transactions;

        if (request.IncludeRunningBalance && request.AccountIds is { Count: 1 })
        {
            var singleAccountId = request.AccountIds[0];

            var startBalance = await dbContext.Transactions
                .Where(t => t.AccountId == singleAccountId && t.Date < (startDate ?? DateTime.MinValue))
                .SumAsync(t => (decimal?)(
                    (t.Type == TransactionType.Income || t.Type == TransactionType.Repayment ? t.Amount.Amount : -t.Amount.Amount)
                    * (t.Amount.Currency != dbContext.Accounts.Where(a => a.Id == singleAccountId).Select(a => a.Balance.Currency).FirstOrDefault()
                        ? t.ExchangeRate : 1m)
                ), cancellationToken) ?? 0m;

            var allTransactions = await query
                .Include(t => t.Participants)
                .OrderBy(t => t.Date)
                .ThenBy(t => t.ParentTransactionId != null ? 1 : 0)
                .ThenBy(t => t.ParentTransactionId ?? t.Id)
                .ThenBy(t => t.Type == TransactionType.Loan && t.ParentTransactionId != null ? 0 : 1)
                .ThenBy(t => t.Id)
                .ToListAsync(cancellationToken);

            var linkedLookup = allTransactions
                .Where(t => t.LinkedTransactionId.HasValue)
                .GroupBy(t => t.LinkedTransactionId!.Value)
                .ToDictionary(g => g.Key, g => g.ToList());

            var runningBalance = startBalance;
            var parentRunningBalance = new Dictionary<Guid, decimal>();
            var items = allTransactions.Select(t =>
            {
                var amount = t.Amount.Amount;
                if (t.Amount.Currency != t.Account.Balance.Currency)
                    amount *= t.ExchangeRate;

                var isVirtualLoan = t.ParentTransactionId.HasValue && t.Type == TransactionType.Loan;

                if (!isVirtualLoan)
                {
                    if (t.Type == TransactionType.Income || t.Type == TransactionType.Repayment)
                        runningBalance += amount;
                    else
                        runningBalance -= amount;
                }

                // Record parent's running balance for child LOANs to inherit
                if (t.ParentTransactionId is null)
                    parentRunningBalance[t.Id] = runningBalance;

                Guid? destId = null;
                string? destName = null;
                if (t.Type == TransactionType.Transfer && t.LinkedTransactionId.HasValue
                    && linkedLookup.TryGetValue(t.LinkedTransactionId.Value, out var linked))
                {
                    var dest = linked.FirstOrDefault(l => l.AccountId != t.AccountId);
                    if (dest is not null)
                    {
                        destId = dest.AccountId;
                        destName = dest.Account.Name;
                    }
                }

                var displayBalance = isVirtualLoan && t.ParentTransactionId.HasValue
                    && parentRunningBalance.TryGetValue(t.ParentTransactionId.Value, out var pb)
                    ? pb : runningBalance;

                return new TransactionResponse(
                    t.Id, t.AccountId, t.Account.Name,
                    t.CategoryId, t.Category?.Name?.Value,
                    t.Amount.Amount, t.Amount.Currency, t.ExchangeRate,
                    Guard.NormalizeDate(t.Date), t.Notes, t.Type, t.Installments, t.IsInitialBalance,
                    displayBalance, t.LinkedTransactionId, t.ParentTransactionId, destId, destName,
                    t.Participants.Select(p => new TransactionParticipantDto(
                        p.PersonId, p.ContributedAmount.Amount, p.RemainingAmount.Amount, p.IsTrackingOnly, p.DestinationAccountId)).ToList(),
                    InstallmentGroupId: t.InstallmentGroupId,
                    InstallmentNumber: t.InstallmentNumber,
                    TotalInstallments: t.TotalInstallments,
                    InterestRate: t.InterestRate);
            }).ToList();

            // Reverse for display (newest first) then paginate
            var reversed = Enumerable.Reverse(items).ToList();

            if (request.Page.HasValue)
            {
                var pageSize = request.PageSize ?? 100;
                var page = request.Page.Value;
                transactions = reversed
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToList();
            }
            else
            {
                transactions = reversed;
            }
        }
        else
        {
            var ordered = query
                .OrderByDescending(t => t.Date)
                .ThenBy(t => t.ParentTransactionId != null ? 1 : 0)
                .ThenBy(t => t.ParentTransactionId ?? t.Id)
                .ThenBy(t => t.Type == TransactionType.Loan && t.ParentTransactionId != null ? 0 : 1)
                .ThenBy(t => t.Id);

            var allTransactions = await ordered
                .Include(t => t.Participants)
                .ToListAsync(cancellationToken);

            if (request.Page.HasValue)
            {
                var pageSize = request.PageSize ?? 100;
                var page = request.Page.Value;
                transactions = allTransactions
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .Select(t => ToResponse(t))
                    .ToList();
            }
            else
            {
                transactions = allTransactions
                    .Select(t => ToResponse(t))
                    .ToList();
            }

            await ResolveTransferDestinations(transactions, cancellationToken);
        }

        return new PagedResponse<TransactionResponse>(
            transactions,
            totalCount,
            request.Page ?? 1,
            request.PageSize ?? totalCount);
    }

    private static TransactionResponse ToResponse(Transaction t)
    {
        return new TransactionResponse(
            t.Id, t.AccountId, t.Account.Name.Value,
            t.CategoryId, t.Category?.Name?.Value,
            t.Amount.Amount, t.Amount.Currency, t.ExchangeRate,
            Guard.NormalizeDate(t.Date), t.Notes, t.Type, t.Installments, t.IsInitialBalance,
            null, t.LinkedTransactionId, t.ParentTransactionId,
            null, null,
            t.Participants.Select(p => new TransactionParticipantDto(
                p.PersonId, p.ContributedAmount.Amount, p.RemainingAmount.Amount, p.IsTrackingOnly, p.DestinationAccountId)).ToList(),
            InstallmentGroupId: t.InstallmentGroupId,
            InstallmentNumber: t.InstallmentNumber,
            TotalInstallments: t.TotalInstallments,
            InterestRate: t.InterestRate);
    }

    private async Task ResolveTransferDestinations(List<TransactionResponse> transactions, CancellationToken cancellationToken)
    {
        var linkedIds = transactions
            .Where(t => t.Type == TransactionType.Transfer && t.LinkedTransactionId.HasValue)
            .Select(t => t.LinkedTransactionId!.Value)
            .Distinct()
            .ToList();

        if (linkedIds.Count == 0) return;

        var linkedTransactions = await dbContext.Transactions
            .Include(t => t.Account)
            .Where(t => t.LinkedTransactionId.HasValue && linkedIds.Contains(t.LinkedTransactionId!.Value))
            .ToListAsync(cancellationToken);

        var destLookup = linkedTransactions
            .Where(t => t.Type != TransactionType.Transfer)
            .GroupBy(t => t.LinkedTransactionId!.Value)
            .ToDictionary(g => g.Key, g => g.First());

        for (var i = 0; i < transactions.Count; i++)
        {
            var tx = transactions[i];
            if (tx.Type != TransactionType.Transfer || !tx.LinkedTransactionId.HasValue) continue;
            if (!destLookup.TryGetValue(tx.LinkedTransactionId.Value, out var dest)) continue;

            transactions[i] = tx with
            {
                TransferDestinationAccountId = dest.AccountId,
                TransferDestinationAccountName = dest.Account.Name
            };
        }
    }
}
