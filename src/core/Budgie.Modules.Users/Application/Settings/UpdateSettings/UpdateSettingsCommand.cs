using Budgie.Shared.Enums;
using ErrorOr;
using Mediator;

namespace Budgie.Modules.Users.Application.Settings.UpdateSettings;

public record UpdateSettingsCommand(
    string MainCurrency,
    CreditConsumptionMode DefaultCreditConsumptionMode,
    List<string>? SupportedCurrencies = null,
    string? Language = null,
    int? TransactionPageSize = null,
    RecurringApplicability? DefaultRecurringApplicability = null,
    bool? AutoApplyOnLoad = null) : IRequest<ErrorOr<Success>>;
