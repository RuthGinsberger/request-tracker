# DECISIONS

Format: Context · Decision · Alternatives considered · Rationale · Trade-offs · Related requirements.

## D-001 Versions: .NET 10 LTS and latest Angular (A1)
- **Context:** Need a current, supported stack.
- **Decision:** .NET 10 (LTS), latest stable Angular (22 at scaffold time), standalone components.
- **Alternatives considered:** .NET 8 LTS; older Angular.
- **Rationale:** Newest LTS/stable; matches the brief.
- **Trade-offs:** The installed Node (24.13) is below the Angular 22 CLI minimum; frontend commands use `npx -p node@24` until Node is upgraded.
- **Related requirements:** R59, A1.

## D-002 SQL Server + `rowversion` as the concurrency token (A2, A14)
- **Context:** Optimistic concurrency must be enforced in the DB/server (R17, R19).
- **Decision:** SQL Server with a native `rowversion` column mapped as an EF Core concurrency token.
- **Alternatives considered:** MongoDB; manual integer version column.
- **Rationale:** DB-maintained token cannot be forgotten by application code; strong indexing and execution-plan tooling.
- **Trade-offs:** SQL Server-specific; `rowversion` is opaque (base64 in the API).
- **Related requirements:** R2, R4, R17, R18, R19.

## D-003 EF Core over Dapper (A3)
- **Decision:** EF Core, `AsNoTracking` + `Select` projection to DTOs on reads.
- **Alternatives considered:** Dapper; raw ADO.NET.
- **Rationale:** Built-in concurrency-token handling, migrations, LINQ composition for dynamic filters; projections keep reads lean.
- **Trade-offs:** Less control over SQL; mitigated by inspecting generated SQL and plans.
- **Related requirements:** R13, R36, R17.

## D-004 Prefix text search (A4)
- **Decision:** `LIKE 'term%'` on Title and OrganizationName.
- **Alternatives considered:** `LIKE '%term%'` (scan); SQL Server Full-Text Search.
- **Rationale:** Index-friendly (seek) on 100k+ rows.
- **Trade-offs:** No infix matching. Full-text search is the documented improvement.
- **Related requirements:** R8, R14, R36.

## D-005 `If-Match` / `ETag` for the version token (A14)
- **Decision:** Version sent in `If-Match`, returned in `ETag` and as `version` in DTOs.
- **Alternatives considered:** Version in request body only.
- **Rationale:** Standard HTTP conditional-request semantics; 412/409 mapping is well understood.
- **Trade-offs:** Bulk uses per-item versions in the body (see D-006).
- **Related requirements:** R17, R19.

## D-006 Bulk update: Partial Success (A15)
- **Decision:** Each item carries its own expected version; all items move to one target status; response 200 with per-item result (Updated / NotFound / Conflict / InvalidTransition) plus a summary.
- **Alternatives considered:** All-or-Nothing.
- **Rationale:** With up to 100 items and concurrent users, one stale row should not discard 99 valid updates; per-item results are actionable.
- **Trade-offs:** Client must inspect per-item results; no single atomic unit.
- **Related requirements:** R22, R23.

## D-007 Offset paging with total count (A16)
- **Decision:** Offset paging, max page size 100; keyset paging documented as the improvement.
- **Alternatives considered:** Keyset paging.
- **Rationale:** UI needs page numbers and totals; simplest correct option.
- **Trade-offs:** Deep pages and `COUNT` cost grow with data size.
- **Related requirements:** R6, R11, R35.

## D-008 HybridCache for aggregates (A11, A17)
- **Decision:** In-process HybridCache for global status/priority counts, short absolute expiration, explicit invalidation on single and bulk updates.
- **Alternatives considered:** IMemoryCache; Redis.
- **Rationale:** Small, justified scope; HybridCache has stampede protection and an L2 upgrade path.
- **Trade-offs:** Per-instance cache; with several instances, others stay stale until expiry. Redis with pub/sub invalidation is described, not built.
- **Related requirements:** R37–R41.

## D-009 No authentication; `X-User-Name` header (A8)
- **Decision:** Required `X-User-Name` header, validated server-side; UI user picker.
- **Alternatives considered:** Real auth (JWT/OIDC).
- **Rationale:** Auth is out of scope; audit needs ChangedBy.
- **Trade-offs:** Not secure; header is trusted.
- **Related requirements:** R20.

## D-010 LocalDB locally, SQL Server container in CI, no Testcontainers (A5, A6)
- **Decision:** Dev and tests use LocalDB (unique DB per run, dropped after); CI uses a SQL Server service container; evaluators use Docker Compose.
- **Alternatives considered:** Testcontainers; SQLite/in-memory provider.
- **Rationale:** Docker cannot run on the dev machine; real SQL Server is required for `rowversion` and plans.
- **Trade-offs:** Compose is verified only in CI.
- **Related requirements:** R42d, R67.

## D-011 Four backend projects (Domain, Application, Infrastructure, Api)
- **Decision:** Four src projects plus Seeder tool and two test projects.
- **Alternatives considered:** Single project; seven or more fine-grained projects.
- **Rationale:** Enforces dependency direction without ceremony.
- **Trade-offs:** More projects than a single-project solution.
- **Related requirements:** R49, R57.

## D-012 Controllers over minimal APIs
- **Decision:** ASP.NET Core controllers.
- **Alternatives considered:** Minimal APIs.
- **Rationale:** Familiar structure for several endpoints, attribute-based validation/ProblemDetails conventions.
- **Trade-offs:** Slightly more boilerplate.
- **Related requirements:** R49, R54.

## D-013 FluentValidation
- **Decision:** FluentValidation for request/query models.
- **Alternatives considered:** DataAnnotations.
- **Rationale:** Expressive, testable, consistent rules (R55).
- **Trade-offs:** Extra dependency.
- **Related requirements:** R11, R55.

## D-014 No AutoMapper, MediatR or repository layer
- **Decision:** Manual mapping via projection expressions; services use the DbContext directly.
- **Alternatives considered:** AutoMapper, MediatR, generic repositories.
- **Rationale:** EF Core already is unit of work/repository; avoids indirection and licensing concerns.
- **Trade-offs:** Slightly more explicit code.
- **Related requirements:** R51, R57.

## D-015 Transition policy, acting user, seeding, scope (A7, A9, A10, A12, A13)
- **Decision:** Fixed transition table (Completed terminal, same-status rejected); exact-match string filters for AssignedTo/OrganizationName; no create/edit/delete endpoints; date range on CreatedAt only; seeder CLI wipes/regenerates with bulk insert and writes no history.
- **Alternatives considered:** Configurable workflow; richer filters.
- **Rationale:** Matches the brief; simplest consistent option.
- **Trade-offs:** Not extensible without code changes.
- **Related requirements:** R3, R7, R16.

## D-016 Run Angular CLI with a temporary newer Node
- **Context:** Machine has Node 24.13.0; Angular 22 CLI requires ≥ 24.15.0.
- **Decision:** Scaffold and run frontend commands through `npx -p node@24` (no machine changes).
- **Alternatives considered:** Install newer Node; older Angular CLI (contradicts A1).
- **Rationale:** Keeps latest Angular without altering the system.
- **Trade-offs:** Slower commands; remove the prefix after upgrading Node.
- **Related requirements:** R59.

