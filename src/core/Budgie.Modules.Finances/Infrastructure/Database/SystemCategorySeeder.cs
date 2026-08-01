using Budgie.Modules.Finances.Domain.Categories;
using Budgie.Modules.Finances.Infrastructure.Logging;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Budgie.Modules.Finances.Infrastructure.Database;

internal sealed class SystemCategorySeeder(IServiceScopeFactory scopeFactory, ILogger<SystemCategorySeeder> logger)
    : IHostedService
{
    private static readonly Dictionary<Guid, (string Name, int Type)> SystemCategories = new()
    {
        [SystemCategoryIds.Transfer] = ("Transfer", 2),
        [SystemCategoryIds.TransferFee] = ("Transfer Fee", 1),
        [SystemCategoryIds.LoanRepayment] = ("Loan Repayment", 4),
        [SystemCategoryIds.Adjustment] = ("Adjustment", 1),
        [SystemCategoryIds.Interest] = ("Interest", 1),
        [SystemCategoryIds.InitialBalance] = ("Initial Balance", 0),
    };

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FinancesDbContext>();

        foreach (var (id, (name, type)) in SystemCategories)
        {
            var exists = await db.Categories
                .IgnoreQueryFilters()
                .AnyAsync(c => c.Id == id, cancellationToken);

            if (!exists)
            {
                var category = Category.CreateSystem(id, name, type);
                db.Categories.Add(category);
                LogMessages.SystemCategorySeeded(logger, name, id);
            }
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
