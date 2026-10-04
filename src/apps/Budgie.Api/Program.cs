using Budgie.Api;
using Budgie.Api.Infrastructure.Middlewares;
using Budgie.Modules.EmailImport;
using Budgie.Modules.Finances;
using Budgie.Modules.Finances.Infrastructure.Database;
using Budgie.Modules.Relationships;
using Budgie.Modules.Users;
using Budgie.Shared.Abstractions;
using Budgie.Shared.Infrastructure;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using System.Net;
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
    .AddRelationshipsModule()
    .AddEmailImportModule(builder.Configuration);

builder.Services.AddMediator(options => options.ServiceLifetime = ServiceLifetime.Scoped);

builder.Services.AddHttpContextAccessor();
// ICurrentUserProvider is registered by AddEmailImportModule as ScopedCurrentUserProvider:
// an explicitly pushed user (EmailImport sync worker, manual review dispatch) wins, and a
// normal HTTP request falls back to the JWT claim, resolving the owner exactly like the
// previous HttpContext-only provider that lived in Budgie.Api.Infrastructure (deleted in
// the email-import debt plan, §6.3). See
// Budgie.Modules.EmailImport/Infrastructure/Identity/ScopedCurrentUserProvider.cs.

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

// The production deployment runs behind a TLS-terminating reverse proxy (AGENTS.md), so the
// request arriving here has Scheme=http even though the browser used https. Without this,
// anything deriving an absolute URL — notably the OAuth redirect_uri of the email import —
// would be built as http:// and the provider would reject it with redirect_uri_mismatch.
// It must be registered BEFORE UseRouting/auth so the scheme is already corrected.
// Unknown proxies are ignored: only KnownProxies/KnownNetworks are trusted.
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
    options.KnownIPNetworks.Clear();
    // .NET 10 exposes KnownIPNetworks as System.Net.IPNetwork (the ASP.NET type is obsolete).
    options.KnownIPNetworks.Add(new System.Net.IPNetwork(IPAddress.Parse("0.0.0.0"), 0));
    options.KnownIPNetworks.Add(new System.Net.IPNetwork(IPAddress.Parse("::"), 0));
});

var app = builder.Build();

// Must precede UseExceptionHandler so redirects and logging see the corrected scheme.
app.UseForwardedHeaders();

app.UseExceptionHandler();
app.UseStatusCodePages();

app.UseMiddleware<RequestCultureMiddleware>();

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
app.MapEmailImportEndpoints();

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
