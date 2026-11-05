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

Two ways to run it locally:

- **Devcontainer (recommended)** — `Dev Containers: Reopen in Container` from VS Code. Brings up PostgreSQL, pgAdmin and the Vite dev server automatically, configured from the same root `.env` as production.
- **Local toolchain** — the steps below.

### 0. Devcontainer

| Service | Port | Description |
|---------|------|-------------|
| `app` | 5057 | Workspace + API (`dotnet run`, or F5). Started by you. |
| `web` | 5173 | Vite dev server with HMR, running in a `oven/bun` container. Started automatically. |
| `db` | 5432 | PostgreSQL 18, credentials from `.env`. |
| `pgadmin` | 5051 | pgAdmin. |

Open `http://localhost:5173` from the Ports panel. Requests to `/api` and `/uploads` are proxied by Vite to the API in the `app` container.

The compose file reads the project root `.env` through the `.devcontainer/.env` symlink, so dev and production share the same `POSTGRES_*`, `JWT_*` and `REGISTRATION_MODE` values; only the environment (`Development`) and ports differ. If `.env` is missing, dev-only defaults are used. The dev database lives in its own volume (`budgie-dev-postgres-data`) so it is separate from any other stack on the machine.

If Vite is started manually inside the `app` container instead, it proxies to `http://localhost:5057` by default; override with `VITE_API_PROXY_TARGET`. Set `VITE_ALLOWED_HOSTS=true` (or a comma-separated list) if you access the dev server through a hostname other than `localhost`.

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

The dev server starts at `http://localhost:5173` and proxies `/api` requests to the backend. Port 5173 must be free if you also run the devcontainer `web` service.

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
| `budgie_uploads` | Person photos (mounted at `/app/wwwroot/uploads`), so they survive rebuilds |

