---
name: create-module
description: "Use when creating a new Budgie module following the Clean Architecture modular monolith conventions. Scaffolds the entire module structure including Application, Domain, Infrastructure, Presentation layers, DbContext, Module registration, and tests."
---

# Create a New Budgie Module

Use this skill to scaffold a new module in `src/core/Budgie.Modules.Xxx/` following the established patterns.

## Module Naming Convention

| Element | Convention | Example |
|---------|-----------|---------|
| Project | `Budgie.Modules.<Name>` | `Budgie.Modules.Inventory` |
| Namespace root | `Budgie.Modules.<Name>` | `Budgie.Modules.Inventory` |
| Module class | `<Name>Module` | `InventoryModule` |
| DbContext | `<Name>DbContext` | `InventoryDbContext` |
| Test project | `Budgie.Modules.<Name>.UnitTests` | `Budgie.Modules.Inventory.UnitTests` |

## Project Structure

```
src/
  apps/
    Budgie.Api/                   # ASP.NET Minimal API host (entry point)
    Budgie.Web/                   # React + Vite frontend
  core/
    Budgie.Shared/                # Shared: Entity, ICurrentUserProvider, enums
    Budgie.Modules.Users/         # Auth, settings
    Budgie.Modules.Finances/      # Accounts, categories, transactions, reports
    Budgie.Modules.Relationships/ # Persons
    Budgie.Modules.Xxx/           # <-- new module goes here
tests/
  Budgie.IntegrationTests/
  Budgie.Modules.Xxx.UnitTests/
```

New modules always go under `src/core/Budgie.Modules.Xxx/`. The API project in `src/apps/Budgie.Api/` references all core modules.

## Dev Environment

A `.devcontainer/` with Docker Compose provides:
- **`app`**: dev container with .NET 10 SDK + Bun
- **`db`**: PostgreSQL 18 Alpine
- **`pgadmin`**: pgAdmin 4 on port 5051

Inside the devcontainer, the API connects to Postgres via hostname `db`. The connection string is set via environment variable `ConnectionStrings__BudgieDb` so `appsettings.json` stays untouched for local development.

## Step 1 — Create the Module Project

```bash
mkdir src/core/Budgie.Modules.Xxx
```

Create `src/core/Budgie.Modules.Xxx/Budgie.Modules.Xxx.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <ItemGroup>
    <ProjectReference Include="..\Budgie.Shared\Budgie.Shared.csproj" />
    <FrameworkReference Include="Microsoft.AspNetCore.App" />
  </ItemGroup>
  <ItemGroup>
    <PackageReference Include="MediatR" Version="14.1.0" />
    <PackageReference Include="Microsoft.EntityFrameworkCore" Version="10.0.8" />
  </ItemGroup>
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>
  <ItemGroup>
    <AssemblyAttribute Include="System.Runtime.CompilerServices.InternalsVisibleTo">
      <_Parameter1>Budgie.Modules.Xxx.UnitTests</_Parameter1>
    </AssemblyAttribute>
  </ItemGroup>
</Project>
```

Add the project reference in `src/apps/Budgie.Api/Budgie.Api.csproj`:

```xml
<ProjectReference Include="..\..\core\Budgie.Modules.Xxx\Budgie.Modules.Xxx.csproj" />
```

## Step 2 — Create Module Entry Point

Create `src/core/Budgie.Modules.Xxx/XxxModule.cs`:

```csharp
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Budgie.Modules.Xxx;

public static class XxxModule
{
    public static IServiceCollection AddXxxModule(this IServiceCollection services)
    {
        services.AddMediatR(config =>
        {
            config.RegisterServicesFromAssembly(typeof(XxxModule).Assembly);
        });
        // Register additional services here
        return services;
    }

    public static IEndpointRouteBuilder MapXxxEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/xxx").RequireAuthorization();
        // Map endpoints here
        return app;
    }
}
```

## Step 3 — Create Domain Entity

Create `src/core/Budgie.Modules.Xxx/Domain/<Aggregate>/Entity.cs`:

```csharp
using Budgie.Shared.Abstractions;

namespace Budgie.Modules.Xxx.Domain.<Aggregate>;

public class EntityName : Entity
{
    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    // Other properties with private setters

    private EntityName() { }  // EF Core

    public static EntityName Create(Guid userId, ...)
    {
        return new EntityName { Id = Guid.NewGuid(), UserId = userId, ... };
    }

    // Business behavior methods
}
```

