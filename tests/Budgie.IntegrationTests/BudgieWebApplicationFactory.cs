using Budgie.Modules.Finances.Infrastructure.Database;
using Budgie.Shared.Infrastructure;
using Dapper;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Npgsql;
using Testcontainers.PostgreSql;

namespace Budgie.IntegrationTests;

/// <summary>
/// Creates a fresh, empty PostgreSQL database per test collection via Testcontainers,
/// then applies the schema from src/apps/Budgie.Api/sql/ directly to the container.
/// Every test run starts from a blank, deterministic database with no dependency on
/// shared/dev DB state. Schema is applied during InitializeAsync, before the app starts,
/// so the API never touches an empty database.
/// </summary>
public class BudgieWebApplicationFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder()
        .WithImage("postgres:18-alpine")
        .WithDatabase("budgie_test")
        .WithUsername("budgie")
        .WithPassword("budgie_test_password")
        .Build();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("UseInMemoryDatabase", "false");
        builder.UseSetting("ConnectionStrings:BudgieDb", _container.GetConnectionString());
    }

    public Task InitializeAsync()
    {
        return Task.Run(async () =>
        {
            await _container.StartAsync();
            await ApplySchemaAsync();
        });
    }

    public Task DisposeAsync() => _container.DisposeAsync().AsTask();

    public string GetContainerConnectionString() => _container.GetConnectionString();

    /// <summary>Kept for backward-compat with existing test classes.</summary>
    public void EnsureDatabaseCreated() { }

    private async Task ApplySchemaAsync()
    {
        using var connection = new NpgsqlConnection(_container.GetConnectionString());
        await connection.OpenAsync();

        var sqlPath = Path.Combine(FindApiSourceRoot(), "sql");
        foreach (var file in Directory.GetFiles(sqlPath, "*.sql").OrderBy(f => f))
        {
            var sql = await File.ReadAllTextAsync(file);
            await connection.ExecuteAsync(sql);
        }

        // Seed system categories (IDs must match SystemCategoryIds for FK resolution)
        var factory = new NpgsqlTestConnectionFactory(_container.GetConnectionString());
        await SystemCategorySeeder.SeedAsync(factory);
    }

    private static string FindApiSourceRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (Directory.Exists(Path.Combine(dir.FullName, "src", "apps", "Budgie.Api", "sql")))
                return Path.Combine(dir.FullName, "src", "apps", "Budgie.Api");
            dir = dir.Parent;
        }
        throw new DirectoryNotFoundException("Could not find src/apps/Budgie.Api/sql directory.");
    }
}
