# Compose smoke checklist (input for the CI job in Task 15)

Run from the repo root on a clean runner: `docker compose up --build --detach --wait` (waits for every service with a healthcheck; `seeder` has none and must have exited).

| # | Check | Expected |
|---|---|---|
| 1 | `docker compose ps -a --format json` for `seeder` | exited, exit code 0 |
| 2 | `docker compose logs seeder` | contains `Done: 100,000 rows written, 100,000 in Requests` (first run) |
| 3 | `docker compose ps` | `db`, `api`, `ui` are `healthy` |
| 4 | `GET http://localhost:5280/health` | 200, `{"status":"Healthy",...}` |
| 5 | `GET http://localhost:8080/` | 200, HTML containing `<app-root` |
| 6 | `GET http://localhost:8080/requests?status=Open` (deep link) | 200 HTML (SPA fallback, not 404) |
| 7 | `GET http://localhost:8080/api/requests?pageSize=5` (through nginx) | 200, `items` has 5 entries, `totalCount` = 100000 |
| 8 | `GET http://localhost:8080/api/requests/summary` | 200, status counts sum to 100000 |
| 9 | Status update through the proxy: take an `id` and `version` from check 7, `PATCH /api/requests/{id}/status` with `If-Match: <version>`, header `X-User-Name: smoke`, a valid transition body | 200 |
| 10 | Same PATCH again with the old version | 409 |
| 11 | `docker compose run --rm seeder` (default `--if-empty`), then check 7 again | seeder exits 0 with `nothing to do`; `totalCount` unchanged (no duplicate data) |
| 12 | `GET http://localhost:5280/scalar/v1` | 404 (Production hides the OpenAPI UI) |

Always finish with `docker compose down -v`; on failure print `docker compose logs` first.