Notes on the pattern:
- `Entity` base (`Budgie.Shared/Abstractions/Entity.cs`) **does not define `Id`** — declare `public Guid Id { get; private set; }` per entity. It does provide `RaiseDomainEvent`/`GetDomainEvents` for `IDomainEvent : INotification` (currently unused scaffolding).
- Not every domain class extends `Entity` — plain classes are fine for owned/simple types (`Tag`, `TransactionParticipant`, `CreditCardSettings`).
- Reuse shared Value Objects from `Budgie.Shared/Domain/ValueObjects/` (`Money`, `CurrencyCode`, `Name`, `EmailAddress`, `HexColor`, `Percentage`): `sealed record`s with private constructors, static factories (`From`, `FromPositive`, `FromNonNegative`) that validate, and implicit conversions to primitives.

## Step 4 — Create DbContext

Create `src/core/Budgie.Modules.Xxx/Infrastructure/Database/XxxDbContext.cs`:

```csharp
using Budgie.Modules.Xxx.Domain.<Aggregate>;
using Budgie.Shared.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace Budgie.Modules.Xxx.Infrastructure.Database;

public class XxxDbContext(
    DbContextOptions<XxxDbContext> options,
    ICurrentUserProvider currentUserProvider) : DbContext(options)
{
    public DbSet<EntityName> EntityNames => Set<EntityName>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<EntityName>(builder =>
        {
            builder.HasKey(e => e.Id);
            builder.Property(e => e.UserId).IsRequired();
            builder.HasQueryFilter(e => e.UserId == currentUserProvider.UserId);
            // Value objects: owned types via builder.OwnsOne(...) or scalar
            // via .HasConversion(ValueConverters.NameConverter)
        });
    }
}
```

If the module doesn't need multi-tenant filtering (like Users module), use traditional constructor without ICurrentUserProvider. Persist value objects with `OwnsOne` (owned types, e.g. `Money` column-split with `HasColumnName`) or shared `ValueConverters` (`Budgie.Shared/Infrastructure/ValueConverters.cs`) via `HasConversion`.

## Step 5 — Register DbContext in Program.cs

In `src/apps/Budgie.Api/Program.cs`, add the DbContext registration alongside the existing ones, mirroring the central in-memory/real branching:

```csharp
using Budgie.Modules.Xxx.Infrastructure.Database;

// In the database section — swap to InMemory when the config flag is set
var dbName = builder.Configuration.GetConnectionString("BudgieDb") ?? "Budgie";
builder.Services.AddDbContext<XxxDbContext>(options =>
{
    if (builder.Configuration.GetValue<bool>("UseInMemoryDatabase"))
        options.UseInMemoryDatabase(dbName);
    else
        options.UseNpgsql(connectionString, b => b.MigrationsAssembly(typeof(Program).Assembly.FullName));
});
```

Also add module registration:
```csharp
// In module section:
builder.Services.AddXxxModule();

// In endpoint mapping section:
app.MapXxxEndpoints();
```

A module may expose **multiple** endpoint-mapping entry points (e.g. Finances has both `MapFinancesEndpoints()` and `MapGoalsEndpoints()` sharing the `/api/finances` group) — add one `MapXxxEndpoints()` per aggregate group.

## Step 6 — Create Command (CQRS)

Create `src/core/Budgie.Modules.Xxx/Application/<Feature>/<Action>/<Action>Command.cs`:

```csharp
using ErrorOr;
using MediatR;

namespace Budgie.Modules.Xxx.Application.<Feature>.<Action>;

public record <Action>Command(/* params */) : IRequest<ErrorOr<Guid>>;
```

Create `src/core/Budgie.Modules.Xxx/Application/<Feature>/<Action>/<Action>CommandHandler.cs`:

```csharp
using Budgie.Modules.Xxx.Infrastructure.Database;
using ErrorOr;
using MediatR;

namespace Budgie.Modules.Xxx.Application.<Feature>.<Action>;

internal sealed class <Action>CommandHandler(XxxDbContext dbContext)
    : IRequestHandler<<Action>Command, ErrorOr<Guid>>
{
    public async Task<ErrorOr<Guid>> Handle(<Action>Command request, CancellationToken ct)
    {
        // Business logic
        var entity = EntityName.Create(/* ... */);
        dbContext.EntityNames.Add(entity);
        await dbContext.SaveChangesAsync(ct);
        return entity.Id;
    }
}
```

