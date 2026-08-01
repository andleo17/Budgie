using Budgie.Modules.Finances.Domain.Accounts;
using Budgie.Shared.Domain.ValueObjects;
using Budgie.Shared.Enums;
using Budgie.Modules.Finances.Domain.Transactions;
using FluentAssertions;
using Xunit;

namespace Budgie.Modules.Finances.UnitTests.Domain.Accounts;

public class AccountExtendedTests
{
    [Fact]
    public void Create_ShouldThrow_WhenNameIsEmpty()
    {
        var act = () => Account.Create(Guid.NewGuid(), "", "USD", 100m);
        act.Should().Throw<ArgumentException>().WithMessage("Name cannot be null or empty.*");
    }

    [Fact]
    public void Create_ShouldThrow_WhenCurrencyIsInvalid()
    {
        var act = () => Account.Create(Guid.NewGuid(), "Account", "US", 100m);
        act.Should().Throw<ArgumentException>().WithMessage("Currency code must be exactly 3 characters.*");
    }

    [Fact]
    public void Create_ShouldThrow_WhenInitialBalanceIsNegative()
    {
        var act = () => Account.Create(Guid.NewGuid(), "Account", "USD", -10m);
        act.Should().Throw<ArgumentException>().WithMessage("Amount cannot be negative.*");
    }

    [Fact]
    public void Create_ShouldThrow_WhenTypeIsCreditAndCreditCardSettingsIsNull()
    {
        var act = () => Account.Create(Guid.NewGuid(), "Credit Card", "USD", 0m, AccountType.Credit, null);
        act.Should().Throw<ArgumentException>().WithMessage("Credit card settings are required for credit accounts.*");
    }

    [Fact]
    public void Create_ShouldNormalizeDate()
    {
        var createdAt = DateTime.UtcNow;
        var account = Account.Create(Guid.NewGuid(), "Account", "USD", 0m, createdAt: createdAt);
        account.CreatedAt.Kind.Should().Be(DateTimeKind.Unspecified);
    }

    [Fact]
    public void Create_ShouldTrimName()
    {
        var account = Account.Create(Guid.NewGuid(), "  My Account  ", "USD", 0m);
        account.Name.Value.Should().Be("My Account");
    }

    [Fact]
    public void Update_ShouldUpdateNameAndSettings()
    {
        var account = Account.Create(Guid.NewGuid(), "Old Name", "USD", 0m);
        var newSettings = new CreditCardSettings(5000m, CreditConsumptionMode.Total, 1, 15, 0, true, CurrencyCode.From("USD"));

        account.Update("New Name", newSettings);

        account.Name.Value.Should().Be("New Name");
        account.CreditCardSettings.Should().Be(newSettings);
    }

    [Fact]
    public void ApplyTransaction_ShouldIncreaseBalance_WhenIncome()
    {
        var account = Account.Create(Guid.NewGuid(), "Checking", "USD", 500m);
        account.ApplyTransaction(Money.FromPositive(200m, CurrencyCode.From("USD")), 1m, 1, TransactionType.Income);
        account.Balance.Amount.Should().Be(700m);
    }

    [Fact]
    public void ApplyTransaction_ShouldDecreaseBalance_WhenExpense()
    {
        var account = Account.Create(Guid.NewGuid(), "Checking", "USD", 500m);
        account.ApplyTransaction(Money.FromPositive(150m, CurrencyCode.From("USD")), 1m, 1, TransactionType.Expense);
        account.Balance.Amount.Should().Be(350m);
    }

    [Fact]
    public void ApplyTransaction_ShouldDecreaseBalance_WhenTransfer()
    {
        var account = Account.Create(Guid.NewGuid(), "Checking", "USD", 500m);
        account.ApplyTransaction(Money.FromPositive(200m, CurrencyCode.From("USD")), 1m, 1, TransactionType.Transfer);
        account.Balance.Amount.Should().Be(300m);
    }

    [Fact]
    public void ApplyTransaction_ShouldDecreaseBalance_WhenLoan()
    {
        var account = Account.Create(Guid.NewGuid(), "Loan Account", "USD", 1000m);
        account.ApplyTransaction(Money.FromPositive(300m, CurrencyCode.From("USD")), 1m, 1, TransactionType.Loan);
        account.Balance.Amount.Should().Be(700m);
    }

