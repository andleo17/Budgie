using System.Data;
using Budgie.Modules.Finances.Domain.Transactions;

namespace Budgie.Modules.Finances.Application;

public interface ITransactionRepository
{
    Task<TransactionInfo?> GetByIdForDeleteAsync(Guid id, Guid userId, CancellationToken ct);
    Task<TransactionForUpdate?> GetByIdForUpdateAsync(Guid id, Guid userId, CancellationToken ct);

    /// <summary>
    /// Returns every leg of a transfer group (outgoing, incoming and optional fee leg)
    /// that share <paramref name="groupId"/> as their <c>LinkedTransactionId</c>.
    /// A transfer is only consistent when read as a whole: the design spec
    /// (docs/superpowers/specs/2026-10-12-transfer-pair-integrity-design.md) defines the
    /// group invariants that update and delete rely on.
    /// </summary>
    Task<List<TransactionForUpdate>> GetTransferGroupAsync(Guid groupId, Guid userId, CancellationToken ct);
    Task<List<TransactionInfo>> GetChildTransactionsForDeleteAsync(Guid parentId, CancellationToken ct);
    Task<Guid?> GetFirstParticipantPersonIdAsync(Guid txId, CancellationToken ct);
    Task DeleteParentParticipantAsync(Guid parentTxId, Guid personId, CancellationToken ct);
    Task AddAsync(Transaction tx, CancellationToken ct, IDbTransaction? transaction = null);
    Task AddRangeAsync(IEnumerable<Transaction> txs, CancellationToken ct, IDbTransaction? transaction = null);
    Task<bool> ExistsByExternalUidAsync(Guid userId, string externalUid, CancellationToken ct);

    /// <summary>
    /// Returns the id of the transaction carrying <paramref name="externalUid"/>, or null
    /// when none exists. Used by EmailImport's second idempotency barrier (spec §3.5): on
    /// a <see cref="Budgie.Shared.Exceptions.UniqueConstraintViolationException"/> the sync
    /// service links the staging row to the
    /// transaction that already exists instead of inventing a duplicate. LIMIT 1 guards
    /// against a hypothetical duplicate the unique index should already prevent.
    /// </summary>
    Task<Guid?> GetIdByExternalUidAsync(Guid userId, string externalUid, CancellationToken ct);
    /// <summary>
    /// Updates the mutable fields of a single transfer leg. Transfer legs are one unit, so
    /// this exists to update both of them consistently rather than rebuilding a domain
    /// entity per leg (spec §Update as a unit).
    /// </summary>
    Task UpdateTransferLegAsync(Guid id, Guid accountId, decimal amount, string currency,
        decimal exchangeRate, DateTime date, string? notes, CancellationToken ct);

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
    Guid AccountId, Guid? ParentTransactionId, Guid? LinkedTransactionId = null);
public record TransactionForUpdate(Guid Id, Shared.Enums.TransactionType Type, bool IsInitialBalance,
    decimal Amount, string Currency, decimal ExchangeRate, int Installments,
    Guid AccountId, Guid? ParentTransactionId, DateTime Date,
    Guid? InstallmentGroupId = null, int InstallmentNumber = 1, int TotalInstallments = 1,
    decimal InterestRate = 0, Guid? LinkedTransactionId = null);