For update/delete commands, use `ErrorOr<Success>` as the return type.

## Step 7 — Create Query (CQRS)

Create `src/core/Budgie.Modules.Xxx/Application/<Feature>/<Action>/<Action>Query.cs` — a pure positional record, no handler in this file:

```csharp
using ErrorOr;
using MediatR;

namespace Budgie.Modules.Xxx.Application.<Feature>.<Action>;

public record <Action>Query(/* params, or empty parens if none */) : IRequest<ErrorOr<List<ResponseDto>>>;
```

Create `src/core/Budgie.Modules.Xxx/Application/<Feature>/<Action>/<Action>QueryHandler.cs` — one dedicated handler file (never inline in the query file):

```csharp
using Budgie.Modules.Xxx.Infrastructure.Database;
using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Budgie.Modules.Xxx.Application.<Feature>.<Action>;

public record ResponseDto(Guid Id, /* ... */);

internal sealed class <Action>QueryHandler(XxxDbContext dbContext)
    : IRequestHandler<<Action>Query, ErrorOr<List<ResponseDto>>>
{
    public async Task<ErrorOr<List<ResponseDto>>> Handle(<Action>Query request, CancellationToken ct)
    {
        return await dbContext.EntityNames
            .AsNoTracking()                        // reads always AsNoTracking
            .Select(e => new ResponseDto(e.Id, /* ... */))
            .ToListAsync(ct);
    }
}
```

List queries return `ErrorOr<List<ResponseDto>>`; single queries return `ErrorOr<ResponseDto>`; paged queries return `ErrorOr<PagedResponse<...>>`. Empty-parameter queries use parens: `public record GetThingsQuery() : IRequest<...>;`.

## Step 8 — Create Endpoint

Create `src/core/Budgie.Modules.Xxx/Presentation/<Feature>/<Action>Endpoint.cs` (single action):

```csharp
using Budgie.Modules.Xxx.Application.<Feature>.<Action>;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Budgie.Modules.Xxx.Presentation.<Feature>;

public static class <Action>Endpoint
{
    public static void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("<route>", async (<Action>Command command, IMediator mediator) =>
        {
            var result = await mediator.Send(command);
            return result.Match<IResult>(
                id => TypedResults.Created($"/api/xxx/<route>/{id}", new { Id = id }),
                errors => TypedResults.BadRequest(errors.ToDictionary(e => e.Code, e => new[] { e.Description }))
            );
        }).WithTags("<Feature>");
    }
}
```

For grouped endpoints, create `<Feature>Endpoints.cs`:

```csharp
internal static class <Feature>Endpoints
{
    public static void MapEndpoints(IEndpointRouteBuilder app)
    {
        app.MapGet("<route>", async (ISender sender) => { ... });
        app.MapPut("<route>/{id:guid}", async (Guid id, [FromBody] RequestDto request, ISender sender) => { ... });
        app.MapDelete("<route>/{id:guid}", async (Guid id, ISender sender) => { ... });
    }
}
public record RequestDto(/* ... */);
```

## Step 9 — Create Unit Test Project

Create `tests/Budgie.Modules.Xxx.UnitTests/Budgie.Modules.Xxx.UnitTests.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <IsPackable>false</IsPackable>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="coverlet.collector" Version="6.0.4" />
    <PackageReference Include="FluentAssertions" Version="8.10.0" />
    <PackageReference Include="Microsoft.EntityFrameworkCore.InMemory" Version="10.0.8" />
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.14.1" />
    <PackageReference Include="NSubstitute" Version="5.3.0" />
    <PackageReference Include="xunit" Version="2.9.3" />
    <PackageReference Include="xunit.runner.visualstudio" Version="3.1.4" />
  </ItemGroup>
  <ItemGroup>
    <Using Include="Xunit" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="..\..\src\core\Budgie.Modules.Xxx\Budgie.Modules.Xxx.csproj" />
  </ItemGroup>
</Project>
```

Create test file `tests/Budgie.Modules.Xxx.UnitTests/Application/<Feature>/<Action>/<Action>CommandHandlerTests.cs`:

