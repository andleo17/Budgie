using Budgie.Modules.Users.Application;
using Budgie.Modules.Users.Domain;
using Budgie.Modules.Users.Infrastructure.Logging;
using Budgie.Shared.Abstractions;
using ErrorOr;
using Mediator;

namespace Budgie.Modules.Users.Application.Settings.UpdateSettings;

internal sealed class UpdateSettingsCommandHandler(
    IUserSettingsRepository settingsRepository,
    ICurrentUserProvider currentUserProvider,
    ICurrencyUsageChecker currencyUsageChecker,
    ILogger<UpdateSettingsCommandHandler> logger)
    : IRequestHandler<UpdateSettingsCommand, ErrorOr<Success>>
{
    public async ValueTask<ErrorOr<Success>> Handle(UpdateSettingsCommand request, CancellationToken ct)
    {
        var userId = currentUserProvider.GetUserId();
        var settings = await settingsRepository.GetByUserIdAsync(userId, ct);
        var existingCurrencies = settings?.SupportedCurrencies ?? ["USD"];

        var newSupported = request.SupportedCurrencies;
        if (newSupported is not null)
        {
            if (!newSupported.Contains(request.MainCurrency))
                return Error.Conflict("Currency.IsMainCurrency", "The base currency must be one of the supported currencies.");

            var removed = existingCurrencies.Except(newSupported).ToList();
            foreach (var code in removed)
            {
                if (code == request.MainCurrency)
                    return Error.Conflict("Currency.IsMainCurrency", "Cannot remove your primary currency.");
                var blocking = await currencyUsageChecker.GetBlockingAccountNamesAsync(userId, code);
                if (blocking.Count != 0)
                    return Error.Conflict("Currency.HasActiveAccounts", $"Cannot remove {code}. Close or zero out: {string.Join(", ", blocking)}");
            }
        }

        if (settings is null)
        {
            settings = UserSettings.Create(userId);
            settings.Update(request.MainCurrency, request.DefaultCreditConsumptionMode, newSupported,
                request.Language, request.TransactionPageSize, request.DefaultRecurringApplicability, request.AutoApplyOnLoad);
            await settingsRepository.AddAsync(settings, ct);
        }
        else
        {
            settings.Update(request.MainCurrency, request.DefaultCreditConsumptionMode, newSupported,
                request.Language, request.TransactionPageSize, request.DefaultRecurringApplicability, request.AutoApplyOnLoad);
            await settingsRepository.UpdateAsync(settings, ct);
        }

        LogMessages.SettingsUpdated(logger, userId);
        return Result.Success;
    }
}