## D-017 Stale version returns 409, not 412 (design)
- **Context:** HTTP says a failed `If-Match` is 412 Precondition Failed; the assignment demands a Conflict response (R17).
- **Decision:** Stale version → `409` with `currentStatus`/`currentVersion`; missing `If-Match` → `428`; malformed → `400`.
- **Alternatives considered:** 412 (HTTP-purist).
- **Rationale:** Follows the assignment wording; 409 body lets the client refresh without another call.
- **Trade-offs:** Slightly non-standard use of `If-Match`; 412 documented as the alternative.
- **Related requirements:** R17, R19.

## D-018 Bulk returns 200 with per-item results, saved item by item (design)
- **Decision:** `200` with `summary` + `results`, not `207`. Each valid item is saved in its own `SaveChanges` (status + audit atomic); a concurrency exception marks that item Conflict, detaches it and continues. Whole-request errors (duplicates, >100, bad format) are `400` with nothing processed.
- **Alternatives considered:** 207 Multi-Status; one transaction (All-or-Nothing); set-based `ExecuteUpdate`.
- **Rationale:** 207 is poorly supported outside WebDAV; per-item saves give exact conflict classification and share the single-update code.
- **Trade-offs:** Up to 100 small transactions per call; `ExecuteUpdate` batching is the documented improvement.
- **Related requirements:** R22, R23.

## D-019 `AssignedTo` is nullable (design)
- **Decision:** `AssignedTo nvarchar(100)` allows null (unassigned); null assignees are excluded from lookups; no "unassigned" filter.
- **Alternatives considered:** Required column with a sentinel value.
- **Rationale:** R2 says "handler ID or name"; unassigned requests are realistic. Sentinels pollute data.
- **Trade-offs:** Null handling in filters and mapping.
- **Related requirements:** R2, R7.

## D-020 Enums stored as `tinyint`, serialized as strings (design)
- **Decision:** Status (1–4) and Priority (1–3) are `tinyint` with CHECK constraints; JSON uses string names.
- **Alternatives considered:** Strings in DB; int enums in JSON; lookup tables.
- **Rationale:** Compact indexes, numeric sort order = business order; readable, stable API contract.
- **Trade-offs:** Raw DB values are less readable in ad-hoc SQL.
- **Related requirements:** R2, R9, R13.

## D-021 Date-range semantics (design)
- **Decision:** `createdFrom`/`createdTo` are `yyyy-MM-dd`; from inclusive at 00:00 UTC, to inclusive through end of day (`CreatedAt < to + 1 day`); from > to → 400.
- **Alternatives considered:** Full timestamps; exclusive upper bound.
- **Rationale:** Matches what a date picker means; half-open comparison is index-friendly and avoids precision pitfalls.
- **Trade-offs:** No time-of-day filtering.
- **Related requirements:** R7, A12.

## D-022 Cache TTLs and keys (design)
- **Decision:** Summary 60 s, lookups 10 min (both configurable); summary invalidated by tag after every committed single/bulk update (bulk once); lookups TTL-only. The race with a concurrent read is bounded by the TTL.
- **Alternatives considered:** Caching list queries; versioned keys; no TTL.
- **Rationale:** Only the hot, expensive, shared aggregates are worth caching; short TTL bounds staleness, including across instances.
- **Trade-offs:** Up to 60 s stale in rare races and on other instances.
- **Related requirements:** R37–R41.

## D-023 Concurrency check: pre-check plus DB predicate (design)
- **Decision:** Service compares versions first (for a precise 409 body), then sets the original `RowVersion` so `UPDATE … WHERE RowVersion = @v` is the real guard; status and audit row in one `SaveChanges`.
- **Alternatives considered:** DB check only; explicit transaction with `UPDLOCK`.
- **Rationale:** Correctness rests on the DB predicate; the pre-check only improves the error and saves a write.
- **Trade-offs:** One extra comparison.
- **Related requirements:** R17–R20.

## D-024 Status change returns a result, not an exception (Task 3)
- **Decision:** `Request.ChangeStatus(newStatus, changedBy, utcNow)` returns `StatusChangeResult.Changed(history)` or `NotAllowed(from, to)` and leaves state untouched when rejected. Argument guards (blank/over-long `changedBy`, non-UTC time) throw, because they are programming errors, not business outcomes.
- **Alternatives considered:** Throwing a domain exception on a rejected transition.
- **Rationale:** Matches CLAUDE.md (expected outcomes are explicit results) and maps directly to `400 invalid-status-transition`/per-item bulk outcomes.
- **Trade-offs:** Callers must handle the result; the history entry is returned and the caller adds it to the DbContext.
- **Related requirements:** R16, R20.

## D-025 Entity encapsulation (Task 3)
- **Decision:** Entities have private setters and a private parameterless constructor for EF. `Request.Create` is the only way to build one; `Status`/`UpdatedAt` change only in `ChangeStatus`. `RequestStatusHistory` has an internal constructor, so only `Request` creates audit rows. `Create` accepts optional status/updatedAt solely so the seeder can generate realistic data without history. The history has no navigation property to `Request`, only the FK.
- **Alternatives considered:** Public setters; separate seeding-only constructor; navigation properties both ways.
- **Rationale:** Prevents bypassing the transition policy; keeps the model minimal (R57).
- **Trade-offs:** `RowVersion` is `private set` too; Task 8 sets its original value through `Entry(...).Property(...).OriginalValue`.
- **Related requirements:** R2, R16, R18, R20.

## D-026 Enums are `byte`-backed; UTC via a convention converter (Task 3)
- **Decision:** `RequestStatus`/`RequestPriority` are `enum : byte`, so EF maps them to `tinyint` natively (explicit `HasColumnType("tinyint")`, CHECK constraints) without `HasConversion<byte>()`. A `ConfigureConventions` rule maps all `DateTime` to `datetime2` with a converter that sets `Kind=Utc` on read.
- **Alternatives considered:** `HasConversion<byte>()` per property; per-property date converters.
- **Rationale:** Same schema as ARCHITECTURE.md with less configuration; one rule covers every timestamp.
- **Trade-offs:** Domain code must pass UTC `DateTime` (enforced by a guard).
- **Related requirements:** R2.

## D-027 Index naming, migrations location, design-time setup (Task 3)
- **Decision:** Indexes are named explicitly (`IX_Requests_<Columns>`, `IX_RequestStatusHistory_RequestId_ChangedAt`) and CHECK constraints as `CK_<Table>_<Column>`. Migrations live in `Infrastructure/Persistence/Migrations`. `dotnet ef` runs with Infrastructure as the project and Api as the startup project (which carries `Microsoft.EntityFrameworkCore.Design`); an `IDesignTimeDbContextFactory` reads `ConnectionStrings__Default`, falling back to LocalDB. `AddInfrastructure` registers the DbContext (scoped) but is not yet called by the Api (Task 5). EF packages are 10.0.12; EF tools are a local dotnet tool.
- **Alternatives considered:** Seeder as startup project; global `dotnet-ef`.
- **Rationale:** Stable names make Task 13 plan analysis readable; a local tool pins the version for CI and evaluators.
- **Trade-offs:** `dotnet format` rewrote the generated migration's encoding (BOM) so the format check passes.
- **Related requirements:** R34, R49.

