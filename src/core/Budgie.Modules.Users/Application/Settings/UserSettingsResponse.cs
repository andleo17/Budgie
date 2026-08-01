using Budgie.Shared.Enums;

namespace Budgie.Modules.Users.Application.Settings;

public record UserSettingsResponse(
    string MainCurrency,
    CreditConsumptionMode DefaultCreditConsumptionMode,
    string Language,
    bool IsConfigured,
    List<string> SupportedCurrencies,
    int TransactionPageSize = 100,
    RecurringApplicability DefaultRecurringApplicability = RecurringApplicability.Automatic,
    bool AutoApplyOnLoad = true);
