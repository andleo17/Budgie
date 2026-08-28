using ErrorOr;
using Mediator;

namespace Budgie.Modules.Finances.Application.Import.PreviewMoneyManagerImport;

// Dry-run Preview del .mmbak — valida y reporta sin escribir en DB (plan §5).
// Application no depende de AspNetCore: se pasa Stream + FileName + ContentType.
public sealed record PreviewMoneyManagerImportCommand(
    Stream FileStream,
    string FileName,
    string ContentType) : IRequest<ErrorOr<PreviewMoneyManagerImportResponse>>;

public sealed record PreviewCurrency(string Iso, decimal Rate, bool IsMain);

public sealed record PreviewAccount(string Name, string Type, string Currency, string ExternalUid);

public sealed record PreviewCategory(string Name, string Type, string? ParentUid, string ExternalUid, decimal? MonthlyBudget);

public sealed record PreviewTransactionsSummary(
    int Income,
    int Expense,
    int Transfer,
    int InitialBalance,
    int Total,
    int DedupedTransfers,
    string DateRange);

public sealed record PreviewBudget(string CategoryUid, decimal Amount);

public sealed record PreviewRecurring(string Frequency, string? Notes);

public sealed record PreviewMoneyManagerImportResponse(
    List<PreviewCurrency> Currencies,
    List<PreviewAccount> Accounts,
    List<PreviewCategory> Categories,
    PreviewTransactionsSummary Summary,
    List<PreviewBudget> Budgets,
    List<PreviewRecurring> Recurrings,
    List<string> Warnings,
    List<string> Errors,
    int SkippedPlaceholders,
    string FileHash,
    bool AlreadyImported);
