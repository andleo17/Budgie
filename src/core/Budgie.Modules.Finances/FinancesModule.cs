using Budgie.Modules.Finances.Application;
using Budgie.Modules.Finances.Infrastructure;
using Budgie.Modules.Finances.Infrastructure.Database;
using Budgie.Modules.Finances.Presentation.Accounts;
using Budgie.Modules.Finances.Presentation.Categories;
using Budgie.Modules.Finances.Presentation.Persons;
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
        return app;
    }
}
