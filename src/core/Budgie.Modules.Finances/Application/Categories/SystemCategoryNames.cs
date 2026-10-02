using Budgie.Modules.Finances.Domain.Categories;

namespace Budgie.Modules.Finances.Application.Categories;

/// <summary>
/// Localized display names for the global system categories (UserId NULL, IsSystem=true,
/// shared by all users — see SystemCategorySeeder). The DB rows keep their canonical
/// English names; this resolves the label at read time from the user's language (es/en).
/// Pure and I/O-free so it stays trivially testable.
/// </summary>
public static class SystemCategoryNames
{
    // English values must match the names seeded by SystemCategorySeeder.
    private static readonly IReadOnlyDictionary<Guid, (string Es, string En)> Names =
        new Dictionary<Guid, (string Es, string En)>
        {
            [SystemCategoryIds.Transfer] = ("Transferencia", "Transfer"),
            [SystemCategoryIds.TransferFee] = ("Comisión de transferencia", "Transfer Fee"),
            [SystemCategoryIds.LoanRepayment] = ("Reembolso de préstamo", "Loan Repayment"),
            [SystemCategoryIds.Adjustment] = ("Ajuste", "Adjustment"),
            [SystemCategoryIds.Interest] = ("Interés", "Interest"),
            [SystemCategoryIds.InitialBalance] = ("Saldo inicial", "Initial Balance"),
        };

    /// <summary>
    /// Returns the display name for a category: the localized system name when the id is a
    /// system category and the language is Spanish; the canonical English name for system
    /// categories in any other language; <paramref name="dbName"/> unchanged for
    /// non-system categories (including null ids/names).
    /// </summary>
    public static string? Resolve(Guid? categoryId, string? dbName, string? language)
    {
        if (!IsSystemCategory(categoryId) || !Names.TryGetValue(categoryId!.Value, out var names))
            return dbName;

        return IsSpanish(language) ? names.Es : names.En;
    }

    /// <summary>True when the id belongs to one of the seeded system categories.</summary>
    public static bool IsSystemCategory(Guid? categoryId)
        => categoryId is { } id && Names.ContainsKey(id);

    // Solo es/en: 'es' o cualquier variante (es-MX, es-419) → español; el resto → inglés.
    private static bool IsSpanish(string? language)
        => !string.IsNullOrWhiteSpace(language) &&
           language.StartsWith("es", StringComparison.OrdinalIgnoreCase);
}
