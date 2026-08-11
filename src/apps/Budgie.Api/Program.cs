using Budgie.Api;
using Budgie.Api.Infrastructure;
using Budgie.Api.Infrastructure.Middlewares;
using Budgie.Modules.Finances;
using Budgie.Modules.Finances.Infrastructure.Database;
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

// Prevent .NET SDK default HTTP_PORTS=8080 from conflicting with ASPNETCORE_URLS
builder.WebHost.UseSetting("HTTP_PORTS", string.Empty);
builder.WebHost.UseSetting("HTTPS_PORTS", string.Empty);

// TLS handled by reverse proxy; suppress infrastructure warnings
builder.Logging.AddFilter("Microsoft.AspNetCore.DataProtection", LogLevel.Error);
builder.Logging.AddFilter("Microsoft.AspNetCore.Server.Kestrel", LogLevel.Error);
builder.Logging.AddFilter("Microsoft.AspNetCore.Hosting.Diagnostics", LogLevel.Error);

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
    await SystemCategorySeeder.SeedAsync(factory);
}

app.Run();

public partial class Program { }
