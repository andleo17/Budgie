using Budgie.Modules.Finances.Infrastructure;
using Budgie.Modules.Finances.Infrastructure.Database;
using Budgie.Modules.Finances.Presentation.Accounts;
using Budgie.Modules.Finances.Presentation.Categories;
using Budgie.Modules.Finances.Presentation.Debts;
using Budgie.Modules.Finances.Presentation.Goals;
using Budgie.Modules.Finances.Presentation.Persons;
using Budgie.Modules.Finances.Presentation.RecurringTransactions;
using Budgie.Modules.Finances.Presentation.Reports;
using Budgie.Modules.Finances.Presentation.Transactions;
using Budgie.Shared.Abstractions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Budgie.Modules.Finances;

public static class FinancesModule
{
    public static IServiceCollection AddFinancesModule(this IServiceCollection services)
    {
        services.AddScoped<ICurrencyUsageChecker, CurrencyUsageChecker>();
        services.AddScoped<ILanguageProvider, LanguageProvider>();
        services.AddSingleton<ITranslationService, TranslationService>();
        services.AddHostedService<SystemCategorySeeder>();

        return services;
    }

    public static IEndpointRouteBuilder MapFinancesEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/finances").RequireAuthorization();
        
        CreateAccountEndpoint.MapEndpoint(group);
        UpdateAccountEndpoint.MapEndpoint(group);
        DeleteAccountEndpoint.MapEndpoint(group);
        GetAccountsEndpoints.MapEndpoints(group);

        CreateCategoryEndpoint.MapEndpoint(group);
        CategoriesEndpoints.MapEndpoints(group);

        CreateTransactionEndpoint.MapEndpoint(group);
        TransactionsEndpoints.MapEndpoints(group);

        RepayLoanEndpoint.MapEndpoint(group);
        BulkRepayEndpoint.MapEndpoint(group);
        TransferEndpoint.MapEndpoint(group);
        ReportsEndpoints.MapEndpoints(group);

        PersonSummaryEndpoint.MapEndpoint(group);
        DebtsEndpoint.MapEndpoint(group);
        RecurringTransactionEndpoints.MapEndpoints(group);
        return app;
    }

    public static IEndpointRouteBuilder MapGoalsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/finances").RequireAuthorization();

        CreateGoalEndpoint.MapEndpoint(group);
        UpdateGoalEndpoint.MapEndpoint(group);
        DeleteGoalEndpoint.MapEndpoint(group);
        GetGoalsEndpoints.MapEndpoints(group);

        return app;
    }
}
