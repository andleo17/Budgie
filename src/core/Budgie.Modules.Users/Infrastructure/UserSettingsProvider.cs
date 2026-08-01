using Budgie.Modules.Users.Infrastructure.Database;
using Budgie.Shared.Abstractions;
using Budgie.Shared.Enums;
using Microsoft.EntityFrameworkCore;

namespace Budgie.Modules.Users.Infrastructure;

internal sealed class UserSettingsProvider(
    UsersDbContext dbContext,
    ICurrentUserProvider currentUserProvider) : IUserSettingsProvider
{
    public async Task<bool> IsConfiguredAsync()
    {
        var userId = currentUserProvider.GetUserId();
        return await dbContext.UserSettings
            .Where(s => s.UserId == userId)
            .Select(s => s.IsConfigured)
            .FirstOrDefaultAsync();
    }

    public async Task<string> GetMainCurrencyAsync()
    {
        var userId = currentUserProvider.GetUserId();
        return await dbContext.UserSettings
            .Where(s => s.UserId == userId)
            .Select(s => s.MainCurrency)
            .FirstOrDefaultAsync() ?? "USD";
    }

    public async Task<CreditConsumptionMode> GetDefaultCreditConsumptionModeAsync()
    {
        var userId = currentUserProvider.GetUserId();
        return await dbContext.UserSettings
            .Where(s => s.UserId == userId)
            .Select(s => s.DefaultCreditConsumptionMode)
            .FirstOrDefaultAsync();
    }

    public async Task<string> GetLanguageAsync()
    {
        var userId = currentUserProvider.GetUserId();
        return await dbContext.UserSettings
            .Where(s => s.UserId == userId)
            .Select(s => s.Language)
            .FirstOrDefaultAsync() ?? "en";
    }
}
