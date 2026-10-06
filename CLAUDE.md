# CLAUDE.md

## Session rules
Every session starts by reading `CLAUDE.md`, `REQUIREMENTS.md`, `PLAN.md`, `DECISIONS.md` and (once it exists) `docs/ARCHITECTURE.md`, then the code relevant to the task. `REQUIREMENTS.md` is the source of truth: never invent requirements, never substitute technologies, never contradict it. Do only the current task's scope. If something is unspecified, choose the simplest option consistent with the requirements and log it in `DECISIONS.md`. A task is done only when build, lint and all tests pass. At the end of each task: update `PLAN.md` status and log decisions in `DECISIONS.md`.

## Stack
.NET 10, ASP.NET Core (controllers), EF Core 10 + SQL Server (LocalDB for local dev and tests; SQL Server 2022 container in Compose and CI), FluentValidation, HybridCache, xUnit + WebApplicationFactory (integration tests against a real SQL Server chosen by connection string; no Testcontainers). Angular (latest stable, standalone, strict) + Angular Material + RxJS, ESLint, Vitest (the scaffolded runner). Do NOT use FluentAssertions v8+ (commercial license); use xUnit assertions or Shouldly. Do NOT add AutoMapper, MediatR, generic repositories or a repository layer over EF Core (R57).

## Structure
```
backend/
  RequestTracker.slnx
  src/RequestTracker.Domain          entities, enums, StatusTransitionPolicy (no dependencies)
  src/RequestTracker.Application     DTOs, query/command models, validators, mapping (projection expressions), services, abstractions (IAppDbContext, cache, current user)
  src/RequestTracker.Infrastructure  EF Core DbContext, configurations, migrations, cache implementation
  src/RequestTracker.Api             controllers, middleware/exception handling, DI composition, OpenAPI
  tools/RequestTracker.Seeder        console app: migrate + (re)seed data
  tests/RequestTracker.UnitTests
  tests/RequestTracker.IntegrationTests
frontend/                            Angular app (core/, features/requests/, shared/)
docs/                                ARCHITECTURE.md, WORK_PLAN.md, PERFORMANCE.md
perf/                                performance scripts
docker-compose.yml                   for evaluators and CI only (Docker is not available on the dev machine); backend/Dockerfile (targets api, seeder), frontend/Dockerfile + nginx.conf; docs/COMPOSE_SMOKE_CHECKLIST.md
```
References: Api→Application, Infrastructure; Infrastructure→Application, Domain; Application→Domain; Seeder→Infrastructure.

## Backend conventions
Nullable enabled, warnings as errors; async all the way with `CancellationToken` passed to every I/O call (no `.Result`/`.Wait()`); API returns DTOs only; reads use `AsNoTracking` + `Select` projection; manual mapping in one place per DTO; expected business outcomes (not found, conflict, invalid transition) are explicit results, not exceptions; exceptions are for unexpected failures; errors are `ProblemDetails` / `ValidationProblemDetails`; logging via `ILogger` message templates at meaningful points, never logging request/response bodies, header values, or connection strings; times in UTC via injected `TimeProvider`; DI lifetimes: DbContext and services scoped, `TimeProvider` and cache singleton.

## Frontend conventions
Standalone components, `OnPush`; components handle presentation and user interaction, services/stores own HTTP and state; typed models mirroring the API DTOs; all filtering/sorting/paging is done server-side.

## Environment
Windows dev machine, PowerShell. The DB is LocalDB: `Server=(localdb)\MSSQLLocalDB;Database=RequestTracker;Trusted_Connection=True;TrustServerCertificate=True` in `appsettings.Development.json` (in JSON the backslash is escaped as `\\`). **Docker is not available locally: never run Docker commands.** Dockerfiles and Compose are verified only in GitHub Actions.

**Node caveat:** the installed Node (v24.13.0) is older than the Angular 22 CLI minimum (v24.15.0). Until Node is upgraded, run every frontend command through a temporary newer Node without installing anything: prefix with `npx -p node@24` (e.g. `npx -p node@24 ng build`). If `node -v` is ≥ 24.15, drop the prefix.

## Commands (verified in Task 1)
- LocalDB: `sqllocaldb info MSSQLLocalDB`, `sqllocaldb start MSSQLLocalDB`
- Ad-hoc SQL: `sqlcmd -S "(localdb)\MSSQLLocalDB" -d RequestTracker -Q "..."` (database exists after Task 3)
- Backend (from repo root):
  - `dotnet build backend/RequestTracker.slnx`
  - `dotnet test backend/RequestTracker.slnx`
  - `dotnet format backend/RequestTracker.slnx --verify-no-changes`
  - run API: `dotnet run --project backend/src/RequestTracker.Api` (http://localhost:5280, `GET /health`, OpenAPI UI at `/scalar/v1` in Development)
  - integration tests create a throw-away database on the server in `INTEGRATIONTESTS_SQLSERVER` (a connection string, or a server name for Windows auth; default LocalDB); CI sets it to the SQL Server service container
- Frontend (from `frontend/`, with the Node caveat above): `npm ci`, `npx ng build`, `npx ng lint`, `npx ng test --watch=false`, `npx ng serve` (http://localhost:4200; `proxy.conf.json` forwards `/api` to the API on 5280, so start the API first)
- Migrations (from repo root; EF tools are a local tool in `.config/dotnet-tools.json`, run `dotnet tool restore` once):
  - add: `dotnet dotnet-ef migrations add <Name> --project backend/src/RequestTracker.Infrastructure --startup-project backend/src/RequestTracker.Api --output-dir Persistence/Migrations`
  - apply to LocalDB: `dotnet dotnet-ef database update --project backend/src/RequestTracker.Infrastructure --startup-project backend/src/RequestTracker.Api`
  - the design-time factory reads `ConnectionStrings__Default` from the environment, else falls back to LocalDB.
- Seeding (connection string from `ConnectionStrings__Default`, else LocalDB; 10–20 s for 100k with all indexes):
  - recreate and seed: `dotnet run --project backend/tools/RequestTracker.Seeder -- --reset`
  - options: `--count <n>` (default 100000), `--seed <n>` (default 42), `--anchor yyyy-MM-dd` (default today UTC; pin it to reproduce data across days), `--if-empty` (Compose: no-op when data exists), `--help`
  - without `--reset`/`--if-empty` a non-empty table is refused (exit 1); usage errors exit 2; Ctrl+C rolls back (exit 130)
- Compose (evaluators/CI only, never run locally): `docker compose up --build` → UI http://localhost:8080; the seeder owns migrations (D-077). The local dev path above (LocalDB + `dotnet run` + `ng serve`) is unchanged. Mirror the Dockerfile builds locally with `dotnet publish <proj> -c Release -o <dir> /p:UseAppHost=false` (from `backend/`) and `npx ng build --configuration production` (output `frontend/dist/frontend/browser`).
- CI workflows: added in Task 15.
