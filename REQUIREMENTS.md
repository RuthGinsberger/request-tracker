# Requirements — Full Stack Home Assignment
Source: the assignment PDF (Hebrew). This is a faithful English rendering. Tags: [C] Core · [NF] Non-functional · [D] Deliverable. The PDF has no bonus items; everything is mandatory or evaluated.

## Context
- R0 [NF] The assignment is for a "Developer Level C" Full Stack role. It evaluates professionalism, code quality, systems thinking, work planning, and technology decisions. The goal is a professional, clear, efficient, maintainable solution at enterprise level. It is not meant to be a large system, and graphic design is not the focus.

## Domain and data
- R1 [C] The system manages requests submitted by organizations and employers. It is used by many users concurrently and holds a large volume of requests. Users can find, filter, view, and update the status of requests, and view summary data.
- R2 [C] Request entity fields: Id; Title; OrganizationName; Status (New / InProgress / Waiting / Completed); Priority (Low / Medium / High); AssignedTo (handler ID or name); CreatedAt; UpdatedAt; Version/RowVersion, used to detect concurrent updates.
- R3 [C] Seed at least 100,000 records. The test data must be re-creatable on demand.
- R4 [C] The database is the candidate's choice: SQL Server, MongoDB, or another option.

## 1. Data retrieval
- R5 [C] Build a REST API in .NET / .NET Core for retrieving requests. All records must never be sent to the client for filtering, sorting, or paging.
- R6 [C] Pagination happens on the server.
- R7 [C] Combined filtering by at least Status, Priority, OrganizationName, AssignedTo, and a date range.
- R8 [C] Text search over at least Title and OrganizationName.
- R9 [C] Sorting by at least 3 fields, ascending and descending.
- R10 [C] At least 2 relevant aggregations.
- R11 [NF] Page size is capped, and all parameters are validated.
- R12 [NF] CancellationToken is used in relevant I/O operations.
- R13 [NF] The API returns DTOs, never DB entities.
- R14 [D] Document two things: how the solution avoids loading all data into memory; how indexes support the main search and filter scenarios.

## 2. Status update and concurrency
- R15 [C] A request that does not exist returns the appropriate HTTP code.
- R16 [C] An invalid status, or a transition that is not allowed, returns a clear validation error.
- R17 [C] Optimistic concurrency: when updates compete, exactly one succeeds and the others receive a proper Conflict response.
- R18 [C] UpdatedAt and RowVersion/Version are updated consistently.
- R19 [NF] Lost updates are prevented. Enforcement must happen on the server or in the DB; UI-only handling is explicitly penalized.

## 3. Change history
- R20 [C] Every status change writes a basic audit record with at least RequestId, PreviousStatus, NewStatus, ChangedAt, and ChangedBy.
- R21 [C] An endpoint returns the change history of a single request.

## 4. Bulk
- R22 [C] A bulk endpoint updates the status of up to 100 requests in one call.
- R23 [C+D] Define and document what happens when some requests don't exist or fail on concurrency. Choose All-or-Nothing or Partial Success, justify the choice, and return a clear response to the client.

## 5. Angular
- R24 [C] Search, filtering, sorting, and paging all run on the server.
- R25 [C] Search input is debounced.
- R26 [C] Previous in-flight requests are cancelled or replaced when the search changes rapidly.
- R27 [C] Loading, Empty, and Error states.
- R28 [C] Status update, including handling of 409 Conflict.
- R29 [C] Aggregations are displayed.
- R30 [C] Change history can be viewed.
- R31 [NF] Responsibilities are separated between Components and Services.

## 6. Performance
- R32 [D] Choose at least 2 central API operations and document how their efficiency was tested on 100k records.
- R33 [D] Provide an execution or query plan, an EXPLAIN, or at least one equivalent metric.
- R34 [D] List the indexes added and the reason for each.
- R35 [D] Identify a possible bottleneck and propose an improvement.
- R36 [NF] Do not load everything before paging or filtering when the DB can do that work.

## 7. Cache (limited scope)
- R37 [C] Cache at least one aggregate, or another read scenario where caching is justified.
- R38 [C] Define an expiration.
- R39 [C] Invalidate appropriately after updates so stale data is not served.
- R40 [D] Justify the choice of cached data.
- R41 [D] Describe what changes when running multiple server instances.

## 8. Tests
- R42 [C] At least 4 meaningful automated tests, at least 1 of them an integration test. They must cover:
  - R42a a happy path for retrieval/filtering or for update;
  - R42b validation or invalid input;
  - R42c a concurrency conflict;
  - R42d an integration test covering the API and the DB, or a suitable substitute.

## 9. Work planning (evaluated as its own deliverable)
- R43 [D] The requirements are broken down logically into Features, User Stories, and Tasks, or an equivalent structure.
- R44 [D] Each task states what is required and the expected output.
- R45 [D] Execution order and dependencies are shown.
- R46 [D] Each task or work unit has an effort estimate.
- R47 [D] Traceability between the assignment's requirements and the tasks is clear.
- R48 [D] The plan includes non-coding tasks: DB design, tests, performance, documentation, and run readiness.

