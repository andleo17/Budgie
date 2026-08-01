using Budgie.Shared.Abstractions;
using Budgie.Shared.Domain.ValueObjects;
using Budgie.Shared.Enums;

namespace Budgie.Modules.Users.Domain;

public class UserSettings : Entity
{
    public Guid UserId { get; private set; }
    public CurrencyCode MainCurrency { get; private set; } = CurrencyCode.From("USD");
    public CreditConsumptionMode DefaultCreditConsumptionMode { get; private set; } = CreditConsumptionMode.Total;
    public string Language { get; private set; } = "en";
    public bool IsConfigured { get; private set; }
    public List<string> SupportedCurrencies { get; private set; } = [];
    public int TransactionPageSize { get; private set; } = 100;
    public RecurringApplicability DefaultRecurringApplicability { get; private set; } = RecurringApplicability.Automatic;
    public bool AutoApplyOnLoad { get; private set; } = true;

    private UserSettings() { }

    public static UserSettings Create(Guid userId)
    {
        return new UserSettings
        {
            UserId = userId,
            IsConfigured = false,
            SupportedCurrencies = ["USD"]
        };
    }

    public void Update(string mainCurrency, CreditConsumptionMode creditConsumptionMode,
        List<string>? supportedCurrencies = null, string? language = null,
        int? transactionPageSize = null,
        RecurringApplicability? defaultRecurringApplicability = null,
        bool? autoApplyOnLoad = null)
    {
        if (supportedCurrencies is not null && supportedCurrencies.Count == 0)
            throw new ArgumentException("Supported currencies must contain at least one currency.", nameof(supportedCurrencies));

        if (transactionPageSize is <= 0)
            throw new ArgumentException("Transaction page size must be positive.", nameof(transactionPageSize));

        MainCurrency = CurrencyCode.From(mainCurrency);
        DefaultCreditConsumptionMode = creditConsumptionMode;
        if (supportedCurrencies is not null)
            SupportedCurrencies = supportedCurrencies.Select(c => c.ToUpperInvariant()).ToList();
        else if (!SupportedCurrencies.Contains(mainCurrency.ToUpperInvariant()))
            SupportedCurrencies.Add(mainCurrency.ToUpperInvariant());
        if (language is not null)
        {
            Guard.NotNullOrEmpty(language, nameof(language));
            Language = language.ToLowerInvariant();
        }
        if (transactionPageSize.HasValue)
            TransactionPageSize = transactionPageSize.Value;
        if (defaultRecurringApplicability.HasValue)
            DefaultRecurringApplicability = defaultRecurringApplicability.Value;
        if (autoApplyOnLoad.HasValue)
            AutoApplyOnLoad = autoApplyOnLoad.Value;
        IsConfigured = true;
    }
}
