using Budgie.Api.Infrastructure;
using Budgie.Api.Infrastructure.Logging;
using Budgie.Api.Infrastructure.Middlewares;
using Budgie.Modules.Finances;
using Budgie.Modules.Finances.Infrastructure.Database;
using Budgie.Modules.Relationships;
using Budgie.Modules.Relationships.Infrastructure.Database;
using Budgie.Modules.Users;
using Budgie.Modules.Users.Infrastructure.Database;
using Budgie.Shared.Abstractions;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.IdentityModel.Tokens;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using Scalar.AspNetCore;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();

// Ignore HTTP_PORTS env var to prevent conflict with ASPNETCORE_URLS
builder.WebHost.UseSetting("HTTP_PORTS", string.Empty);
builder.WebHost.UseSetting("HTTPS_PORTS", string.Empty);

// Register Modules
builder.Services
    .AddUsersModule()
    .AddFinancesModule()
    .AddRelationshipsModule();

// Database
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUserProvider, CurrentUserProvider>();

var connectionString = builder.Configuration.GetConnectionString("BudgieDb");
if (builder.Configuration.GetValue<bool>("UseInMemoryDatabase"))
{
    var dbName = builder.Configuration.GetConnectionString("BudgieDb") ?? "Budgie";
    builder.Services.AddDbContext<UsersDbContext>(options => options.UseInMemoryDatabase(dbName));
    builder.Services.AddDbContext<FinancesDbContext>(options => options
        .UseInMemoryDatabase(dbName)
        .ConfigureWarnings(w => w.Ignore(CoreEventId.PossibleIncorrectRequiredNavigationWithQueryFilterInteractionWarning)));
    builder.Services.AddDbContext<RelationshipsDbContext>(options => options.UseInMemoryDatabase(dbName));
}
else
{
    Action<DbContextOptionsBuilder> efConfig = o => o
        .ConfigureWarnings(w => w.Ignore(CoreEventId.PossibleIncorrectRequiredNavigationWithQueryFilterInteractionWarning));
    builder.Services.AddDbContext<UsersDbContext>(options =>
    {
        options.UseNpgsql(connectionString, b => b.MigrationsAssembly(typeof(Program).Assembly.FullName));
        efConfig(options);
    });
    builder.Services.AddDbContext<FinancesDbContext>(options =>
    {
        options.UseNpgsql(connectionString, b => b.MigrationsAssembly(typeof(Program).Assembly.FullName));
        efConfig(options);
    });
    builder.Services.AddDbContext<RelationshipsDbContext>(options =>
    {
        options.UseNpgsql(connectionString, b => b.MigrationsAssembly(typeof(Program).Assembly.FullName));
        efConfig(options);
    });
}

// OpenTelemetry (configured via OTEL_SERVICE_NAME, OTEL_EXPORTER_OTLP_ENDPOINT, etc.)
builder.Services.AddOpenTelemetry()
    .WithTracing(tracing =>
    {
        tracing.AddAspNetCoreInstrumentation()
               .AddEntityFrameworkCoreInstrumentation();
    })
    .WithMetrics(metrics =>
    {
        metrics.AddAspNetCoreInstrumentation();
    })
    .UseOtlpExporter();

// Configure Authentication
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

                var dbContext = context.HttpContext.RequestServices.GetRequiredService<UsersDbContext>();
                var userExists = await dbContext.Users.AnyAsync(u => u.Id == userId);
                if (!userExists)
                {
                    context.Fail("User no longer exists.");
                }
            }
        };
    });

builder.Services.AddAuthorization();

var app = builder.Build();

// Configure the HTTP request pipeline.
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

// Health check
app.MapGet("/health", () => Results.Ok(new { status = "healthy" }));

// Map Module Endpoints
app.MapFinancesEndpoints();
app.MapGoalsEndpoints();
app.MapRelationshipsEndpoints();
app.MapUsersEndpoints();

// Apply migrations on startup (only for real database)
if (!app.Configuration.GetValue<bool>("UseInMemoryDatabase"))
{
    using var scope = app.Services.CreateScope();
    var services = scope.ServiceProvider;

    try
    {
        var usersDb = services.GetRequiredService<UsersDbContext>();
        await usersDb.Database.MigrateAsync();

        var financesDb = services.GetRequiredService<FinancesDbContext>();
        await financesDb.Database.MigrateAsync();

        var relationshipsDb = services.GetRequiredService<RelationshipsDbContext>();
        await relationshipsDb.Database.MigrateAsync();
    }
    catch (Exception ex)
    {
        var logger = services.GetRequiredService<ILogger<Program>>();
        LogMessages.DatabaseMigrationError(logger, ex);
        throw;
    }
}

app.Run();