## D-028 SqlBulkCopy, batched, one transaction (Task 4)
- **Context:** R3 needs 100k+ rows quickly and repeatably.
- **Decision:** The seeder builds `Request` objects lazily via `Request.Create`, fills a 10k-row `DataTable` per batch, and writes it with `SqlBulkCopy` (TableLock) inside one transaction. Id and RowVersion are not mapped. A cancel or failure rolls everything back.
- **Alternatives considered:** EF `AddRange` + `SaveChanges` batches (change tracking and per-row INSERT cost, minutes for 100k); raw `INSERT ... SELECT` generated in T-SQL (fast, but the distributions live outside tested C#).
- **Rationale:** Bulk copy measured ~8-10 s for 100k with 7 indexes; memory stays at one batch; domain guards still apply to every generated row.
- **Trade-offs:** Bypasses EF interceptors and the history rules (intended: no seeded history, A13).
- **Related requirements:** R3, A13.

## D-029 Reset strategy: drop and migrate (Task 4)
- **Decision:** `--reset` calls `EnsureDeletedAsync` then `MigrateAsync`, so the schema always comes from migrations. Without `--reset` a non-empty Requests table is refused (exit 1); `--if-empty` makes that a no-op (exit 0). `--reset` with `--if-empty` is a usage error. Before anything else the seeder waits for the SQL Server instance (connects to `master`, 6 attempts, exponential backoff capped at 15 s) so it works in Compose.
- **Alternatives considered:** `TRUNCATE`/`DELETE` (leaves history rows and keeps a stale schema); resetting only the identity.
- **Rationale:** Guarantees a clean, migration-built state, including the audit table.
- **Trade-offs:** Needs permission to drop the database (true for LocalDB and the Compose `sa`).
- **Related requirements:** R3.

## D-030 Determinism: seed plus date anchor (Task 4)
- **Decision:** `System.Random(seed)` (default 42) drives all choices; "now" is an `--anchor` date, default today's UTC midnight, so CreatedAt always spans the last 24 months. The same seed and anchor give byte-identical data (verified with a CHECKSUM_AGG over two resets). Across different days, pin `--anchor` to reproduce.
- **Alternatives considered:** Hard-coded anchor (data goes stale and the date range filters look old); real current time (never reproducible).
- **Rationale:** Keeps data realistic and still reproducible on demand.
- **Trade-offs:** Same seed on a different day shifts all dates.
- **Related requirements:** R3.

## D-031 Seeder configuration and generator placement (Task 4)
- **Decision:** The connection string comes from `ConnectionStrings__Default` or the LocalDB fallback, via the existing `DesignTimeDbContextFactory.ResolveConnectionString()` (made public); no appsettings or config packages. Generator, option parser and writer are public classes in the Seeder project, which the unit-test project references. The skewed organization pick (squared uniform; the largest org is ~6% of rows) and 30% unassigned New requests are documented in `RequestGenerator`.
- **Alternatives considered:** `Microsoft.Extensions.Configuration` in the seeder; generator in Application; `System.CommandLine`.
- **Rationale:** Smallest thing that works in Compose via env vars; no extra dependencies.
- **Trade-offs:** The test project references an exe project.
- **Related requirements:** R3, R57.

## D-032 Error handling: one ProblemDetails factory plus `IExceptionHandler` (Task 5)
- **Context:** R54 needs global, consistent errors; ARCHITECTURE.md section 5 fixes the `type`/`errorCode`/`traceId` contract.
- **Decision:** `ErrorCodes` (constants and the `urn:requesttracker:error:{code}` type) and `ApiProblems` (the only place ProblemDetails are built; `Normalize` also decorates framework-generated ones through `AddProblemDetails(CustomizeProblemDetails)`, and `UseStatusCodePages` gives unknown routes a ProblemDetails). `GlobalExceptionHandler` returns `500 internal-error` with a generic detail; in Development it adds the message and a `stackTrace` extension. It logs each exception once at Error, so the exception-handler middleware's own diagnostics are suppressed (`SuppressDiagnosticsCallback`). A cancellation while `RequestAborted` is set is logged at Debug only and answered with an empty 499. Status codes without a defined code get `errorCode = http-{status}`.
- **Alternatives considered:** Custom exception middleware; letting the middleware log and the handler stay silent.
- **Rationale:** Built-in pipeline, one construction site, no duplicated logging.
- **Trade-offs:** `SuppressDiagnosticsCallback` hides the middleware log for every exception, so the handler is the single source of truth.
- **Related requirements:** R15, R54, R56.

## D-033 Validation wiring: global filter plus shared model-state factory (Task 5)
- **Context:** Model-binding errors and FluentValidation errors must share one `ValidationProblemDetails` shape (R55).
- **Decision:** A global `ValidationFilter` runs the registered `IValidator<T>` for every action argument; `ApiBehaviorOptions.InvalidModelStateResponseFactory` builds binding errors through the same `ApiProblems.Validation`. Error keys are camelCased. The route id is bound as a `GetRequestQuery(int Id)` record with `[FromRoute]` (not an `{id:int}` constraint, which would give 404): `abc` fails binding, `0`/negative fails the validator, both give 400.
- **Alternatives considered:** `FluentValidation.AspNetCore` (unmaintained); validating manually in each controller; route constraint.
- **Rationale:** No extra package, one code path, controllers stay free of validation code.
- **Trade-offs:** A container lookup per argument type (cheap).
- **Related requirements:** R11, R55.

## D-034 OpenAPI: built-in generator plus Scalar UI, Development only (Task 5)
- **Decision:** `Microsoft.AspNetCore.OpenApi` serves `/openapi/v1.json`; `Scalar.AspNetCore` serves the UI at `/scalar/v1`; both mapped only in Development. JSON options (camelCase, enums by name, integers rejected for enums) live in `ApiJson` and feed both MVC and the minimal-API/OpenAPI options.
- **Alternatives considered:** Swashbuckle / Swagger UI (not shipped with .NET 10, maintenance risk).
- **Rationale:** First-party document generation; Scalar is a small, modern UI.
- **Trade-offs:** The Scalar page loads its script from a CDN, so it needs internet access.
- **Angular proxy:** the API has no CORS configuration, so the Angular dev proxy (`/api` to `http://localhost:5280`) is all that is needed; `proxy.conf.json` itself belongs to Task 10 (it does not exist yet).
- **Related requirements:** R49, R58.

## D-035 Integration-test database strategy (Task 5)
- **Context:** Real SQL Server is required (`rowversion`, plans); Docker is not available locally (A6, D-010).
- **Decision:** `SqlServerFixture` (one per run, xUnit collection fixture) reads `INTEGRATIONTESTS_SQLSERVER` (a connection string, or a bare server name used with Windows auth; default `(localdb)\MSSQLLocalDB`), creates `RequestTracker_Tests_<guid>` through `MigrateAsync` (so the real migrations are tested), wipes rows between tests with Respawn (keeping `__EFMigrationsHistory`) and drops the database in `DisposeAsync`. `ApiFactory` hosts the real API with the connection string injected via `AddInMemoryCollection`, which works because `AddInfrastructure` reads it lazily when the DbContext is first resolved. `TestData.AddRequestAsync` inserts specific rows. Tests run sequentially (one collection). A database-down test points a Production-environment host at a missing database.
- **Alternatives considered:** Testcontainers; EF InMemory or SQLite; a shared database with manual cleanup.
- **Rationale:** InMemory/SQLite have no `rowversion` and no SQL Server semantics, so concurrency and plan behaviour could not be tested; a unique disposable database makes Respawn safe.
- **Trade-offs:** A crashed run can leave a `RequestTracker_Tests_*` database behind; tests need LocalDB running. The solution uses xUnit 2.9.3 (v2), so fixtures use the `Task`-based `IAsyncLifetime`. `TimeProvider` is registered as `TimeProvider.System` but not faked yet; Task 8 adds a fake when timestamps matter.
- **Related requirements:** R42d.

## D-036 Smaller Task 5 choices
- **`AddInfrastructure()` takes no `IConfiguration`:** the connection string is read lazily from DI (see D-035). A missing string fails on first use, and the startup log line states `ConnectionStringConfigured` (never the value).
- **`ICurrentUser`:** `HeaderCurrentUser` exposes `UserName` only when `X-User-Name` appears once and passes `UserNameRules` (trimmed, 1-100); otherwise null. Task 8 answers null with `400 validation-failed`.
- **Version helpers:** `VersionToken` (base64 encode/decode of the 8-byte rowversion) is in Application so the mapping expression can use it; `ETagHeader.Format` (quoted strong ETag) is in Api and will be reused by updates.
- **Mapping:** `RequestMappings.ToDetails` is one `Expression<Func<Request, RequestDetailsDto>>`; `Version` is encoded client-side after the row is read (EF evaluates the last projection step in memory).
- **Health:** `/health` runs a DbContext check (`CanConnect`) and returns `{status, checks[{name,status}]}` with no exception text; 503 when unhealthy.
- **Logging:** Information startup summary; unhandled exceptions at Error; no Warning on 404 for GET by id (a missing id is a normal outcome and would be noise). `IAppDbContext` moved to `RequestTracker.Application.Abstractions`.
- **Related requirements:** R12, R13, R50-R56.

## D-037 Whitelisted sorting with an Id tie-breaker (Task 6)
- **Decision:** `sortBy` is parsed into the `RequestSortField` enum (createdAt, updatedAt, priority, status, title; case-insensitive, numbers and unknown names rejected). The query service maps each member to a typed key expression in a `switch`, then `OrderBy(key).ThenBy(Id)` (or the descending pair), so no user string reaches the query. Id follows the sort direction.
- **Alternatives considered:** `System.Linq.Dynamic`/string-based ordering; a dictionary of untyped selectors.
- **Rationale:** Injection-proof by construction; a total order makes `OFFSET/FETCH` deterministic when many rows tie (Priority and Status have 3-4 values). Priority/Status sort on the numeric `tinyint`, so Low < Medium < High.
- **Trade-offs:** Adding a sort field is a code change (enum member plus one switch arm).
- **Related requirements:** R9, R11.

## D-038 Date-range semantics in the list (Task 6)
- **Decision:** Implements D-021: `CreatedAt >= from 00:00 UTC` and `CreatedAt < to + 1 day`; `createdFrom > createdTo` gives 400 on `createdTo`. `createdTo = 9999-12-31` (no next day exists) simply applies no upper bound instead of overflowing.
- **Related requirements:** R7.

## D-039 Multi-value filters, strict parsing (Task 6)
- **Decision:** `status` and `priority` are repeatable query parameters bound as `string[]` and parsed by `QueryValues.TryParseEnum` (names only, case-insensitive, duplicates collapsed) into a SQL `IN`; one value becomes `=`. They are strings, not enums, in the query model so a bad value yields one error on the field whose message lists the allowed values, and numeric values (`status=1`) are not silently accepted, which MVC's native enum binder would do. `organizationName`/`assignedTo` are exact matches applied unless blank; whitespace-only counts as not provided. All optional model properties are nullable so MVC adds no implicit "required" errors.
- **Alternatives considered:** Native enum model binding; comma-separated values.
- **Trade-offs:** Parsing happens twice (validator, service); both use the same helper.
- **Related requirements:** R7, R11.

## D-040 Prefix search with `EF.Functions.Like` and our own escaping (Task 6)
- **Decision:** `search` is trimmed (blank means none, max 100 after trimming) and turned into `LIKE 'term%' ESCAPE '\'` by `LikePattern.StartsWith`, which escapes `\ % _ [`; applied as `Title LIKE … OR OrganizationName LIKE …`. Verified in the generated SQL: `[r].[Title] LIKE @pattern ESCAPE N'\'`.
- **Alternatives considered:** `string.StartsWith` (EF's translation of a parameter has varied across versions and may not be seek-friendly); `LIKE '%term%'` (scan); full-text search.
- **Rationale:** Matches ARCHITECTURE section 13 exactly, is index-friendly (a prefix range on `IX_Requests_Title` / the organization index) and the escaping is unit-tested and independent of EF internals. Trade-off: no infix matching (A4); full-text search is the documented improvement.
- **Related requirements:** R8, R36.

## D-041 Paging and count (Task 6)
- **Decision:** `COUNT(*)` on the filtered query first; the page query runs only when `(page-1)*pageSize < totalCount`, so empty results and pages past the end cost one round trip and return 200 with empty `items`. `totalPages = ceil(total / pageSize)` computed in `long` (0 when no matches). `page` is capped at 1,000,000 so the offset cannot overflow.
- **Alternatives considered:** `COUNT(*) OVER()` in the page query (returns no total for an empty page); always running both queries.
- **Trade-offs:** Two queries per normal request; the count over a multi-column filter is a Task 13 candidate.
- **Related requirements:** R6, R11, R36.

## D-042 Query model, DTO and wiring (Task 6)
- **Decision:** `RequestListQuery` (class with `init` properties and defaults, bound with `[FromQuery]`), `RequestListQueryValidator` (picked up by the global `ValidationFilter`), `RequestListItemDto` plus `RequestMappings.ToListItem` (a separate DTO and expression from the details one, same fields), `PagedResult<T>`. `RequestQueryService` now also takes an `ILogger` and logs one Debug line per list (counts and sort field only, never the search text or filter values).
- **Related requirements:** R5, R13, R55, R56.

## D-043 EF command logging in Development (Task 6)
- **Decision:** `appsettings.Development.json` sets `Microsoft.EntityFrameworkCore.Database.Command` to Information, so generated SQL is visible when running locally; parameter values are not logged (no sensitive-data logging). Production keeps the default.
- **Related requirements:** R36, R56.

## D-044 Cache abstraction, placement and lifetimes (Task 7)
- **Decision:** `IRequestReadCache` (`GetSummaryAsync`, `GetLookupsAsync`, `InvalidateSummaryAsync`) lives in Application; `RequestReadCache` in Infrastructure wraps `HybridCache` (`Microsoft.Extensions.Caching.Hybrid` 10.10.0, in-process L1). `HybridCache` is a singleton; the wrapper is scoped because the cache factories use the scoped DbContext through `RequestSummaryService`. Keys `requests:summary:v1` / `requests:lookups:v1`, tags `summary` / `lookups`. Absolute expiration is set for both cache levels. Cache misses are logged at Debug, invalidation at Information. A factory exception is not cached by HybridCache and surfaces as the usual 500 ProblemDetails; the caller's cancellation token reaches the SQL query.
- **Alternatives considered:** `IMemoryCache` (no stampede protection, no L2 path); a singleton cache taking `IServiceScopeFactory` (hidden service location); caching in the controller.
- **Rationale:** Services stay free of cache code, controllers depend on one abstraction, and the update tasks need only one call.
- **Related requirements:** R37, R38, D-022.

## D-045 Cache options bound and validated at startup (Task 7)
- **Decision:** `CacheOptions` (`Cache:SummaryTtlSeconds` default 60, `Cache:LookupsTtlMinutes` default 10) bound with `BindConfiguration` and `ValidateOnStart`; `CacheOptionsValidator` rejects values below 1 or above 1 day (a zero TTL would silently disable caching). Defaults are also written in `appsettings.json`.
- **Related requirements:** R38.

## D-046 Summary and lookups queries (Task 7)
- **Decision:** Two GROUP BY queries (status, priority), each returning at most 4 and 3 rows; `SummaryMappings.ToSummary` fills missing enum members with 0 in enum order and sets `totalCount` to the sum of the status counts. `generatedAtUtc` comes from the injected `TimeProvider`. Lookups are `SELECT DISTINCT` on OrganizationName and non-null AssignedTo, sorted in memory with ordinal-ignore-case so the order does not depend on the database collation. The two summary queries are not in one snapshot, so under concurrent writes the priority total can differ from the status total by a row; accepted for an advisory aggregate.
- **Alternatives considered:** One query with conditional aggregation; sorting in SQL (collation-dependent).
- **Related requirements:** R10, A11.

## D-047 Why these data are cached (R40) and what is not (Task 7)
- **Decision:** The summary is a full-table aggregate over 100k+ rows, identical for every user, read on every page load and changed only by the comparatively rare status update, so it has a high hit rate and a cheap invalidation. Lookups are two DISTINCT scans of wide columns that no endpoint can change (A10), so they need no invalidation, only a long TTL. List queries are not cached: the key space (filters x sort x page) is huge and the hit rate would be low.
- **Operational note:** Lookups are TTL-only, so re-running the seeder while the API runs shows new organizations or assignees only after up to 10 minutes or an API restart. The summary shows seeder changes within 60 s.
- **Measured (LocalDB, 100k rows, Development):** summary cold 497 ms (includes first-request JIT) vs warm 7 ms; lookups cold 213 ms vs warm 1.6 ms.
- **Related requirements:** R37, R40.

## D-048 Invalidation timing and multiple instances (Task 7; R41)
- **Decision:** `InvalidateSummaryAsync` removes the `summary` tag and must be called only after `SaveChanges` succeeds (Tasks 8 and 9; bulk calls it once). Race: a read that started before the commit can finish after the invalidation and re-cache the pre-commit value; the TTL (60 s) bounds that window, and `generatedAtUtc` makes it visible. Strictness would need versioned keys or write-through, which is over-engineering here.
- **Multiple instances:** every instance has its own L1, so an update on instance A clears only A; B and C serve the old summary until their TTL expires (at most 60 s) and may show different numbers for that time. Options: accept it (current choice, the TTL is the bound); or register a distributed L2 (for example Redis through `AddStackExchangeRedisCache`, which HybridCache uses automatically) so values are shared, plus a pub/sub backplane that broadcasts tag invalidations so each instance clears its L1 (HybridCache does not do cross-node L1 invalidation itself). Neither is built (A17); both are composition changes in `AddInfrastructure`.
- **Tests:** the integration harness clears both tags between tests (the API and its cache live for the whole run). The cache-hit proof inserts a row directly in the database, shows the second call unchanged (same `generatedAtUtc`), then shows the new count after `InvalidateSummaryAsync`.
- **Related requirements:** R39, R41.

## D-049 Check order of the status update (Task 8)
- **Decision:** Exactly as ARCHITECTURE.md section 5: input 400 (id, body status via the validation filter; `X-User-Name` in the controller) → 428 missing `If-Match` → 400 `invalid-version` → 404 → 409 stale version → 400 `invalid-status-transition` → 200. Version is checked before the transition rule, so a stale client always learns the row changed (the transition may be valid against the new state). `StatusChangeClassifier` is the pure, unit-tested form of the last two steps.
- **Trade-off:** The user-name error is raised after id/body validation, so a request with several input problems reports the id/body ones first.
- **Related requirements:** R15–R17.

## D-050 409, not 412, for a stale version (Task 8)
- **Decision:** Implements D-017. The 409 body carries `currentStatus` and `currentVersion` so the client can refresh without another GET. A weak, `*`, unquoted or list `If-Match` is 400 `invalid-version` (`ETagHeader.TryParseVersion`); a missing one is 428.
- **Related requirements:** R17, R19.

## D-051 Single `SaveChanges` for status and audit; DB predicate is the guard (Task 8)
- **Decision:** `RequestStatusChanger.ApplyAsync` (shared with bulk) loads nothing itself: the caller passes a tracked entity. It classifies (fast 409 / 400), calls `Request.ChangeStatus`, sets `Property(RowVersion).OriginalValue = expectedVersion`, adds the history row and calls one `SaveChangesAsync`, so EF issues `UPDATE ... WHERE Id=@id AND RowVersion=@expected` and the history `INSERT` in one implicit transaction. `DbUpdateConcurrencyException` (0 rows) → the entity and the history row are detached (so a bulk loop does not retry them), the current status/version is read with `GetDatabaseValuesAsync`, and the outcome is `Conflict`.
- **Why safe:** the pre-check is only an optimization; two writers that both passed it are separated by SQL Server's row lock and the `RowVersion` predicate, and the loser's audit insert rolls back with its UPDATE.
- **Alternatives considered:** explicit transaction with `UPDLOCK`; DB check only.
- **Related requirements:** R17–R20.

## D-052 Cache invalidation timing, logging (Task 8)
- **Decision:** `RequestStatusService` calls `IRequestReadCache.InvalidateSummaryAsync` only after `ApplyAsync` returned `Updated` (never for 404/409/400, never before the commit); the changer does not invalidate, so bulk can do it once. Logging in the changer: Information on success (request id, from, to), Warning on conflict (request id; pre-check and race). The user name and header values are never logged.
- **Related requirements:** R39, R56.

## D-053 History endpoint and smaller choices (Task 8)
- **Decision:** `GET /api/requests/{id}/history`: `AnyAsync` for 404, then a no-tracking projection (`StatusHistoryItemDto.Projection`) ordered `ChangedAt desc, Id desc`; empty array when no changes. `RequestMappings.ToDetailsDto` reuses the compiled `ToDetails` expression for the in-memory entity so the DTO mapping still lives in one place. The body `status` is a string parsed by `QueryValues.TryParseEnum` (names only, case-insensitive; numbers rejected). If the row vanishes between read and write the outcome is 404 (rows are never deleted, so this is defensive).
- **Related requirements:** R20, R21.

## D-054 Bulk update is Partial Success, not All-or-Nothing (Task 9, R23)
- **Decision:** Each item succeeds or fails on its own. The call answers `200 { summary, results }` in request order; `outcome` is `updated | notFound | conflict | invalidTransition`, `newVersion` only for `updated`, a short `message` only for failures. Malformed input (0 or >100 items, duplicate ids, unparsable version, bad `targetStatus`, missing `X-User-Name`) is a whole-request `400` and nothing is processed.
- **Why:** the items are unrelated requests edited by different people; one stale or already-completed row must not discard the other 99 valid changes. Under concurrent use an All-or-Nothing batch of 100 would fail almost every time, and its only benefit (atomicity across unrelated rows) has no business meaning. The per-item outcome tells the client exactly what to reload and retry.
- **Alternatives:** All-or-Nothing (one transaction, any failure rolls back everything; simple to reason about but brittle and gives the client a single error); `207 Multi-Status` (WebDAV semantics, poorly handled by clients and proxies; the request itself was valid, so `200` with a typed body is simpler); one batched `SaveChanges` with conflict retry (fewer round trips, but one failed row aborts the batch, so the batch must be re-split and retried, with more complex code for the same result).
- **Consequences for the client:** `200` does not mean "everything worked"; it must read `summary`/`results`. Retrying only the failed items is safe (updated items now have a new version, which the response returns). Failed items are never partially applied. Because the audit row and status are saved together per item, history only exists for updated items.
- **Trade-off:** up to 100 small transactions per call (one round trip each); a set-based `UPDATE ... OUTPUT` would be faster but cannot classify conflicts per row. Revisit only if profiling (Task 13) shows a need.
- **Related requirements:** R22, R23.

## D-055 Bulk flow and reuse of the single-update component (Task 9)
- **Decision:** `BulkStatusService` loads all ids in one tracked query, then walks the items in request order: not in the dictionary → `notFound`; otherwise `RequestStatusChanger.ApplyAsync` (the Task 8 component, so version check, transition rule, one `SaveChanges` for status + audit and the race handling are not duplicated). A `DbUpdateConcurrencyException` inside the changer already detaches the entity and its history row, so the loop just continues. A concurrent single update during the bulk therefore shows up as `conflict` (pre-check or `RowVersion` predicate). The summary cache is invalidated once, after the loop, only if at least one item was updated.
- **Related requirements:** R17, R22, R39.

## D-056 Cancellation and logging of the bulk update (Task 9)
- **Decision:** the token is passed to every call and checked before each item. On cancellation the exception propagates (no response, not logged as an error); items already saved stay committed (consistent with Partial Success), the item in flight rolls back with its own transaction, and the cache is not invalidated by that call, so counts can be up to one TTL (60 s) stale. Logging: one Information line with the summary counts, one Warning with the conflict count when > 0; no per-item lines from the service (the changer's existing per-item Information/Warning lines remain), never user names or bodies.
- **Related requirements:** R12, R39, R53, R56.

## D-057 Bulk DTO details (Task 9)
- **Decision:** the request carries `version` as plain base64 without quotes (as in DTOs); the controller decodes it after the validation filter. `BulkItemOutcome` is serialized camelCase (`notFound`) with a property-level converter, because the global PascalCase enum converter wins over a type-level attribute. Null `newVersion`/`message` are omitted. The validator reports per-item problems under `items[i].id`/`items[i].version`.
- **Related requirements:** R22.

## D-058 Signals for state, RxJS for async streams (Task 10)
- **Decision:** `RequestsApiService` returns cold `Observable`s (HTTP is a stream that can be cancelled by unsubscribing, which `switchMap` in Task 11 relies on). Synchronous, UI-facing state is a signal (`CurrentUserService.userName`/`isSet`); `toSignal` bridges the lookups call into the picker. Components never call `HttpClient`.
- **Alternatives considered:** Promises/`async` services (no cancellation); signals-only `resource` (weaker fit for the debounce/switchMap pipeline in ARCHITECTURE section 10).
- **Related requirements:** R26, R31.

## D-059 Interceptor responsibilities (Task 10)
- **Decision:** Two functional interceptors, each with one job. `userNameInterceptor` adds `X-User-Name` to non-GET/HEAD/OPTIONS calls when a user is selected; with no user it adds nothing and the server answers 400 (the UI will block mutations earlier through `isSet`). `errorInterceptor` (outermost) maps every `HttpErrorResponse` to an `ApiError` (status, `errorCode`, title, detail, field errors, `currentStatus`/`currentVersion`/`requestedStatus`, `traceId`) and rethrows it; status 0 gives a friendly network message; a non-ProblemDetails body gives a generic fallback. `If-Match` is not an interceptor concern: `ifMatchHeader()` is the single helper and `updateStatus` is its only caller. Query to `HttpParams` lives in `toHttpParams()` only (repeatable status/priority, empty values omitted). The API base URL is `environment.apiBaseUrl = '/api'` (relative, works behind the dev proxy and nginx).
- **Related requirements:** R17, R31, R50.

## D-060 User persistence (Task 10)
- **Decision:** The selected name is kept in `localStorage` (key `requesttracker.userName`), read once at start-up, with every storage access in try/catch. After the lookups load, `validateAgainst(assignees)` clears a stored name that is no longer an assignee (for example after re-seeding). No default user is chosen: the picker starts empty until the user selects one.
- **Alternatives considered:** `sessionStorage` (loses the choice per tab); auto-selecting the first assignee (hides that an identity is required).
- **Related requirements:** A8.

## D-061 Smaller Task 10 choices
- `strict: true` added to `tsconfig.json` (the scaffold did not enable it); no `any` in the code. `proxy.conf.json` (`/api` to `http://localhost:5280`) is wired through `serve.options.proxyConfig`, so plain `ng serve` works; no `ng` command changed. Material theme is the scaffold's; the shell is a sticky toolbar plus `<main>`. Routes: `''` redirects to `/requests`, which lazy-loads a placeholder page; unknown paths redirect to `/requests`. The user picker lives in `core/user/` next to its service. Verified live: with the API on 5280 and `ng serve`, `GET localhost:4200/api/lookups` returns the lookups through the proxy.
- **Related requirements:** R49, R50.

## D-062 Store pattern without NgRx (Task 11)
- **Decision:** `RequestsListStore` is a plain `@Injectable()` provided in the page component's `providers` (one instance per page, destroyed with it). Query state lives in a private `BehaviorSubject<RequestsListState>`; the view model (`loading | loaded | empty | error`, items, totalCount, error) is a signal fed by the pipeline; components read signals and call `set*` methods. `start(initialState)` builds the pipeline once, so the first request already uses the URL state.
- **Alternatives considered:** NgRx / signal-store (a dependency and ceremony for one screen, R57); signals + `resource` (weaker fit for debounce and cancellation); state in the component (breaks R31).
- **Related requirements:** R24–R27, R31, R57.

## D-063 Pipeline: debounce, switchMap, error handling (Task 11)
- **Decision:** `search$ → debounce → trim → distinctUntilChanged` commits the term into the state; state changes and Retry go through one `switchMap` to `RequestsApiService.list`, with `catchError` inside it (an error ends only that inner request) and `startWith(loading)`. Debounce is 300 ms, except an emptied box reloads at once (`debounce` returning an immediate notifier). Any change to search/filters/sort/page size resets to page 1; paging keeps filters; identical states are not re-sent. A 200 with no rows but a positive total on page > 1 (filter shrank the result) resets to page 1. Loading keeps the previous rows; an error clears them and shows the server's message (field messages for 400) with Retry. `clearFilters` keeps sort and page size and is one request.
- **Why switchMap:** only the latest query matters; it unsubscribes the previous HTTP call (browser abort) and drops its late answer. `mergeMap` would render responses in arrival order (stale results can overwrite fresh ones); `concatMap` would queue every intermediate request and make the user wait.
- **Related requirements:** R24–R27.

## D-064 URL state (Task 11)
- **Decision:** The page parses `queryParamMap` once on start (`parseListState`: unknown or invalid values fall back to defaults, inverted date range dropped, page size must be one of 10/20/50/100) and an `effect` writes the state back with `router.navigate([], { queryParams, replaceUrl: true })`; defaults are omitted from the URL. The URL is not read again after start (replaceUrl adds no history entries, so there is no back/forward to follow).
- **Related requirements:** R24, R27.

## D-065 Filter UI choices (Task 11)
- **Decision:** Status/priority are multi-selects; organization (about 300) and assignee (about 40) are single selects of the `/api/lookups` values, with "Any" (exact-match filters, A9). Date range uses the Material range picker with the native date adapter; dates are sent as `yyyy-MM-dd` built from local calendar parts (no `toISOString`, which shifts the day). A picker choice is sent once when the picker closes; typed dates are sent at once; an invalid or inverted range is not sent and shows an error. The search box keeps its own text and is overwritten from the state only when the committed search changes (clear filters), so debounce commits never fight typing. Rows show `createdAt`/`updatedAt` through `DatePipe` (local time). Sort is only on the five API-whitelisted columns (`mat-sort-header` carries the API field name; sort clearing is disabled). Page sizes 10/20/50/100.
- **Alternatives considered:** autocomplete for organization (not needed at 300 options).
- **Related requirements:** R24, R7.

## D-066 Smaller Task 11 choices
- Lookups load in the store; if the call fails the selects are just empty. The initial bundle warning (565 kB vs 500 kB budget) comes from the Material shell, not the lazy list page.
- Not exercised in a real browser: only the API through the dev proxy (search, multi filters, sort, paging, date range, empty, page past the end) plus unit tests; the visual flow of the date picker and mat-sort was not clicked through.
- **Related requirements:** R27, R31.

## D-067 409 UX flow in the status update (Task 12)
- **Decision:** `StatusUpdateService` (provided by the page) sends PATCH with `If-Match` from the row's version. On 409 it shows "This request was changed by someone else…", reloads that row with `GET /api/requests/{id}` (the 409 extensions lack title, dates and priority), replaces it in place and refreshes the summary. It never retries automatically: the user sees the fresh status and chooses again, and the action is offered from the fresh row's allowed targets. If the row refresh fails the current page is reloaded. 400 shows the server's message and refreshes the row; 404 shows a message and reloads the list; network or 5xx shows the message and reloads list and summary (the outcome of the write is unknown). A request id is "pending" while in flight, which disables its action and blocks a second submit; with no current user nothing is sent and the user is told to pick one in the toolbar.
- **Alternatives considered:** auto-retry with the `currentVersion` (silently overwrites a decision made on stale data); using only the 409 extensions (no other fields).
- **Related requirements:** R28, R27.

## D-068 Client-side transition mirror is UX only (Task 12)
- **Decision:** `allowedTargets()` in `core/models/status-transitions.ts` mirrors ARCHITECTURE section 3 so the menu offers only sensible targets (Completed has none). It is never used to reject anything: the server's `StatusTransitionPolicy` and the `RowVersion` predicate stay the authority, and a mismatch (stale table, stale row) just produces the 400/409 handled above. A unit test pins the table.
- **Related requirements:** R28, R31.

## D-069 Summary refresh strategy (Task 12)
- **Decision:** `SummaryStore` loads on page start and `refresh()` runs after every successful update and after a 409/400 row refresh. The server invalidates its cached summary on commit (D-052), so the next read is current; no client-side counter arithmetic. Old numbers stay visible during a refresh, a failed refresh keeps them, only a failed first load is an error state with Retry. A newer load cancels an older in-flight one.
- **Related requirements:** R29, R27, R39.

## D-070 Keeping the list consistent after an update; history dialog (Task 12)
- **Decision:** On 200 the row is replaced in place. The current page is then reloaded (`retry`, which cancels any in-flight load through `switchMap`) when the new status no longer matches an active status filter, when the sort is by status or updatedAt, or when a list load was in flight during the update. `HistoryDialogComponent` (MatDialog, own `HistoryStore`) loads on open with loading, "No status changes yet", and error + Retry states; entries show local time, previous → new status and user, newest first as the API orders them. Row actions are two buttons (a "Change status" menu of allowed targets and "History"). Bulk UI is out of scope.
- **Not verified:** the two-window conflict was reproduced against the real API with two clients holding one version (A 200, B 409, fresh GET, B retry 200, summary and history changed) but the UI was not clicked through in a browser.
- **Related requirements:** R27, R28, R30.

## D-071 Measurement method and what counts as evidence (Task 13)
- **Decision:** `perf/` holds the whole analysis: `Scenarios.ps1` (the EF-generated SQL of 17 list/summary scenarios, replayed with `sp_executesql` and the same parameter types), `Run-SqlPerf.ps1` (actual plan from an empty plan cache, 3 warm-ups, median of 7 warm runs of logical reads/CPU/elapsed, one cold run after `DROPCLEANBUFFERS`), `Get-PlanSummary.ps1`, `Measure-Api.ps1` (p50/p95 through a Release API started by the script, plan cache cleared, separate TTL-1h and TTL-1s instances for summary hit/miss), `Format-Results.ps1`, `index-sizes.sql`, `write-cost.sql`. Results and `.sqlplan` files are committed in `docs/performance/`; the narrative is `docs/PERFORMANCE.md`. Logical reads are the primary metric (LocalDB 2019 Express on a laptop, whole database cached, 1 ms timer).
- **Alternatives considered:** k6 (not installed, would add a dependency); `STATISTICS PROFILE` text plans (no saved `.sqlplan`); averaging runs (hides the first-compile outliers).
- **Rationale:** Reproducible with documented commands and no Docker; every number in the document is from a committed file.
- **Trade-offs:** The SQL harness replays EF's statement text, so it must be kept in sync if the query shape changes; `DBCC FREEPROCCACHE` clears the plan cache of the whole LocalDB instance (acceptable for a dev instance).
- **Related requirements:** R32, R33.

## D-072 Plan reuse (parameter sniffing) is part of the measurement (Task 13)
- **Context:** The first replay of page 4000 read 245,506 pages instead of ~2,000: it reused the plan the API had cached for `OFFSET 0`, because `OFFSET @p` is a parameter and the statement text is identical for every page.
- **Decision:** Scenario 07 reproduces this deliberately (page-1 execution in the same session after an empty plan cache); 07b is the same statement with `OPTION (RECOMPILE)`. `Measure-Api.ps1` clears the plan cache first and orders requests so the default page is compiled before the deep page, and the common search before the rare one, exactly as in a running API. No code change for it (no `RECOMPILE` interceptor): the covering index bounds the damage (245,183 → 1,582 reads), keyset paging is the real fix (documented, out of scope).
- **Related requirements:** R35.

## D-073 Covering `IX_Requests_CreatedAt` (migration `CoveringCreatedAtIndex`) (Task 13)
- **Decision:** `IX_Requests_CreatedAt` is recreated with `INCLUDE (Title, OrganizationName, Status, Priority, AssignedTo, UpdatedAt, RowVersion)`, so the ordered scan serves the whole list projection without key lookups.
- **Evidence (100k rows, warm medians):** default page 1: 182 → 6 reads; deep page 4000 with a reused plan: 245,183 → 1,582 reads (API p50 414 → 93 ms); common prefix search page: 1,770 → 14; status / priority / assignee / large-org / date-range page queries: 171–549 → 4–23; rare search through the API 479 → 66 ms.
- **Cost / regressions:** index 225 → 1,975 pages (+13.7 MB); status update 11 → 22 logical reads together with D-074; seeding 100k rows ~7 s → 10–17 s; a rare search (1 match) page query went 11 → 1,975 reads (an ordered scan instead of seeks) and the date-range count 5 → 22. Accepted: the worst case is now bounded (one scan of the index) instead of unbounded.
- **Alternatives considered:** leave as is and rely on plan luck (rejected: unbounded); `OPTION (RECOMPILE)` via an interceptor (per-request compile cost, extra machinery); a covering index per sort/filter (storage).
- **Related requirements:** R14, R34, R35.

## D-074 Single-column sort indexes (migration `SortColumnIndexes`) (Task 13)
- **Decision:** Add `IX_Requests_UpdatedAt`, `IX_Requests_Priority`, `IX_Requests_Status`. The clustered key `Id` appended to each is exactly the tie-breaker of every list sort (D-037), so `ORDER BY X, Id` is an ordered scan that stops after the page.
- **Evidence:** sort by updatedAt / priority / status, page 1: 2,010 reads and a Top-N sort of 100,000 rows (207–266 ms) → 178 / 184 / 191 reads (1–4 ms). They are also the narrowest indexes, so unfiltered and status/priority counts and the summary now read 138 pages instead of 151.
- **Cost:** 501 pages (3.9 MB) and three more index entries rewritten on each status update (`Priority` never changes, so it adds none).
- **Alternatives considered:** leave unindexed (every column-header click costs 250 ms at 100k and scales linearly); composite sort indexes with filters (combinatorial).
- **Related requirements:** R9, R34.

## D-075 No index removed; what each earns (Task 13)
- **Decision:** All indexes of the architecture plan stay. Ablation (one index disabled, 5 measured runs, final configuration): `IX_Requests_Status_CreatedAt` is needed for the count of status + date range (10 vs 250 reads); `IX_Requests_Status_Priority` for the count of status + priority (10 vs 1,975); `IX_Requests_AssignedTo_CreatedAt` for the assignee count (17 vs 1,975); `IX_Requests_Title` for the prefix-search count (72 vs 1,975) and sort by title (935 vs 1,975); the three sort indexes as in D-074. `IX_Requests_Status_Priority` no longer serves the summary or plain counts (138 reads with or without it) and `IX_Requests_Status_CreatedAt` no longer serves page queries; both are small (1.2 and 1.9 MB), their remaining gap grows linearly with the table, and they are the first to drop if write cost matters.
- **Low-selectivity filters:** status (25%) and priority (20%) are served by ordered scans that stop after 20 matches (9 reads), not forced seeks; their counts use narrow single-column indexes.
- **Related requirements:** R34, R35.

## D-076 Smaller Task 13 choices
- Seeding with all indexes takes 10–17 s (was ~7 s); CLAUDE.md updated. Creating the indexes after the load would be faster but `--reset` runs the migrations first; not worth the complexity.
- No new tests: the migrations change no behaviour (all 361 existing tests pass against the migrated schema); the performance claims are verified by the committed scripts, not by assertions on reads.
- The bottlenecks (deep OFFSET, count over large filtered sets, leading-wildcard and rare-term search) and the 10M-row extrapolation are documented in `docs/PERFORMANCE.md` sections 7.1–7.5 with trade-offs of keyset paging, cached/approximate counts and full-text search; none is implemented (out of scope).
- **Related requirements:** R35, R36.

## D-077 Seeder owns migrations in Compose; the API never migrates (Task 14)
- **Decision:** `db` (healthy) → `seeder` (`--if-empty`: migrate, seed 100k only when empty, exit 0) → `api` (`service_completed_successfully`) → `ui` (api healthy). Re-running `up` never duplicates data; re-seed with `docker compose run --rm seeder --reset`, wipe with `docker compose down -v`.
- **Rationale:** One owner of the schema removes any startup race between API instances and migrations.
- **Related requirements:** R58, R67.

## D-078 nginx reverse proxy instead of CORS (Task 14)
- **Decision:** The UI image is nginx serving the SPA (`try_files` fallback to `index.html`) and proxying `/api/` to `api:8080`; the API enables no CORS. Gzip is off for `/api/` because nginx weakens ETags of compressed responses.
- **Rationale:** Same origin as `ng serve` + `proxy.conf.json`; `environment.apiBaseUrl` is already the relative `/api`.

## D-079 Images, ports and healthchecks (Task 14)
- **Decision:** One `backend/Dockerfile` with targets `seeder` (runtime image) and `api` (aspnet image), both `USER app`, sharing one restore/publish stage; `frontend/Dockerfile` is `node:24-alpine` → `nginx:1.28-alpine`. Tags are pinned to major/minor, not digests. Host ports: UI 8080, API 5280 (also the dev port), DB 1433, all overridable via `.env` (`UI_PORT`, `API_PORT`, `DB_PORT`). The API runs in Production in Compose (no OpenAPI UI); `API_ENVIRONMENT=Development` enables `/scalar/v1`.
- **Healthchecks:** db `sqlcmd` from `/opt/mssql-tools18` (`-C`), start period 30 s; api `curl` on `/health` (curl installed deliberately via apt, the aspnet image has none); ui busybox `wget` on 127.0.0.1 (not `localhost`, which may resolve to IPv6).
- **Trade-offs:** apt in the API image needs network at build time and adds a few MB; the SA password is embedded in the connection string, so `;` and `$` are unsupported in an override (documented).

## D-080 Compose verified only in CI (Task 14)
- **Decision:** Docker is unavailable locally, so the Dockerfiles were checked by running the same `dotnet restore/publish` and `npm ci` + `ng build --configuration production` commands, checking every COPY path against the repo, and re-reading the nginx and Compose files. `docs/COMPOSE_SMOKE_CHECKLIST.md` lists the expected behaviour for Task 15.
- **Residual risks (CI only):** image tag availability, apt in the aspnet image, `sqlcmd` path, `--wait` semantics with a one-shot service, `<<` merge in Compose, nginx start-up.

## D-081 SQL Server service container, not Testcontainers (Task 15)
- **Decision:** The `backend` job uses a GitHub `services: mssql` container (`mssql/server:2022-latest`, health check with `/opt/mssql-tools18/bin/sqlcmd -C`) and sets `INTEGRATIONTESTS_SQLSERVER`; the fixture creates and drops its own database. Same test code runs on LocalDB locally and Linux SQL Server in CI.
- **Rationale:** Consistent with D-010 (no Testcontainers); the runner already provides Docker and the service starts in parallel with checkout/restore. The SA password is a throwaway value in the workflow env, not a secret. Collation: both default to `SQL_Latin1_General_CP1_CI_AS`; no test relies on a specific collation.
- **Related requirements:** R67.

## D-082 Compose smoke design (Task 15)
- **Decision:** Job `compose-smoke` (needs backend, frontend) runs `ci/compose-smoke.sh` (LF, needs docker, curl, jq). It uses `up --build -d` (not `--wait`, because the seeder is one-shot), polls the seeder to exit 0 and api/ui to be healthy within a 15-minute bound, then checks the UI proxy: SPA and deep link, list (5 items, total 100000), summary (100000), PATCH with a fresh ETag (200) and again (409), seeder re-run is a no-op, `/scalar/v1` is 404. Logs are printed on failure; `down -v` always runs (trap in the script plus an `if: always()` step).
- **Rationale:** Implements `docs/COMPOSE_SMOKE_CHECKLIST.md`; re-runnable by anyone with Docker.

## D-083 Workflow hygiene (Task 15)
- **Decision:** Action majors pinned (`@v4`/`@v3`), `permissions: contents: read`, a concurrency group cancelling superseded runs, NuGet cache via `actions/cache` (no lock files exist, so setup-dotnet's cache is not usable), npm cache via setup-node. Frontend tests run on jsdom, so no headless browser is installed. TRX results are uploaded per test assembly (`LogFilePrefix`). README carries a badge with `<OWNER>/<REPO>` placeholders.

## D-084 Verification limits (Task 15)
- **Decision:** `actionlint` 1.7.7 (Windows binary) passes on the workflow; `bash -n` passes on the script; build, format, 216 unit + 145 integration tests, lint, 62 frontend tests and the build pass locally. The Docker parts and the script's runtime behaviour are verifiable only in the first CI run.
