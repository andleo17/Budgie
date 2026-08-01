using Budgie.Modules.Finances.Application.Transactions.CreateTransaction;
using Budgie.Shared.Enums;

namespace Budgie.Modules.Finances.Application.Transactions;

public record TransactionResponse(
    Guid Id,
    Guid AccountId,
    string AccountName,
    Guid? CategoryId,
    string? CategoryName,
    decimal Amount,
    string Currency,
    decimal ExchangeRate,
    DateTime Date,
    string? Notes,
    TransactionType Type,
    int Installments,
    bool IsInitialBalance,
    decimal? RunningBalance = null,
    Guid? LinkedTransactionId = null,
    Guid? ParentTransactionId = null,
    Guid? TransferDestinationAccountId = null,
    string? TransferDestinationAccountName = null,
    List<TransactionParticipantDto>? Participants = null,
    Guid? InstallmentGroupId = null,
    int InstallmentNumber = 1,
    int TotalInstallments = 1,
    decimal InterestRate = 0);