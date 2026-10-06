# Request Tracker

[![CI](https://github.com/RuthGinsberger/request-tracker/actions/workflows/ci.yml/badge.svg)](https://github.com/RuthGinsberger/request-tracker/actions/workflows/ci.yml)

A full stack home assignment: an enterprise-style request-management system (.NET 10 + SQL Server + Angular) that handles 100k+ requests with server-side paging, filtering, search, sorting, optimistic concurrency, audit history, bulk updates and a small cache.

Full documentation is coming (see `REQUIREMENTS.md`, `PLAN.md` and `DECISIONS.md` for the current state).

## Run it (Docker Compose, any OS)

```
docker compose up --build
```

Then open http://localhost:8080. The first start builds the images, starts SQL Server, applies the migrations and seeds 100,000 requests (a few minutes in total); later starts skip seeding. Order: `db` → `seeder` (migrates + seeds, then exits) → `api` → `ui`. The seeder owns the migrations; the API never migrates.

| What | Where |
|---|---|
| UI (nginx; proxies `/api` to the API) | http://localhost:8080 |
| API | http://localhost:5280 (`/health`, `/api/requests`) |
| SQL Server (user `sa`, password `Dev_Passw0rd!` unless overridden) | `localhost,1433` |

- **OpenAPI UI:** off by default. Put `API_ENVIRONMENT=Development` in `.env` and open http://localhost:5280/scalar/v1.
- **Port conflicts / password:** copy `.env.example` to `.env` and change `UI_PORT`, `API_PORT`, `DB_PORT` or `MSSQL_SA_PASSWORD`.
- **Re-seed from scratch:** `docker compose run --rm seeder --reset`. **Wipe everything:** `docker compose down -v`.
- **Apple Silicon:** SQL Server runs under emulation; enable "Use Rosetta" in Docker Desktop. The first start is slower.

Local development without Docker (Windows + LocalDB) is described in `CLAUDE.md`.