    [Fact]
    public void ApplyTransaction_ShouldIncreaseBalance_WhenRepayment()
    {
        var account = Account.Create(Guid.NewGuid(), "Loan Account", "USD", 700m);
        account.ApplyTransaction(Money.FromPositive(300m, CurrencyCode.From("USD")), 1m, 1, TransactionType.Repayment);
        account.Balance.Amount.Should().Be(1000m);
    }

    [Fact]
    public void ApplyTransaction_ShouldHandleForeignCurrency()
    {
        var account = Account.Create(Guid.NewGuid(), "Savings PEN", "PEN", 1000m);
        account.ApplyTransaction(Money.FromPositive(100m, CurrencyCode.From("USD")), 3.8m, 1, TransactionType.Expense);
        account.Balance.Amount.Should().Be(620m);
        account.Balance.Currency.Code.Should().Be("PEN");
    }

    [Fact]
    public void RevertTransaction_ShouldRestoreOriginalBalance_WhenRevertingIncome()
    {
        var account = Account.Create(Guid.NewGuid(), "Checking", "USD", 500m);
        account.ApplyTransaction(Money.FromPositive(200m, CurrencyCode.From("USD")), 1m, 1, TransactionType.Income);
        account.RevertTransaction(Money.FromPositive(200m, CurrencyCode.From("USD")), 1m, 1, TransactionType.Income);
        account.Balance.Amount.Should().Be(500m);
    }

    [Fact]
    public void RevertTransaction_ShouldRestoreOriginalBalance_WhenRevertingExpense()
    {
        var account = Account.Create(Guid.NewGuid(), "Checking", "USD", 500m);
        account.ApplyTransaction(Money.FromPositive(150m, CurrencyCode.From("USD")), 1m, 1, TransactionType.Expense);
        account.RevertTransaction(Money.FromPositive(150m, CurrencyCode.From("USD")), 1m, 1, TransactionType.Expense);
        account.Balance.Amount.Should().Be(500m);
    }

    [Fact]
    public void RevertTransaction_ShouldRestoreOriginalBalance_WhenRevertingTransfer()
    {
        var account = Account.Create(Guid.NewGuid(), "Checking", "USD", 500m);
        account.ApplyTransaction(Money.FromPositive(200m, CurrencyCode.From("USD")), 1m, 1, TransactionType.Transfer);
        account.RevertTransaction(Money.FromPositive(200m, CurrencyCode.From("USD")), 1m, 1, TransactionType.Transfer);
        account.Balance.Amount.Should().Be(500m);
    }

    [Fact]
    public void RevertTransaction_ShouldRestoreOriginalBalance_WhenRevertingLoan()
    {
        var account = Account.Create(Guid.NewGuid(), "Loan Account", "USD", 1000m);
        account.ApplyTransaction(Money.FromPositive(300m, CurrencyCode.From("USD")), 1m, 1, TransactionType.Loan);
        account.RevertTransaction(Money.FromPositive(300m, CurrencyCode.From("USD")), 1m, 1, TransactionType.Loan);
        account.Balance.Amount.Should().Be(1000m);
    }

    [Fact]
    public void RevertTransaction_ShouldRestoreOriginalBalance_WhenRevertingRepayment()
    {
        var account = Account.Create(Guid.NewGuid(), "Loan Account", "USD", 700m);
        account.ApplyTransaction(Money.FromPositive(300m, CurrencyCode.From("USD")), 1m, 1, TransactionType.Repayment);
        account.RevertTransaction(Money.FromPositive(300m, CurrencyCode.From("USD")), 1m, 1, TransactionType.Repayment);
        account.Balance.Amount.Should().Be(700m);
    }

    [Fact]
    public void RevertTransaction_ShouldHandleForeignCurrency()
    {
        var account = Account.Create(Guid.NewGuid(), "Savings PEN", "PEN", 1000m);
        account.ApplyTransaction(Money.FromPositive(100m, CurrencyCode.From("USD")), 3.8m, 1, TransactionType.Expense);
        account.RevertTransaction(Money.FromPositive(100m, CurrencyCode.From("USD")), 3.8m, 1, TransactionType.Expense);
        account.Balance.Amount.Should().Be(1000m);
    }

    [Fact]
    public void CalculateInterest_ShouldReturnZero_WhenNoCreditCardSettings()
    {
        var account = Account.Create(Guid.NewGuid(), "Checking", "USD", 1000m);
        var interest = account.CalculateInterest(Money.FromNonNegative(500m, CurrencyCode.From("USD")), 30);
        interest.Amount.Should().Be(0);
    }

