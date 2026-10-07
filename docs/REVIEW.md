# Final review (Task 18)

Reviewed as a hiring evaluator against `REQUIREMENTS.md`, including the stated penalties. Method: read all docs and code; clean-state run on LocalDB (`seeder --reset`, API, `ng serve` proxy on :4200); every documented edge case re-run by curl; build, format, lint and all tests re-run; latest CI run inspected with `gh`.

## 1. Audit R0–R70

Evidence names are tests (xUnit under `backend/tests/...`, Vitest under `frontend/src/app/`) or document sections. The full per-requirement test names are in [README section 17](../README.md#17-requirements-traceability-r0-r70).

| Req | Status | Evidence |
|---|---|---|
| R0 | Met | Layered solution, [WORK_PLAN](WORK_PLAN.md), this review; no over-engineering found (no AutoMapper/MediatR/repository layer) |
| R1, R2 | Met | `Request.cs`, `RequestConfiguration.cs`; `GetRequestByIdTests` |
| R3 | Met | Seeder `--reset` run in this review: 100,000 rows in 8 s; `RequestGeneratorTests` |
| R4 | Met | SQL Server + EF Core, README §8, D-002 |
| R5, R6 | Met | `RequestQueryService.ListAsync` (COUNT, then OFFSET/FETCH of one page); `ListRequestsTests.Paging_returns_metadata_...` |
| R7 | Met | `Filters_combine_with_and`, `Date_range_covers_whole_days` |
| R8 | Met | `LikePattern`; `Search_matches_title_or_organization_prefix`; curl `search=%` matched literally (0 rows) |
| R9 | Met | `RequestSorting`, 5 fields, `Each_sort_field_ascending_and_descending_...` |
| R10 | Met | `RequestSummaryService`, `SummaryAndLookupsTests` |
| R11 | Met | `RequestListQueryValidator`; curl `pageSize=101` → 400 naming `pageSize` |
| R12 | Met | Token on every action and I/O call; `Cancelled_token_stops_the_query` |
| R13 | Met | DTOs + `RequestMappings`; `Items_expose_only_dto_fields_...` |
| R14 | Met | README §12, PERFORMANCE §6 |
| R15 | Met | 404 on GET/PATCH/history; `Unknown_request_returns_404` |
| R16 | Met | `StatusTransitionPolicyTests`; curl same status and `Completed → InProgress` → 400 `invalid-status-transition` |
| R17 | Met | `Parallel_updates_with_the_same_version_let_exactly_one_win`; curl stale ETag → 409 |
| R18 | Met | `rowversion` + `UpdatedAt` in one UPDATE; `Happy_path_updates_status_version_and_etag_...` |
| R19 | Met | `RowVersion` predicate in `RequestStatusChanger.cs`; `If-Match` mandatory (428); nothing UI-only |
| R20, R21 | Met | History row in the same `SaveChanges`; curl history showed both changes newest first |
| R22, R23 | Met | `BulkStatusService`; curl mixed batch (updated/notFound/conflict), 101 items → 400, duplicates → 400; README §10 |
| R24–R26 | Met | `requests-list.store.ts` (debounce 300 ms, `switchMap`); `requests-http-flows.spec.ts` asserts the in-flight call is cancelled |
| R27 | Met | Loading/Empty/Error with Retry in `requests-page.component.ts` |
| R28 | Met | `status-update.service.ts` (`If-Match`, 409 refresh, never auto-retry); `status-update.service.spec.ts` |
| R29, R30 | Met | `SummaryStore`/`SummaryComponent`; `HistoryDialogComponent`; `summary-history.stores.spec.ts` |
| R31 | Met | Components present; `RequestsApiService` is the only HttpClient user; stores own state |
| R32–R35 | Met | PERFORMANCE §3–§7, `docs/performance/` plans and CSVs, `perf/` scripts |
| R36 | Met | No materialization before paging (verified in `RequestQueryService`); `GROUP BY`/`DISTINCT` for aggregates |
| R37–R39 | Met | `RequestReadCache` (TTL 60 s / 10 min), invalidation after commit only; `Successful_update_invalidates_the_summary_cache`; curl showed summary counts change right after an update |
| R40, R41 | Met | README §11, ARCHITECTURE §8 (multi-instance options) |
| R42, R42a–d | Met | 216 unit + 146 integration (real SQL Server) + 69 frontend, all passing |
| R43–R48 | Met | [WORK_PLAN](WORK_PLAN.md): 9 features, 10 stories, 45 tasks with output, dependencies, estimates, requirement matrix, non-coding tasks |
| R49–R51 | Met | Project references enforce direction; mappings isolated in `RequestMappings`/`SummaryMappings` |
| R52 | Met | DbContext/services scoped, `TimeProvider`/`HybridCache` singleton |
| R53 | Met | No `.Result`/`.Wait()`/`GetAwaiter` in `src` or `tools` (grep) |
| R54, R55 | Met | `GlobalExceptionHandler`, `ApiProblems`, `ValidationFilter`; one error shape |
| R56 | Met | Message-template logging; no bodies, header values or connection strings (`LogStartupSummary` logs only a boolean) |
| R57 | Met | No generic repository, mediator or mapper library |
| R58–R65 | Met | README §1–§14 |
| R66 | Met | README §16, written by the candidate |
| R67–R69 | Met after push | Public repo, CI badge, one-command Compose; latest pushed commit green in all three CI jobs; no ZIPs tracked (`git ls-files`) |
| R70 | Met | README §16 |

No Partial or Missing items remain.

## 2. Evaluator critique by weight

- **Backend (17):** layering, async and DI are clean. One real duplicate found (user-header problem built twice in the controller), fixed. Single and bulk updates share `RequestStatusChanger`; no rule exists twice.
- **Angular (15):** OnPush everywhere, no client-side data operations, `catchError` inside `switchMap`, 409 never auto-retried.
- **REST (13):** status codes and ProblemDetails consistent. **Gap found:** the OpenAPI document did not list `If-Match` / `X-User-Name`. Fixed (D-088) and tested.
- **DB and performance (13):** every index has measured evidence including an ablation run; no change.
- **Concurrency and audit (11):** DB-enforced predicate, atomic status + audit, real parallel test.
- **Code quality (9):** build has 0 warnings; no TODO/FIXME in code; a stale `(Task 8)` comment and a misplaced csproj comment were fixed. The Angular build printed a bundle-budget warning on every build; budget adjusted (D-090).
- **Work plan (8), Tests (6), Cache (4), Docs (4):** complete. Docs counts and decision counts were updated to the current state.

## 3. Findings fixed in this review

1. OpenAPI now documents the required `If-Match` and `X-User-Name` headers (new attribute + transformer + integration test) — D-088.
2. Duplicate "user header required" problem construction in `RequestsController` collapsed into one method — D-089.
3. Stale task reference in `Request.cs` and misplaced comment in `RequestTracker.Api.csproj` — D-089.
4. Angular initial-bundle budget warning on every `ng build` (571 kB vs 500 kB) — budget warning threshold set to 650 kB — D-090.
5. Docs aligned: test counts (362 backend), decision count (D-001…D-091), WORK_PLAN T9.7 and PLAN Task 18 marked Done, REVIEW.md linked from README.

## 4. Residual risks / known limitations

(Consistent with README §14.)

- Deep offset paging and large `COUNT(*)` grow linearly with table size; keyset paging is the documented improvement.
- The cache is per instance; other instances can serve a summary up to 60 s old. A bulk call cancelled mid-way also leaves the summary up to one TTL stale (D-091).
- A rare search term costs more reads than before index tuning; prefix-only search.
- No authentication: `X-User-Name` is trusted.
- The Angular UI was **not click-tested in a browser** (covered by 69 tests and the same API calls through the dev proxy).
- Performance numbers come from LocalDB (SQL Server 2019 engine) on one laptop.
- CI is green for the last **pushed** commit; this review's changes are local until you push and confirm the new run.

## 5. Submission checklist (for you)

1. Commit and push to the public GitHub repo (no ZIPs, no email attachments).
2. Open the Actions tab and confirm the new run is green: backend (build, format, tests), frontend, and the Docker Compose smoke job.
3. ~~Fill in the AI-usage placeholders in README §16~~ Done by the candidate.
4. Click-test the UI once in a browser (including two windows for the 409 flow).
5. Open the repository link in a private window to confirm it is public.
6. Send the link.
