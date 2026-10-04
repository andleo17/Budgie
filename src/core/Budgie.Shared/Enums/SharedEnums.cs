namespace Budgie.Shared.Enums;

public enum UserRole
{
    Admin = 1,
    User = 2
}

public enum AccountType
{
    Checking = 0,
    Savings = 1,
    Credit = 2,
    Cash = 3,
    Debt = 4
}

public enum CreditConsumptionMode
{
    Total,
    Partial
}

public enum InterestCalculationMethod
{
    FrenchAmortization,
    SimpleInterest
}

public enum TransactionType
{
    Income,
    Expense,
    Transfer,
    Loan,
    Repayment
}

public enum RecurringFrequency
{
    Daily,
    Weekly,
    Biweekly,
    Monthly,
    Yearly,
    Custom
}

public enum RecurringApplicability
{
    Template,
    Automatic,
    Manual
}

public enum RecurringEndType
{
    Never,
    AfterCount,
    OnDate
}

/// <summary>
/// Email providers Budgie can read transaction notifications from.
/// Persisted and shared by Infrastructure, Presentation and configuration, so it lives here
/// rather than next to its only use. Adding a member is additive and needs no migration:
/// the provider implementation resolves through <c>IEmailProviderResolver</c>.
/// </summary>
public enum EmailProvider
{
    Gmail = 0
}

/// <summary>
/// Lifecycle of a staged email transaction (EmailImport review inbox). Persisted as int
/// (<c>PendingEmailTransactions."Status"</c>) and bound from Presentation
/// (<c>?status=</c> on <c>GET /inbox</c>), so it lives here next to
/// <see cref="EmailProvider"/> and <see cref="TransactionType"/> rather than next to its
/// domain entity.
/// </summary>
/// <remarks>
/// The numeric values are part of the persistence AND wire contract: rows written with one
/// numbering must keep reading correctly forever. They are written explicitly on purpose —
/// reordering this enum silently corrupts every stored row without failing a single test.
/// </remarks>
public enum PendingEmailStatus
{
    /// <summary>Awaiting review or auto-commit.</summary>
    Pending = 0,
    /// <summary>Already committed to Budgie (manually or through auto-commit).</summary>
    Committed = 1,
    /// <summary>Discarded by the user. The row is kept so its MessageId keeps blocking re-import.</summary>
    Dismissed = 2,
    /// <summary>Parse or commit failed. The row's FailureReason explains why.</summary>
    Failed = 3
}
