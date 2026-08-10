using Budgie.Modules.Users.Application;
using Budgie.Shared.Abstractions;
using Budgie.Shared.Enums;

namespace Budgie.Modules.Users.Infrastructure;

internal sealed class UserSettingsProvider(
    IUserSettingsRepository settingsRepository,
    ICurrentUserProvider currentUserProvider) : IUserSettingsProvider
{
    public async Task<bool> IsConfiguredAsync()
    {
        var settings = await settingsRepository.GetByUserIdAsync(currentUserProvider.GetUserId(), CancellationToken.None);
        return settings?.IsConfigured ?? false;
    }

    public async Task<string> GetMainCurrencyAsync()
        => (await settingsRepository.GetByUserIdAsync(currentUserProvider.GetUserId(), CancellationToken.None))?.MainCurrency ?? "USD";

    public async Task<CreditConsumptionMode> GetDefaultCreditConsumptionModeAsync()
        => (await settingsRepository.GetByUserIdAsync(currentUserProvider.GetUserId(), CancellationToken.None))?.DefaultCreditConsumptionMode
           ?? CreditConsumptionMode.Total;

    public async Task<string> GetLanguageAsync()
        => (await settingsRepository.GetByUserIdAsync(currentUserProvider.GetUserId(), CancellationToken.None))?.Language ?? "en";
}
