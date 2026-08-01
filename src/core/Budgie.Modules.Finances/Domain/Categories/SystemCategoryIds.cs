namespace Budgie.Modules.Finances.Domain.Categories;

public static class SystemCategoryIds
{
    public static readonly Guid Transfer = Guid.Parse("A1000000-0000-0000-0000-000000000001");
    public static readonly Guid TransferFee = Guid.Parse("A1000000-0000-0000-0000-000000000002");
    public static readonly Guid LoanRepayment = Guid.Parse("A1000000-0000-0000-0000-000000000003");
    public static readonly Guid Adjustment = Guid.Parse("A1000000-0000-0000-0000-000000000004");
    public static readonly Guid Interest = Guid.Parse("A1000000-0000-0000-0000-000000000005");
    public static readonly Guid InitialBalance = Guid.Parse("A1000000-0000-0000-0000-000000000006");
}
