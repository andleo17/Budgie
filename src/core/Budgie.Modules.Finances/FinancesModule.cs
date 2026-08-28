using Budgie.Modules.Finances.Application;
using Budgie.Modules.Finances.Application.Import;
using Budgie.Modules.Finances.Infrastructure;
using Budgie.Modules.Finances.Infrastructure.Database;
using Budgie.Modules.Finances.Infrastructure.Import;
using Budgie.Modules.Finances.Presentation.Accounts;
using Budgie.Modules.Finances.Presentation.Categories;
using Budgie.Modules.Finances.Presentation.Export;
using Budgie.Modules.Finances.Presentation.Goals;
using Budgie.Modules.Finances.Presentation.Import;
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
        services.AddScoped<IAccountRepository, AccountRepository>();
        services.AddScoped<ITransactionRepository, TransactionRepository>();
        services.AddScoped<ICategoryRepository, CategoryRepository>();
        services.AddScoped<IRecurringTransactionRepository, RecurringTransactionRepository>();
        services.AddScoped<IGoalRepository, GoalRepository>();
        services.AddScoped<IReportRepository, ReportRepository>();
        services.AddScoped<IExportRepository, ExportRepository>();
        services.AddScoped<IMoneyManagerParser, MoneyManagerSqliteParser>();
        services.AddScoped<IImportRunRepository, ImportRunRepository>();
        return services;
    }

    public static IEndpointRouteBuilder MapFinancesEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/finances").RequireAuthorization();
        CreateAccountEndpoint.MapEndpoint(group);
        AccountsEndpoints.MapEndpoints(group);
        CreateTransactionEndpoint.MapEndpoint(group);
        TransactionsEndpoints.MapEndpoints(group);
        BulkRepayEndpoint.MapEndpoint(group);
        PersonSummaryEndpoint.MapEndpoint(group);
        DebtsEndpoint.MapEndpoint(group);
        CategoriesEndpoints.MapEndpoints(group);
        GoalsEndpoints.MapEndpoints(group);
        ReportsEndpoints.MapEndpoints(group);
        RecurringTransactionEndpoints.MapEndpoints(group);
        ExportEndpoints.MapEndpoints(group);
        ImportEndpoints.MapImportEndpoints(app);
        return app;
    }
}