Person photos are written to `wwwroot/uploads` at runtime. They used to live only in the image layer, which meant a rebuild lost every photo uploaded since the last build while the database kept pointing at the missing files. They now live in the `budgie_uploads` volume. On the first start with the volume mounted, `entrypoint.sh` copies any photos found in the image into the volume (a marker file makes this happen once, and existing files are never overwritten), so upgrading an existing deployment keeps its photos.

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
| `EMAILIMPORT_ENABLED` | `false` | — | Registers the background email sync worker (see [Connecting Gmail](#connecting-gmail-email-import)) |
| `EMAILIMPORT_SYNCINTERVALMINUTES` | `10` | — | Minutes between automatic mailbox syncs |
| `EMAILIMPORT_ENCRYPTIONKEY` | derived from `JWT_SECRET` | — | 32-byte key encrypting the stored OAuth credentials at rest |
| `EMAILIMPORT_PROVIDERS_GMAIL_CLIENTID` | — | for Gmail | OAuth client id from Google Cloud Console |
| `EMAILIMPORT_PROVIDERS_GMAIL_CLIENTSECRET` | — | for Gmail | OAuth client secret from Google Cloud Console |

### PostgreSQL 18 note

The official PostgreSQL 18 image changed its layout versus 17: `PGDATA` defaults to `/var/lib/postgresql/18/docker` and the declared `VOLUME` moved to the parent `/var/lib/postgresql`. The compose file sets `PGDATA` explicitly and mounts the volume at the parent path. If you are migrating an existing PostgreSQL 17 volume, back it up and migrate via `pg_dumpall`/restore or `pg_upgrade` — do not mount the old child path `/var/lib/postgresql/data`.

## Connecting Gmail (email import)

Budgie can read your bank notification emails and turn them into transactions. The `EmailImport`
module talks to the Gmail API with **read-only** access (`https://www.googleapis.com/auth/gmail.readonly`):
it never marks messages as read and never writes anything back to the mailbox.

There are three steps: create an OAuth client in Google Cloud, configure Budgie with it, then use
**Settings → Email import → Connect email**.

### 1. Create the OAuth client

1. Open [Google Cloud Console](https://console.cloud.google.com/) and create (or pick) a project.
2. **APIs & Services → Library** → enable the **Gmail API**.
3. **APIs & Services → OAuth consent screen**:
   - User type **External** (or **Internal** if you use Google Workspace).
   - Add the scope `https://www.googleapis.com/auth/gmail.readonly`.
   - While the publishing status is **Testing**, add your Google account under **Test users** —
     otherwise Google rejects the consent with `access_denied`.
4. **APIs & Services → Credentials → Create credentials → OAuth client ID**, type **Web application**.
5. In **Authorized redirect URIs** add exactly the callback Budgie builds. It is derived from the
   scheme and host of the request that starts the flow, so it must match character by character:
   - Production behind a TLS proxy: `https://<your-domain>/api/email-import/connections/gmail/callback`
   - Local development (API on port 5057): `http://localhost:5057/api/email-import/connections/gmail/callback`
6. Copy the **Client ID** and **Client secret**.

### 2. Configure Budgie

**Docker** — set these in `.env` (the compose file maps them to `EmailImport__*`):

```dotenv
EMAILIMPORT_ENABLED=false                                      # background sync worker
EMAILIMPORT_SYNCINTERVALMINUTES=10
EMAILIMPORT_ENCRYPTIONKEY=                                      # generate it yourself, see below
EMAILIMPORT_PROVIDERS_GMAIL_CLIENTID=<client-id>.apps.googleusercontent.com
EMAILIMPORT_PROVIDERS_GMAIL_CLIENTSECRET=<client-secret>
```

Then `docker compose up -d`.

**Local toolchain** — note that `.env` is only read by Docker Compose: `dotnet run` ignores it. Use
user secrets (they live outside the repo, so the client secret never lands in git) or the equivalent
`EmailImport__*` environment variables:

```bash
cd src/apps/Budgie.Api

dotnet user-secrets set "EmailImport:Providers:gmail:ClientId" "<client-id>.apps.googleusercontent.com"
dotnet user-secrets set "EmailImport:Providers:gmail:ClientSecret" "<client-secret>"
dotnet user-secrets set "EmailImport:EncryptionKey" "$(openssl rand -hex 32)"
dotnet user-secrets set "EmailImport:Enabled" "false"   # true to run the background worker
```

They load because `launchSettings.json` starts the API with `ASPNETCORE_ENVIRONMENT=Development`. Do
**not** put the client secret in `appsettings.Development.json`: that file is tracked by git. Its
`EmailImport` section is only here for reference, since it is the shape `appsettings.json` ships:

```json
{
  "EmailImport": {
    "Enabled": false,
    "SyncIntervalMinutes": 10,
    "EncryptionKey": "",
    "Providers": {
      "gmail": {
        "ClientId": "<client-id>.apps.googleusercontent.com",
        "ClientSecret": "<client-secret>"
      }
    }
  }
}
```

Worth knowing:

- **The encryption key is yours to generate.** Google only hands out the client id and secret.
  Any string works — it is hashed with SHA-256 into the 32-byte AES key — so a random value is
  enough: `openssl rand -hex 32` (hex survives `.env` and shells without quoting surprises).
- `Enabled=false` only turns the **background worker** off; you can still sync on demand with
  **Sync now** (`POST /api/email-import/sync`). Set it to `true` on a long-running deployment.
- With `EncryptionKey` empty the key is derived from `JWT:Secret` (with a warning in the log). Set a
  real one in production **and keep it stable**: changing it makes every stored credential
  undecryptable, and the mailbox has to be connected again.

### 3. Connect the mailbox

1. Sign in and complete the initial setup.
2. **Settings → Email import → Connect email** (Gmail is the only provider today).
3. Approve the read-only Gmail consent in the Google screen that opens.
4. Google redirects to Budgie's callback; Budgie stores the encrypted refresh token and sends you back
   to **Settings** with *"Mailbox connected!"*. The card then shows the mailbox address, the last sync
   and the auto-commit threshold.

Next, open **Email → Manage rules and inbox** to create the rules that match your bank's sender and
subject, then either review the staged emails or let those above the confidence threshold commit
automatically. Start with **Sync now** to see it work end to end.

### Troubleshooting

| Symptom | Cause / fix |
|---------|-------------|
| `redirect_uri_mismatch` | The registered URI does not match the one Budgie built. Check host and scheme: behind a reverse proxy the deployment must forward `X-Forwarded-Proto` (Budgie reads it), and in development the API listens on 5057. |
| `EmailImport.ProviderNotConfigured` | Client id/secret are empty. Fill them and restart the API. |
| `access_denied` right after picking the account | The account is not in **Test users** of a consent screen still in **Testing**. |
| Sync starts failing days after connecting | Google expires refresh tokens after **7 days** while the OAuth app is in **Testing**. Reconnect, or complete Google verification and publish the app. |
| `EmailImport.AlreadyConnected` | One mailbox per provider per user. Disconnect it before connecting another. |
| Connect fails inside the devcontainer | The Vite service proxies `/api` to `http://app:5057`, so the redirect URI is built as `http://app:5057/...` — Google rejects plain `http` for non-loopback hosts and your browser cannot resolve it. Run the frontend on your host instead (`cd src/apps/Budgie.Web && bun run dev`, whose default proxy target is `http://localhost:5057`) and register the `localhost:5057` URI. The callback then lands on the API host, so go back to `http://localhost:5173/settings` to see the connection. |

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