## 10. Code quality
- R49 [NF] Clear project structure and separation of concerns.
- R50 [NF] Readable code, consistent naming, and no duplication.
- R51 [NF] DTOs, Models, and Mapping are clearly separated.
- R52 [NF] Dependency injection with correct lifetimes.
- R53 [NF] Correct async throughout.
- R54 [NF] Global error handling using ProblemDetails or an equivalent.
- R55 [NF] Consistent validation.
- R56 [NF] Logging at meaningful points, without exposing sensitive data.
- R57 [NF] No over-engineering.

## 11. Documentation
- R58 [D] Clear instructions to understand and run the solution.
- R59 [D] Technologies and their versions.
- R60 [D] Description of the solution structure.
- R61 [D] The DB choice and its indexes.
- R62 [D] Explanation of concurrency and the bulk update.
- R63 [D] Explanation of the cache and its invalidation.
- R64 [D] Two significant technology decisions, with the alternatives considered.
- R65 [D] One known limitation and one thing that could be improved.
- R66 [D] AI usage: which parts used AI, and what was checked or changed manually.

## Submission
- R67 [D] Evaluators must be able to fully and conveniently review the running system, the source code, the work plan, the implementation, and the decisions. They must be able to run it.
- R68 [NF] No ZIP files and no source code sent as email attachments.
- R69 [NF] The submission method, accessibility, completeness, and presentation quality are all graded.
- R70 [NF] AI use is allowed and neither costs nor earns points. The candidate is responsible for all submitted code.

## Evaluation weights (reference)
Backend .NET 17 · Angular 15 · REST API 13 · DB and performance 13 · Concurrency and Audit 11 · Code quality 9 · Work planning 8 · Tests 6 · Cache 4 · Docs 4 = 100.

## Penalties stated in the PDF
- A working solution does not automatically get full marks.
- Client-side filtering, sorting, or paging is penalized significantly.
- Concurrency handled only in the UI is incomplete.
- A cache without an invalidation strategy loses points.
- A shallow task breakdown loses points.
- AI does not replace quality.
- Over-engineering is not an advantage.

## Clarification answers (binding decisions by the candidate)
- A1 Versions: current .NET LTS (.NET 10) and the latest stable Angular, standalone components.
- A2 Database: SQL Server with EF Core; native `rowversion` for concurrency; actual execution plans for performance evidence.
- A3 Data access: EF Core, `AsNoTracking` and projection to DTOs on read paths.
- A4 Text search: prefix-friendly `LIKE 'term%'` on Title and OrganizationName (index-friendly). Full-text search is documented as the improvement.
- A5 Run environment: the development machine is Windows with SQL Server Express LocalDB. Docker cannot run on it (hardware virtualization is disabled). Local development and tests run against LocalDB. A Docker Compose setup (SQL Server container + seeder + API + UI) is still provided so evaluators can run everything with one command on any OS, and it is verified automatically in GitHub Actions CI.
- A6 Integration tests: xUnit + WebApplicationFactory against a real SQL Server chosen by connection string: LocalDB locally (a unique test database per run, dropped at the end), and a SQL Server service container in GitHub Actions. No Testcontainers, because Docker is unavailable locally.
- A7 Allowed status transitions: New→InProgress; InProgress→Waiting; Waiting→InProgress; InProgress→Completed; Waiting→Completed. Completed is terminal. Setting the same status again is rejected. Everything else is rejected.
- A8 No authentication. The acting user comes from a required `X-User-Name` header, validated by the server. The UI has a user picker.
- A9 AssignedTo and OrganizationName are plain strings with exact-match filters. Partial matching is available only through text search (R8).
- A10 No create, edit, or delete endpoints, and no edits to Priority or AssignedTo. Only the operations the PDF lists.
- A11 Aggregations: counts by Status and by Priority, computed globally (not filter-aware) and cached.
- A12 The date range filter applies to CreatedAt only.
- A13 Seeding: a dedicated seeder CLI that wipes and regenerates on demand, uses bulk insert, produces realistic distributions, and creates no seeded history.
- A14 Version token: sent with `If-Match`, returned in `ETag` (and as a `version` field in DTOs).
- A15 Bulk: Partial Success. Each item carries its own expected version. All items move to the same target status. Response is 200 with a per-item result (Updated / NotFound / Conflict / InvalidTransition) plus a summary.
- A16 Paging: offset paging with a total count, max page size 100. Keyset paging is documented as the bottleneck improvement.
- A17 Cache: in-process HybridCache (or IMemoryCache) with a short absolute expiration and explicit invalidation on single and bulk updates. Redis or another distributed cache with pub/sub invalidation is described for scale-out, not implemented.
- A18 Human-facing work plan: `docs/WORK_PLAN.md` (Features → User Stories → Tasks, with output, dependencies, hour estimates, requirement IDs, and non-coding tasks).
- A19 Effort estimates reflect realistic hand-coding effort.
- A20 Hosting: a public GitHub repo, no live deployment. A GitHub Actions CI pipeline is required. It builds and runs all tests against SQL Server on Linux, and smoke-tests the Docker Compose setup.
- A21 Documentation language: English.
- A22 AI-usage section (R66): drafted from DECISIONS.md, with clearly marked placeholders that the candidate completes personally.
- A23 UI: Angular Material, minimal custom styling.
- A24 Frontend tests: focused tests on the RxJS search pipeline (and the 409 flow), included.
- A25 No deadline. All optional items are included.
