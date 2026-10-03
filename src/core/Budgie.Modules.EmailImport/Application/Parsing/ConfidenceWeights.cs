namespace Budgie.Modules.EmailImport.Application.Parsing;

/// <summary>
/// The single source of truth for the confidence weights (spec §4.5). They live here as
/// constants so a test can pin the expected confidence arithmetic and so nobody has to hunt
/// for magic numbers scattered through the adapter: change a weight here and every score,
/// threshold example and explanation follows.
/// </summary>
/// <remarks>
/// The weights sum to 1.00, so a perfect parse scores exactly 1.0 and a missing field costs
/// exactly its weight. <see cref="DefaultAutoCommitThreshold"/> mirrors
/// <c>EmailConnection.AutoCommitMinConfidence</c>'s default; the connection stores its own copy
/// because each user can tune it.
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

    /// <summary>Default minimum confidence for auto-commit, mirrored by <c>EmailConnection.AutoCommitMinConfidence</c>.</summary>
    public const decimal DefaultAutoCommitThreshold = 0.85m;
}