```csharp
using Budgie.Modules.Xxx.Application.<Feature>.<Action>;
using Budgie.Modules.Xxx.Infrastructure.Database;
using Budgie.Shared.Abstractions;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace Budgie.Modules.Xxx.UnitTests.Application.<Feature>;

public class <Action>CommandHandlerTests : IDisposable
{
    private readonly XxxDbContext _dbContext;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly Guid _userId = Guid.NewGuid();

    public <Action>CommandHandlerTests()
    {
        _currentUserProvider = Substitute.For<ICurrentUserProvider>();
        _currentUserProvider.UserId.Returns(_userId);
        _currentUserProvider.GetUserId().Returns(_userId);   // always set BOTH

        var options = new DbContextOptionsBuilder<XxxDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())   // fresh DB per test
            .Options;

        _dbContext = new XxxDbContext(options, _currentUserProvider);
        _handler = new <Action>CommandHandler(_dbContext, NullLogger<<Action>CommandHandler>.Instance);
    }

    public void Dispose() => _dbContext.Dispose();

    [Fact]
    public async Task Handle_ShouldCreate_WhenValid()
    {
        var result = await _handler.Handle(new <Action>Command(/* ... */), CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.Should().NotBeEmpty();
    }
}
```

Note: use the `UsersDbContext`-style traditional constructor (no `ICurrentUserProvider`) when testing the Users module. Infrastructure services are tested standalone with an in-memory `ConfigurationBuilder()` (e.g. `JwtProviderTests`).

### Test Coverage Guidance (maximize coverage)

- **One test file per handler**: `Application/<Feature>/<Action>/<Action>CommandHandlerTests.cs` (or `QueryHandlerTests.cs`). Combined CRUD tests in a single `<Feature>CrudHandlersTests.cs` are also accepted.
- **Naming**: handlers `Handle_<Scenario>_<Expected>()`; domain tests `<Behavior>_Should<Expected>()`.
- **Cover every branch**: success + each error (`NotFound`, `Conflict`, `Unauthorized`, `Forbidden`, `Validation` → assert `result.FirstError.Code`), multi-tenant filtering (seed a second user, assert exclusion), balance arithmetic with exact decimals, and edge-case parameterizations.
- **Domain classes** get `Domain/<Aggregate>/<Entity>Tests.cs` covering `Create` validation throws, `Update`/mutation behavior, and boundary values.
- **Module smoke test** `XxxModuleTests`: build a fresh `ServiceCollection`, call `AddXxxModule()`, and assert the expected services/MediatR registration are present.
- **EF InMemory limitations**: cannot translate a `WHERE` over a navigation + ValueObject (e.g. `t.Account.Name.Value.Contains(...)`) nor raw SQL. Test the InMemory-safe equivalent branch (e.g. search by `Notes`) and leave the unsupported path untested — it is covered by integration tests against PostgreSQL only.

## Step 10 — Error Pattern Reference

| Scenario | Return | Code |
|----------|--------|------|
| Create success | `ErrorOr<Guid>` | `return entity.Id;` |
| Update/delete success | `ErrorOr<Success>` | `return Result.Success;` |
| Not found | `ErrorOr<...>` | `return Error.NotFound("Xxx.NotFound", "Xxx not found.");` |
| Conflict | `ErrorOr<...>` | `return Error.Conflict("Xxx.AlreadyExists", "...");` |
| Unauthorized | `ErrorOr<...>` | `return Error.Unauthorized("Auth.InvalidCredentials", "Invalid ...");` |

## Step 11 — Verify

1. Build: `dotnet build`
2. Run unit tests: `dotnet test tests/Budgie.Modules.Xxx.UnitTests`
3. Run all tests: `dotnet test`
4. Check that `using` statements in Module.cs in Program.cs are correctly imported

## Devcontainer workflow

```bash
# Open in devcontainer (VS Code: Reopen in Container)
# Or run manually:
docker compose -f .devcontainer/docker-compose.yml up -d

# The API auto-restores and builds on container start.
# To run the API:
dotnet run --project src/apps/Budgie.Api

# To run the frontend:
bun --cwd src/apps/Budgie.Web dev

# PostgreSQL is available on localhost:5432 via the `db` service.
# pgAdmin is available at http://localhost:5051 (admin@budgie.local / admin).

# Production deployment (single container — API serves the frontend):
cp .env.example .env   # edit secrets (POSTGRES_PASSWORD, JWT_SECRET)
docker compose up -d   # builds src/apps/Budgie.Api/Dockerfile, listens on port 80
# Healthcheck: GET http://localhost:80/health
# The legacy separate web image (src/apps/Budgie.Web/Dockerfile) is NOT used in production.
