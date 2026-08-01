# Budgie

Personal finance manager — track accounts, transactions, budgets, and goals with multi-currency support and debt tracking.

## Tech Stack

| Layer | Technology |
|-------|-----------|
| Backend | .NET 10, ASP.NET Core Minimal APIs, EF Core 10, Npgsql |
| Frontend | React 19, Vite, shadcn/ui, Tailwind CSS v4, React Query, openapi-typescript |
| Database | PostgreSQL 18 |
| Auth | JWT (BCrypt.Net) |
| Containers | Docker, docker-compose (single container) |

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- [Bun](https://bun.sh) (for frontend development)
- [PostgreSQL 18](https://www.postgresql.org/download/) (for local development)
- [Docker](https://docs.docker.com/get-docker/) (for production/containerized setup)

## Development

### 1. Database

```bash
# Start PostgreSQL (or use an existing instance)
docker run -d --name budgie-db \
  -e POSTGRES_DB=budgie \
  -e POSTGRES_USER=budgie \
  -e POSTGRES_PASSWORD=budgie \
  -p 5432:5432 \
  postgres:18-alpine
```

### 2. Backend

```bash
cd src/apps/Budgie.Api

# Restore and run with migrations applied automatically
dotnet run
```

The API starts at `http://localhost:5057`. Scalar API reference at `/scalar`.

### 3. Frontend

```bash
cd src/apps/Budgie.Web

bun install
bun run dev
```

The dev server starts at `http://localhost:5173` and proxies `/api` requests to the backend.

### 4. Updating API Types

After changing backend endpoints, regenerate the frontend types:

```bash
cd src/apps/Budgie.Web

# Start the API with in-memory database on a dedicated port
ASPNETCORE_URLS=http://localhost:5060 \
UseInMemoryDatabase=true \
dotnet run --project ../Budgie.Api --no-launch-profile &

# Wait for startup, then fetch the OpenAPI schema
sleep 8
curl -s http://localhost:5060/openapi/v1.json -o openapi.json

# Generate TypeScript types
npx openapi-typescript openapi.json --output src/lib/api-types.ts

# Stop the API
pkill -f "Budgie.Api" 2>/dev/null || true
```

## Production (Docker)

A single container builds the API and serves the frontend (no nginx needed). Configuration lives in `.env`.

```bash
# 1. Create your configuration from the template
cp .env.example .env

# 2. Edit .env and set the required secrets:
#    - POSTGRES_PASSWORD  (strong password for PostgreSQL)
#    - JWT_SECRET         (JWT signing key, min 32 chars)

# 3. Build and start
docker compose up -d

# Stop
docker compose down

# View logs
docker compose logs -f
```

The application is served at `http://localhost:80` (port configurable via `BUDGIE_PORT`). The API and frontend run in a single container — nginx is not required. Healthcheck: `GET /health` returns `200 { status = "healthy" }`.

### Volumes

| Volume | Purpose |
|--------|---------|
| `budgie_postgres_data` | Persistent database storage (mounted at `/var/lib/postgresql` — see PostgreSQL 18 layout note below) |
| `budgie_dp_keys` | ASP.NET Data Protection keys (encrypted with auto-generated certificate) |

### Configuration

All configuration is passed via a `.env` file at the project root (see `.env.example`):

| Variable | Default | Required | Description |
|----------|---------|----------|-------------|
| `POSTGRES_PASSWORD` | — | Yes | PostgreSQL password |
| `JWT_SECRET` | — | Yes | JWT signing key (min 32 chars) |
| `BUDGIE_PORT` | `80` | — | Host port for the API/frontend (container listens on 80 internally) |
| `ASPNETCORE_ENVIRONMENT` | `Production` | — | Runtime environment |
| `DB_HOST` | `db` | — | Database host (compose service name) |
| `DB_PORT` | `5432` | — | Host port for PostgreSQL |
| `POSTGRES_DB` | `budgie` | — | Database name |
| `POSTGRES_USER` | `budgie` | — | Database user |
| `JWT_ISSUER` / `JWT_AUDIENCE` | `Budgie` | — | JWT issuer/audience |

### PostgreSQL 18 note

The official PostgreSQL 18 image changed its layout versus 17: `PGDATA` defaults to `/var/lib/postgresql/18/docker` and the declared `VOLUME` moved to the parent `/var/lib/postgresql`. The compose file sets `PGDATA` explicitly and mounts the volume at the parent path. If you are migrating an existing PostgreSQL 17 volume, back it up and migrate via `pg_dumpall`/restore or `pg_upgrade` — do not mount the old child path `/var/lib/postgresql/data`.

## Project Structure

```
src/
  apps/
    Budgie.Api/         # ASP.NET Minimal API host (entry point)
    Budgie.Web/         # React + Vite frontend
  core/
    Budgie.Shared/      # Shared entities, value objects, enums
    Budgie.Modules.Users/        # Auth, user settings
    Budgie.Modules.Finances/     # Accounts, categories, transactions, goals
    Budgie.Modules.Relationships/# Persons, tags
tests/
  Budgie.IntegrationTests/
  Budgie.Modules.Xxx.UnitTests/
```

Each module follows Clean Architecture: **Application** (CQRS), **Domain** (entities), **Infrastructure** (EF Core, DbContext), **Presentation** (endpoints).

## Architecture

Budgie is a **modular monolith**: each `Budgie.Modules.Xxx` project is self-contained (Application, Domain, Infrastructure, Presentation) and registered from `Program.cs` via `AddXxxModule()` / `MapXxxEndpoints()`. Cross-cutting conventions:

- **CQRS with MediatR**: commands/queries are positional `record`s implementing `IRequest<ErrorOr<T>>` (create → `ErrorOr<Guid>`, update/delete → `ErrorOr<Success>`, queries → `ErrorOr<List<T>>`). Handlers are `internal sealed class` with primary constructors, registered per-assembly in the module's `AddXxxModule()`.
- **Domain-driven entities**: private setters, private parameterless constructor (EF materialization), static `Create` factory with validation, and mutation methods for behavior. The base `Entity` provides domain-event scaffolding (currently unused).
- **Value objects**: `Money`, `CurrencyCode`, `Name`, `EmailAddress`, `HexColor`, `Percentage` — sealed records with factory validation and operator overloads; persisted via `OwnsOne` (owned types) or shared `ValueConverters` with `HasConversion`.
- **ErrorOr result pipeline**: handlers return `ErrorOr<T>`; endpoints map with `result.Match(...)` to `TypedResults`/`Results`, serializing errors as `{ code: [description] }`.
- **Multi-tenancy**: `ICurrentUserProvider` + EF global query filters (`HasQueryFilter`) scope every read/write to the current user.
- **Compile-time logging**: `[LoggerMessage]` partial methods in `Infrastructure/Logging/LogMessages.cs` — no raw `logger.Log*` calls.

## Testing

- **Unit tests** (xUnit + NSubstitute + FluentAssertions + EF Core InMemory): one file per handler, fresh InMemory DB per test (`Guid.NewGuid().ToString()`), mocked `ICurrentUserProvider` (both `UserId` and `GetUserId`), `NullLogger<THandler>.Instance`. Coverage: Shared **97.2%**, Users **~95%**, Relationships **~95%**, Finances **73.1%**.
- **Integration tests** (`WebApplicationFactory<Program>` + InMemory): 11 files; each class uses `IClassFixture<BudgieWebApplicationFactory>` with an isolated InMemory DB; auth flow is Register → Login → Bearer → `settings/setup` (requests before setup get **403**). Assert success codes 200/201/204 and errors 400/401/403/404.
- **Coverage command**: `dotnet test --collect:"XPlat Code Coverage"` (coverlet.collector). EF InMemory cannot translate navigation + ValueObject in `WHERE` clauses nor raw SQL — such branches are covered by integration tests against PostgreSQL.

## Useful Commands

```bash
# Backend
dotnet build                      # Build solution
dotnet test                       # Run all tests
dotnet watch run --project src/apps/Budgie.Api  # Hot reload
dotnet ef migrations add <Name>   # Add EF migration

# Frontend
bun run build                     # Production build
bun run lint                      # Lint
bun run typecheck                 # TypeScript check

# Docker
docker compose build              # Rebuild images
docker compose up -d              # Start services
docker compose down -v            # Stop and delete volumes
```
