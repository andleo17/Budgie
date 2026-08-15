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
