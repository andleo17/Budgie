using Budgie.Api;
using Budgie.Api.Infrastructure;
using Budgie.Api.Infrastructure.Middlewares;
using Budgie.Modules.Finances;
using Budgie.Modules.Relationships;
using Budgie.Modules.Users;
using Budgie.Shared.Abstractions;
using Budgie.Shared.Infrastructure;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Npgsql;
using OpenTelemetry.Trace;
using OpenTelemetry.Metrics;
using OpenTelemetry;
using Scalar.AspNetCore;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();

builder.WebHost.UseSetting("HTTP_PORTS", string.Empty);
builder.WebHost.UseSetting("HTTPS_PORTS", string.Empty);

// Npgsql Data Source (AOT-compatible, pooled connections)
var connectionString = builder.Configuration.GetConnectionString("BudgieDb")!;
builder.Services.AddSingleton<NpgsqlDataSource>(_ =>
{
    var dataSourceBuilder = new NpgsqlDataSourceBuilder(connectionString);
    return dataSourceBuilder.Build();
});
builder.Services.AddSingleton<IDbConnectionFactory, NpgsqlConnectionFactory>();

builder.Services
    .AddUsersModule()
    .AddFinancesModule()
    .AddRelationshipsModule();

builder.Services.AddMediator(options => options.ServiceLifetime = ServiceLifetime.Scoped);

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUserProvider, CurrentUserProvider>();

// OpenTelemetry
builder.Services.AddOpenTelemetry()
    .WithTracing(tracing =>
    {
        tracing.AddAspNetCoreInstrumentation();
    })
    .WithMetrics(metrics =>
    {
        metrics.AddAspNetCoreInstrumentation();
    })
    .UseOtlpExporter();

// Authentication
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"] ?? "Budgie",
            ValidAudience = builder.Configuration["Jwt:Audience"] ?? "Budgie",
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Secret"] ?? "a-very-long-and-secure-secret-key-123456789"))
        };

        options.Events = new JwtBearerEvents
        {
            OnTokenValidated = async context =>
            {
                var userIdClaim = context.Principal?.FindFirstValue(ClaimTypes.NameIdentifier)
                                  ?? context.Principal?.FindFirstValue(JwtRegisteredClaimNames.Sub);

                if (!Guid.TryParse(userIdClaim, out var userId))
                {
                    context.Fail("Invalid token: missing user identifier.");
                    return;
                }

                var factory = context.HttpContext.RequestServices.GetRequiredService<IDbConnectionFactory>();
                var exists = await factory.QuerySingleAsync<bool>(
                    @"SELECT EXISTS(SELECT 1 FROM ""Users"" WHERE ""Id"" = @Id)",
                    new { Id = userId },
                    context.HttpContext.RequestAborted);

                if (!exists)
                    context.Fail("User no longer exists.");
            }
        };
    });

builder.Services.AddAuthorization();

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();

app.UseDefaultFiles();
app.UseStaticFiles();
app.MapFallbackToFile("index.html");

app.UseAuthentication();
app.UseAuthorization();
app.UseMiddleware<ConfigurationCheckMiddleware>();

app.MapOpenApi();
app.MapScalarApiReference();

app.MapGet("/health", () => Results.Ok(new { status = "healthy" }));

app.MapFinancesEndpoints();
app.MapRelationshipsEndpoints();
app.MapUsersEndpoints();

// Apply schema on startup
if (!app.Configuration.GetValue<bool>("UseInMemoryDatabase"))
{
    using var scope = app.Services.CreateScope();
    var factory = scope.ServiceProvider.GetRequiredService<IDbConnectionFactory>();

    var scriptsPath = Path.Combine(app.Environment.ContentRootPath, "sql");
    if (Directory.Exists(scriptsPath))
    {
        foreach (var file in Directory.GetFiles(scriptsPath, "*.sql").OrderBy(f => f))
        {
            var sql = await File.ReadAllTextAsync(file);
            await factory.ExecuteAsync(sql);
        }
    }

    // Seed system categories
    await SeedSystemCategoriesAsync(factory);
}

app.Run();

static async Task SeedSystemCategoriesAsync(IDbConnectionFactory factory)
{
    var exists = await factory.QuerySingleAsync<bool>(
        @"SELECT EXISTS(SELECT 1 FROM ""Categories"" WHERE ""IsSystem"" = TRUE)");

    if (exists) return;

    var systemCategories = new[]
    {
        (Id: Guid.Parse("a0000000-0000-0000-0000-000000000001"), Name: "Salary", Type: 0),
        (Id: Guid.Parse("a0000000-0000-0000-0000-000000000002"), Name: "Freelance", Type: 0),
        (Id: Guid.Parse("a0000000-0000-0000-0000-000000000003"), Name: "Investment", Type: 0),
        (Id: Guid.Parse("a0000000-0000-0000-0000-000000000004"), Name: "Loan Repayment", Type: 0),
        (Id: Guid.Parse("b0000000-0000-0000-0000-000000000001"), Name: "Food", Type: 1),
        (Id: Guid.Parse("b0000000-0000-0000-0000-000000000002"), Name: "Transport", Type: 1),
        (Id: Guid.Parse("b0000000-0000-0000-0000-000000000003"), Name: "Housing", Type: 1),
        (Id: Guid.Parse("b0000000-0000-0000-0000-000000000004"), Name: "Health", Type: 1),
        (Id: Guid.Parse("b0000000-0000-0000-0000-000000000005"), Name: "Entertainment", Type: 1),
        (Id: Guid.Parse("b0000000-0000-0000-0000-000000000006"), Name: "Education", Type: 1),
        (Id: Guid.Parse("b0000000-0000-0000-0000-000000000007"), Name: "Bills", Type: 1),
        (Id: Guid.Parse("b0000000-0000-0000-0000-000000000008"), Name: "Transfer", Type: 2),
        (Id: Guid.Parse("b0000000-0000-0000-0000-000000000009"), Name: "Initial Balance", Type: 3),
    };

    foreach (var cat in systemCategories)
    {
        await factory.ExecuteAsync(
            @"INSERT INTO ""Categories"" (""Id"", ""Name"", ""Type"", ""IsSystem"")
              VALUES (@Id, @Name, @Type, TRUE)
              ON CONFLICT DO NOTHING",
            new { cat.Id, cat.Name, cat.Type });
    }
}

public partial class Program { }
