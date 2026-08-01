using Budgie.Shared.Enums;

namespace Budgie.Shared.Abstractions;

public interface IUserSettingsProvider
{
    Task<bool> IsConfiguredAsync();
    Task<string> GetMainCurrencyAsync();
    Task<CreditConsumptionMode> GetDefaultCreditConsumptionModeAsync();
    Task<string> GetLanguageAsync();
}
