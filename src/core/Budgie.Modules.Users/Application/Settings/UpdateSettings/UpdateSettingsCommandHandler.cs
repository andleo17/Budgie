using Budgie.Modules.Users.Domain;
using Budgie.Modules.Users.Infrastructure.Database;
using Budgie.Modules.Users.Infrastructure.Logging;
using Budgie.Shared.Abstractions;
using ErrorOr;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace Budgie.Modules.Users.Application.Settings.UpdateSettings;

internal sealed class UpdateSettingsCommandHandler(
    UsersDbContext dbContext,
    ICurrentUserProvider currentUserProvider,
    ICurrencyUsageChecker currencyUsageChecker,
    ILogger<UpdateSettingsCommandHandler> logger)
    : IRequestHandler<UpdateSettingsCommand, ErrorOr<Success>>
{
    public async ValueTask<ErrorOr<Success>> Handle(UpdateSettingsCommand request, CancellationToken cancellationToken)
    {
        var userId = currentUserProvider.GetUserId();
        var settings = await dbContext.UserSettings.FirstOrDefaultAsync(s => s.UserId == userId, cancellationToken);

        if (settings is null)
        {
            settings = UserSettings.Create(userId);
            dbContext.UserSettings.Add(settings);
        }

        var newSupported = request.SupportedCurrencies;
        if (newSupported is not null)
        {
            if (!newSupported.Contains(request.MainCurrency))
                return Error.Conflict("Currency.IsMainCurrency",
                    "The base currency must be one of the supported currencies.");

            var removed = settings.SupportedCurrencies.Except(newSupported).ToList();
            foreach (var code in removed)
            {
                if (code == request.MainCurrency)
                    return Error.Conflict("Currency.IsMainCurrency",
                        "Cannot remove your primary currency. Select a different base currency first.");

                var blocking = await currencyUsageChecker.GetBlockingAccountNamesAsync(userId, code);
                if (blocking.Count != 0)
                    return Error.Conflict("Currency.HasActiveAccounts",
                        $"Cannot remove {code}. Close or zero out: {string.Join(", ", blocking)}");
            }
        }

        settings.Update(request.MainCurrency, request.DefaultCreditConsumptionMode, newSupported, request.Language, request.TransactionPageSize, request.DefaultRecurringApplicability, request.AutoApplyOnLoad);
        await dbContext.SaveChangesAsync(cancellationToken);

        LogMessages.SettingsUpdated(logger, userId);

        return Result.Success;
    }
}
