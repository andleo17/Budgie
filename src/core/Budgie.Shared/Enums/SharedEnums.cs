namespace Budgie.Shared.Enums;

public enum AccountType
{
    Checking,
    Savings,
    Credit
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
