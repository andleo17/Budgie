using Budgie.Modules.Finances.Domain.Categories;
using Budgie.Shared.Infrastructure;

namespace Budgie.Modules.Finances.Infrastructure.Database;

/// <summary>
/// Seeds the system categories used internally by the app (repayments, transfers,
/// initial balance, etc.) plus the default user-facing categories.
/// The internal category IDs come from SystemCategoryIds so FK references resolve.
/// </summary>
public static class SystemCategorySeeder
{
    public static async Task SeedAsync(IDbConnectionFactory factory, CancellationToken ct = default)
    {
        var exists = await factory.QuerySingleAsync<bool>(
            @"SELECT EXISTS(SELECT 1 FROM ""Categories"" WHERE ""IsSystem"" = TRUE)", null, ct);

        if (exists) return;

        var systemCategories = new (Guid Id, string Name, int Type)[]
        {
            // Internal categories — IDs must match SystemCategoryIds
            (SystemCategoryIds.Transfer, "Transfer", 2),
            (SystemCategoryIds.TransferFee, "Transfer Fee", 2),
            (SystemCategoryIds.LoanRepayment, "Loan Repayment", 0),
            (SystemCategoryIds.Adjustment, "Adjustment", 2),
            (SystemCategoryIds.Interest, "Interest", 1),
            (SystemCategoryIds.InitialBalance, "Initial Balance", 3),
            // User-facing default categories
            (Guid.Parse("b0000000-0000-0000-0000-000000000001"), "Food", 1),
            (Guid.Parse("b0000000-0000-0000-0000-000000000002"), "Transport", 1),
            (Guid.Parse("b0000000-0000-0000-0000-000000000003"), "Housing", 1),
            (Guid.Parse("b0000000-0000-0000-0000-000000000004"), "Health", 1),
            (Guid.Parse("b0000000-0000-0000-0000-000000000005"), "Entertainment", 1),
            (Guid.Parse("b0000000-0000-0000-0000-000000000006"), "Education", 1),
            (Guid.Parse("b0000000-0000-0000-0000-000000000007"), "Bills", 1),
        };

        foreach (var cat in systemCategories)
        {
            await factory.ExecuteAsync(
                @"INSERT INTO ""Categories"" (""Id"", ""Name"", ""Type"", ""IsSystem"")
                  VALUES (@Id, @Name, @Type, TRUE)
                  ON CONFLICT DO NOTHING",
                new { cat.Id, cat.Name, cat.Type }, ct);
        }
    }
}
