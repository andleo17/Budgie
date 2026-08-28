using ErrorOr;
using Mediator;

namespace Budgie.Modules.Finances.Application.Import.CommitMoneyManagerImport;

// Commit transaccional del .mmbak (plan §6): re-parsea y persiste
// Accounts → Categories → Transactions → Recurring de forma idempotente y atómica.
// Application no depende de AspNetCore: se pasa Stream + FileName + ContentType.
public sealed record CommitMoneyManagerImportCommand(
    Stream FileStream,
    string FileName,
    string ContentType,
    bool Overwrite = false) : IRequest<ErrorOr<CommitMoneyManagerImportResponse>>;

public sealed record CommitMoneyManagerImportResponse(
    int CreatedAccounts,
    int SkippedAccounts,
    int CreatedCategories,
    int SkippedCategories,
    int CreatedTransactions,
    int SkippedTransactions,
    int CreatedRecurring,
    List<string> Warnings,
    string FileHash);

// Conteos persistidos en ImportRuns.Counts (jsonb). Tipado para source-gen AOT:
// NO usar tipos anónimos con JsonSerializer (reflexión rompe bajo NativeAOT).
public sealed record ImportRunCounts(
    int CreatedAccounts,
    int CreatedCategories,
    int CreatedTransactions,
    int CreatedRecurring);
