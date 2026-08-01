using Budgie.Modules.Finances.Domain.Accounts;
using Budgie.Modules.Finances.Domain.Goals;
using Budgie.Shared.Domain.ValueObjects;
using Budgie.Shared.Enums;
using Budgie.Modules.Finances.Domain.Transactions;
using FluentAssertions;
using Xunit;

namespace Budgie.Modules.Finances.UnitTests.Domain.Accounts;

public class AccountTests
{
    [Fact]
    public void Create_ShouldInitializePropertiesCorrectly()
    {
        // Arrange
        var name = "Savings";
        var currency = "USD";
        var initialBalance = 100m;

        // Act
        var account = Account.Create(Guid.NewGuid(), name, currency, initialBalance);

        // Assert
        account.Id.Should().NotBeEmpty();
        account.Name.Value.Should().Be(name);
        account.Balance.Currency.Code.Should().Be(currency);
        account.Balance.Amount.Should().Be(initialBalance);
        account.Type.Should().Be(AccountType.Checking);
    }

    [Fact]
    public void Create_ShouldInitializeGoalProperties_WhenProvided()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var accountId = Guid.NewGuid();
        var targetAmount = 5000m;
        var targetDate = DateTime.UtcNow.AddMonths(6);

        // Act
        var goal = Goal.Create(userId, accountId, targetAmount, "USD", targetDate);

        // Assert
        goal.Id.Should().NotBeEmpty();
        goal.AccountId.Should().Be(accountId);
        goal.TargetAmount.Amount.Should().Be(targetAmount);
        goal.TargetDate.Should().Be(targetDate);
    }

    [Fact]
    public void ApplyTransaction_ShouldDeductFullAmount_WhenCreditModeIsTotal()
    {
        // Arrange
        var initialBalance = 1000m;
        var creditLimit = 1000m;
        var creditCardSettings = new CreditCardSettings(creditLimit, CreditConsumptionMode.Total, null, null, 0, true, CurrencyCode.From("USD"));
        var account = Account.Create(Guid.NewGuid(), "Credit Card", "USD", initialBalance, AccountType.Credit, creditCardSettings);
        
        // Act
        account.ApplyTransaction(Money.FromPositive(300m, CurrencyCode.From("USD")), 1.0m, 3, TransactionType.Expense);

        // Assert
        account.Balance.Amount.Should().Be(700m); // 1000 - 300
    }

    [Fact]
    public void ApplyTransaction_ShouldDeductPartialAmount_WhenCreditModeIsPartial()
    {
        // Arrange
        var initialBalance = 1000m;
        var creditLimit = 1000m;
        var creditCardSettings = new CreditCardSettings(creditLimit, CreditConsumptionMode.Partial, 1, 1, 0, true, CurrencyCode.From("USD"));
        var account = Account.Create(Guid.NewGuid(), "Credit Card", "USD", initialBalance, AccountType.Credit, creditCardSettings);
        
        // Act
        account.ApplyTransaction(Money.FromPositive(300m, CurrencyCode.From("USD")), 1.0m, 3, TransactionType.Expense);

        // Assert
        account.Balance.Amount.Should().Be(900m); // 1000 - (300/3)
    }

    [Fact]
    public void ApplyTransaction_ShouldConvertCurrency_WhenCurrenciesAreDifferent()
    {
        // Arrange
        var account = Account.Create(Guid.NewGuid(), "Savings PEN", "PEN", 1000m);
        
        // Act
        account.ApplyTransaction(Money.FromPositive(100m, CurrencyCode.From("USD")), 3.8m, 1, TransactionType.Expense);

        // Assert
        account.Balance.Amount.Should().Be(620m); // 1000 - (100 * 3.8)
    }

    [Fact]
    public void RevertTransaction_ShouldIncreaseBalance_WhenRevertingExpense()
    {
        // Arrange
        var initialBalance = 1000m;
        var account = Account.Create(Guid.NewGuid(), "Savings", "USD", initialBalance);
        account.ApplyTransaction(Money.FromPositive(200m, CurrencyCode.From("USD")), 1.0m, 1, TransactionType.Expense); // Balance = 800

        // Act
        account.RevertTransaction(Money.FromPositive(200m, CurrencyCode.From("USD")), 1.0m, 1, TransactionType.Expense);

        // Assert
        account.Balance.Amount.Should().Be(initialBalance);
    }

    [Fact]
    public void RevertTransaction_ShouldDeductBalance_WhenRevertingIncome()
    {
        // Arrange
        var initialBalance = 1000m;
        var account = Account.Create(Guid.NewGuid(), "Savings", "USD", initialBalance);
        account.ApplyTransaction(Money.FromPositive(500m, CurrencyCode.From("USD")), 1.0m, 1, TransactionType.Income); // Balance = 1500

        // Act
        account.RevertTransaction(Money.FromPositive(500m, CurrencyCode.From("USD")), 1.0m, 1, TransactionType.Income);

        // Assert
        account.Balance.Amount.Should().Be(initialBalance);
    }

    [Fact]
    public void RevertTransaction_ShouldHandleCurrencyConversion()
    {
        // Arrange
        var account = Account.Create(Guid.NewGuid(), "Savings PEN", "PEN", 1000m);
        account.ApplyTransaction(Money.FromPositive(100m, CurrencyCode.From("USD")), 3.8m, 1, TransactionType.Expense); // Balance = 620

        // Act
        account.RevertTransaction(Money.FromPositive(100m, CurrencyCode.From("USD")), 3.8m, 1, TransactionType.Expense);

        // Assert
        account.Balance.Amount.Should().Be(1000m);
    }
}