    [Fact]
    public void CalculateInterest_ShouldReturnZero_WhenAprIsZero()
    {
        var settings = new CreditCardSettings(5000m, CreditConsumptionMode.Total, 1, 15, 0, true, CurrencyCode.From("USD"));
        var account = Account.Create(Guid.NewGuid(), "Credit Card", "USD", 0m, AccountType.Credit, settings);
        var interest = account.CalculateInterest(Money.FromNonNegative(1000m, CurrencyCode.From("USD")), 30);
        interest.Amount.Should().Be(0);
    }

    [Fact]
    public void CalculateInterest_ShouldCalculateCorrectly_WhenAprIsPositive()
    {
        var settings = new CreditCardSettings(5000m, CreditConsumptionMode.Total, 1, 15, 36.5m, true, CurrencyCode.From("USD"));
        var account = Account.Create(Guid.NewGuid(), "Credit Card", "USD", 0m, AccountType.Credit, settings);
        var interest = account.CalculateInterest(Money.FromNonNegative(1000m, CurrencyCode.From("USD")), 30);
        interest.Amount.Should().Be(30m);
    }
}

public class CreditCardSettingsTests
{
    [Fact]
    public void Constructor_ShouldThrow_WhenBillingCycleDayIsLessThan1()
    {
        var act = () => new CreditCardSettings(5000m, CreditConsumptionMode.Total, 0, 15, 0, true, CurrencyCode.From("USD"));
        act.Should().Throw<ArgumentException>().WithMessage("Billing cycle day must be between 1 and 31.*");
    }

    [Fact]
    public void Constructor_ShouldThrow_WhenBillingCycleDayIsGreaterThan31()
    {
        var act = () => new CreditCardSettings(5000m, CreditConsumptionMode.Total, 32, 15, 0, true, CurrencyCode.From("USD"));
        act.Should().Throw<ArgumentException>().WithMessage("Billing cycle day must be between 1 and 31.*");
    }

    [Fact]
    public void Constructor_ShouldThrow_WhenPaymentDueDayIsLessThan1()
    {
        var act = () => new CreditCardSettings(5000m, CreditConsumptionMode.Total, 1, 0, 0, true, CurrencyCode.From("USD"));
        act.Should().Throw<ArgumentException>().WithMessage("Payment due day must be between 1 and 31.*");
    }

    [Fact]
    public void Constructor_ShouldThrow_WhenPaymentDueDayIsGreaterThan31()
    {
        var act = () => new CreditCardSettings(5000m, CreditConsumptionMode.Total, 1, 32, 0, true, CurrencyCode.From("USD"));
        act.Should().Throw<ArgumentException>().WithMessage("Payment due day must be between 1 and 31.*");
    }

    [Fact]
    public void GetBillingPeriod_ShouldReturnNull_WhenNoCycleDay()
    {
        var settings = new CreditCardSettings(5000m, CreditConsumptionMode.Total, null, null, 0, true, CurrencyCode.From("USD"));
        var period = settings.GetBillingPeriod(DateTime.UtcNow);
        period.Should().BeNull();
    }

    [Fact]
    public void GetBillingPeriod_ShouldReturnCorrectPeriod()
    {
        var settings = new CreditCardSettings(5000m, CreditConsumptionMode.Total, 15, 10, 0, true, CurrencyCode.From("USD"));
        var asOf = new DateTime(2025, 6, 20, 0, 0, 0, DateTimeKind.Unspecified);
        var period = settings.GetBillingPeriod(asOf);
        period.Should().NotBeNull();
        period!.Value.Start.Should().Be(new DateTime(2025, 5, 16, 0, 0, 0, DateTimeKind.Unspecified));
        period.Value.End.Should().Be(new DateTime(2025, 6, 15, 0, 0, 0, DateTimeKind.Unspecified));
    }

    [Fact]
    public void CalculateInterest_ShouldReturnCorrectValue()
    {
        var settings = new CreditCardSettings(5000m, CreditConsumptionMode.Total, 1, 15, 36.5m, true, CurrencyCode.From("USD"));
        var balance = Money.FromNonNegative(1000m, CurrencyCode.From("USD"));
        var interest = settings.CalculateInterest(balance, 30);
        interest.Amount.Should().Be(30m);
        interest.Currency.Code.Should().Be("USD");
    }
}
