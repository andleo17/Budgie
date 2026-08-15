using Budgie.Modules.Finances.Domain.Transactions;

namespace Budgie.Modules.Finances.Application;

public interface ITransactionRepository
{
    Task<TransactionInfo?> GetByIdForDeleteAsync(Guid id, Guid userId, CancellationToken ct);
    Task<TransactionForUpdate?> GetByIdForUpdateAsync(Guid id, Guid userId, CancellationToken ct);
    Task<List<TransactionInfo>> GetChildTransactionsForDeleteAsync(Guid parentId, CancellationToken ct);
    Task<Guid?> GetFirstParticipantPersonIdAsync(Guid txId, CancellationToken ct);
    Task DeleteParentParticipantAsync(Guid parentTxId, Guid personId, CancellationToken ct);
    Task AddAsync(Transaction tx, CancellationToken ct);
    Task UpdateAsync(Transaction tx, CancellationToken ct);
    Task AddParticipantAsync(TransactionParticipant p, CancellationToken ct);
    Task<decimal?> GetParticipantRemainingAsync(Guid participantId, Guid userId, CancellationToken ct);
    Task ReduceParticipantRemainingAsync(Guid participantId, decimal amount, CancellationToken ct);
    Task ReduceParentParticipantRemainingAsync(Guid childParticipantId, decimal amount, CancellationToken ct);
    Task RestoreParticipantRemainingAsync(Guid participantId, decimal amount, CancellationToken ct);
    Task RestoreParentParticipantRemainingAsync(Guid childParticipantId, decimal amount, CancellationToken ct);
    Task AddRepaymentDetailAsync(Guid repaymentTxId, Guid participantId, decimal amount, CancellationToken ct);
    Task<List<RepaymentDetailInfo>> GetRepaymentDetailsAsync(Guid repaymentTxId, CancellationToken ct);
    Task DeleteRepaymentDetailsAsync(Guid repaymentTxId, CancellationToken ct);
    Task DeleteTransactionAsync(Guid id, CancellationToken ct);
    Task DeleteParticipantAsync(Guid txId, CancellationToken ct);
    Task DeleteChildParticipantsAsync(Guid parentTxId, CancellationToken ct);
    Task DeleteChildTransactionsAsync(Guid parentTxId, CancellationToken ct);
    Task<int> GetTransactionCountAsync(Application.Transactions.GetTransactions.GetTransactionsQuery request, CancellationToken ct);
    Task<List<Application.Transactions.TransactionResponse>> GetTransactionsAsync(Application.Transactions.GetTransactions.GetTransactionsQuery request, CancellationToken ct);
    Task AddTrackingParticipant(Guid txId, Guid personId, string currency, CancellationToken ct);
    Task<bool> HasTransactionsBeforeAsync(Guid accountId, DateTime date, CancellationToken ct);
    Task<List<Application.Debts.GetPersonDebts.DebtResponse>> GetPersonDebtsAsync(Guid personId, CancellationToken ct);
    Task<Application.Persons.GetPersonSummary.PersonSummaryResponse> GetPersonSummaryAsync(Guid personId, CancellationToken ct);
}

public record RepaymentDetailInfo(Guid RepaidParticipantId, decimal Amount);
public record TransactionInfo(Guid Id, Shared.Enums.TransactionType Type, bool IsInitialBalance,
    decimal Amount, string Currency, decimal ExchangeRate, int Installments,
    Guid AccountId, Guid? ParentTransactionId);
public record TransactionForUpdate(Guid Id, Shared.Enums.TransactionType Type, bool IsInitialBalance,
    decimal Amount, string Currency, decimal ExchangeRate, int Installments,
    Guid AccountId, Guid? ParentTransactionId, DateTime Date);
