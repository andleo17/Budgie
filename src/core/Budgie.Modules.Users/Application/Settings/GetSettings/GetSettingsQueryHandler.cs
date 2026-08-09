using Budgie.Modules.Users.Infrastructure.Database;
using Budgie.Shared.Abstractions;
using Budgie.Shared.Enums;
using ErrorOr;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace Budgie.Modules.Users.Application.Settings.GetSettings;

internal sealed class GetSettingsQueryHandler(
    UsersDbContext dbContext,
    ICurrentUserProvider currentUserProvider)
    : IRequestHandler<GetSettingsQuery, ErrorOr<UserSettingsResponse>>
{
    public async ValueTask<ErrorOr<UserSettingsResponse>> Handle(GetSettingsQuery request, CancellationToken cancellationToken)
    {
        var userId = currentUserProvider.GetUserId();
        var settings = await dbContext.UserSettings.FirstOrDefaultAsync(s => s.UserId == userId, cancellationToken);

        if (settings is null)
        {
            return new UserSettingsResponse("USD", CreditConsumptionMode.Total, "en", false, ["USD"], 100);
        }

        var supported = settings.SupportedCurrencies.Count != 0
            ? settings.SupportedCurrencies
            : [settings.MainCurrency];

        return new UserSettingsResponse(
            settings.MainCurrency,
            settings.DefaultCreditConsumptionMode,
            settings.Language,
            settings.IsConfigured,
            supported,
            settings.TransactionPageSize,
            settings.DefaultRecurringApplicability,
            settings.AutoApplyOnLoad);
    }
}
