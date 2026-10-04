using Budgie.Modules.EmailImport.Domain.Connections;

namespace Budgie.Modules.EmailImport.Application.Parsing;

/// <summary>
/// The single source of truth for the confidence weights (spec §4.5). They live here as
/// constants so a test can pin the expected confidence arithmetic and so nobody has to hunt
/// for magic numbers scattered through the adapter: change a weight here and every score,
/// threshold example and explanation follows.
/// </summary>
/// <remarks>
/// The weights sum to 1.00, so a perfect parse scores exactly 1.0 and a missing field costs
/// exactly its weight. <see cref="DefaultAutoCommitThreshold"/> is a REFERENCE to
/// <c>EmailConnection.DefaultAutoCommitMinConfidence</c> (Domain), not an independent literal:
/// Domain cannot depend on Application, so the constant could not point the other way and the
/// domain copy is the single source of truth. The DDL's <c>DEFAULT 0.85</c> in
/// <c>003_email_import.sql</c> is the third copy (DDL cannot reference C#); it is pinned to the
/// same constant by <c>AutoCommitThresholdConsistencyTests</c>, so the three copies cannot
/// drift silently. Each connection stores its own copy of the value because the user can tune
/// it per mailbox.
/// </remarks>
public static class ConfidenceWeights
{
    /// <summary>Weight of the extracted amount. Highest weight: it is the field that creates the transaction.</summary>
    public const decimal Amount = 0.35m;

    /// <summary>Weight of the transaction direction (Income vs Expense).</summary>
    public const decimal Direction = 0.20m;

    /// <summary>Weight of the merchant/payee.</summary>
    public const decimal Merchant = 0.20m;

    /// <summary>Weight of the operation date.</summary>
    public const decimal Date = 0.15m;

    /// <summary>Weight of the currency symbol vs the rule's declared currency.</summary>
    public const decimal Currency = 0.10m;

    /// <summary>
    /// Default minimum confidence for auto-commit. This is a REFERENCE to
    /// <see cref="EmailConnection.DefaultAutoCommitMinConfidence"/>, not a second literal: the
    /// domain constant is the single source of truth (Domain may not depend on Application, so
    /// the constant cannot point the other way), and a const-to-const reference keeps this
    /// Application-facing name in sync at compile time with zero runtime cost. Whoever changes
    /// the default changes it in Domain — and the DDL copy is caught by
    /// <c>AutoCommitThresholdConsistencyTests</c>.
    /// </summary>
    public const decimal DefaultAutoCommitThreshold = EmailConnection.DefaultAutoCommitMinConfidence;
}
