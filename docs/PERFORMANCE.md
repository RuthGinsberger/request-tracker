# Performance analysis (100,000 requests)

Covers R14, R32–R36. Every number below was produced by the scripts in [`perf/`](../perf) and is stored in
[`docs/performance/`](performance) (CSV results and the actual `.sqlplan` files). The analysis led to two index migrations
(`CoveringCreatedAtIndex`, `SortColumnIndexes`); the "before / after" columns are real runs of the same scripts on the same data.

## 1. Summary

| | Before (indexes as designed) | After (both migrations) |
|---|---|---|
| Default list, page 1, page query (logical reads) | 182 | **6** |
| Deep page 4000, plan already cached for page 1 (logical reads) | **245,183** | **1,582** |
| Deep page 4000 through the API, p50 / p95 | 414 / 442 ms | **93 / 111 ms** |
| Prefix search "License" (7,180 matches), page query reads | 1,770 | **14** |
| Sort by `updatedAt` / `priority` / `status`, page query reads (SQL ms) | 2,010 (207–266 ms) | **178–191 (1–4 ms)** |
| Rare prefix search (1 match) through the API, p50 | 479 ms (plan reused from a common term) | **66 ms** (but SQL reads 11 → 1,975, section 7.4) |
| `GET /summary` cache miss vs hit, API p50 | ~107 ms vs 1.1 ms | unchanged (138 reads per query) |

The three numbers to remember: **no secondary index = 2,010 reads for every statement (a full scan of the 15.7 MB table); the designed indexes = 5–183 reads for
most statements but 245,183 for a deep page; the final set = ≤ 23 reads for nearly every page-1 list query and a bounded worst case of ≈ 2,000 reads.**

## 2. Method and environment

**Environment (honest version).** A Windows 11 laptop (Intel Core i5-1135G7, 8 logical CPUs, 15.7 GB RAM) running **SQL Server LocalDB**
(`Microsoft SQL Server 2019 Express, 15.0.4382.1`, compatibility level 150), with the API and the scripts on the same machine. LocalDB runs the same SQL Server
engine family as the SQL Server 2022 container used in CI and Compose, so plan shapes and **logical reads carry over**; absolute times do not (shared laptop,
no network hop, the whole ~55 MB database stays in memory, 1 ms resolution in `SET STATISTICS TIME`). **Logical reads are therefore the primary metric**; elapsed
time is secondary and shown with its noise. SQL Server 2022 can choose different plans in details (it has parameter-sensitive plan optimization, 2019 does not),
so the numbers should be re-checked on the CI container.

**Data.** 100,000 rows from the seeder (`--reset --anchor 2026-10-06`, seed 42, identical on every run): 300 organizations (largest 5,812 rows, smallest 145),
about 40 assignees plus 4,538 unassigned, Status New 15,167 / InProgress 25,038 / Waiting 14,943 / Completed 44,852, Priority Low 40,110 / Medium 39,949 / High 19,941,
`CreatedAt` over the last 24 months. The clustered table is 2,010 data pages (15.7 MB).

**What is measured.**

1. *SQL level* (`perf/Run-SqlPerf.ps1`, scenarios in `perf/Scenarios.ps1`): the exact statements EF Core generates (copied from the API log with
   `Microsoft.EntityFrameworkCore.Database.Command` at Information, section 6), replayed through `sp_executesql` with the same parameter types. Per statement:
   the **actual execution plan** (`SET STATISTICS XML ON`, from an empty plan cache, saved as `.sqlplan`), then 3 discarded warm-up runs and **7 measured runs**,
   reporting the **median** logical reads and elapsed time (`SET STATISTICS IO, TIME ON`), then one **cold** run after `CHECKPOINT; DBCC DROPCLEANBUFFERS` (columns
   `ColdElapsedMs`, `ColdPhysicalReads` in the CSV). Tables below are **warm medians** unless stated. Indexes are rebuilt and statistics updated with `FULLSCAN` before each run
   (a freshly bulk-loaded index is fragmented and reads more pages: a full scan is 2,189 pages instead of 2,010).
2. *API level* (`perf/Measure-Api.ps1`): the Release build started in Production mode (no EF logging), the plan cache cleared first, 50 sequential requests per endpoint after
   5 warm-up requests; p50/p95 of wall-clock time as seen by an `HttpClient` (routing, validation, the two SQL statements, JSON). For the summary: one instance with a 1-hour TTL
   (warm = cache hit) and one with a 1-second TTL and a 1.2 s pause between requests (every request is a cache miss).
3. *Three index configurations*: **no-indexes** (all non-clustered indexes disabled; clustered PK only), **initial-indexes** (the six non-clustered indexes of ARCHITECTURE.md section 4,
   created by `InitialCreate`), **final-indexes** (after `CoveringCreatedAtIndex` and `SortColumnIndexes`). An intermediate run with only the first migration is kept as
   [results-covering-index.csv](performance/results-covering-index.csv) (it shows the effect of the covering index alone).

**Scenarios** (page size 20, default sort `createdAt desc, id desc` unless stated). A list request runs two statements, `COUNT(*)` and the page query.

| # | Scenario | Matches |
|---|---|---|
| 01 | default page 1 (no filter) | 100,000 |
| 02 | `status=InProgress`, sort createdAt | 25,038 |
| 03 | exact `organizationName` = Evergreen Health (rarest) | 145 |
| 04 | exact `organizationName` = Acme Ltd (largest) | 5,812 |
| 05 | prefix search `License` (Title or OrganizationName) | 7,180 |
| 06 | `createdFrom/To` = one week | 936 |
| 07 | default sort, **page 4000** (OFFSET 79,980) | 100,000 |
| 08 | `status=New`, sort **title** desc | 15,167 |
| 09 | `GET /summary`: two `GROUP BY` statements | – |
| 10 | `status=InProgress` + 3-month date range | 3,157 |
| 11 | exact `assignedTo` = tamar.katz | 2,504 |
| 12 | prefix search `Permit application #8228` (a reference number) | 1 |
| 13 | `priority=High` | 19,941 |
| 14–16 | no filter, sort by `updatedAt` / `priority` / `status` desc | 100,000 |
| 17 | `status=InProgress` + `priority=High` | 4,944 |

Scenario 07 first runs the page-1 values in the same session from an empty plan cache, which reproduces the plan cache of a running API after the first request (section 7.1);
**07b** is the same statement with `OPTION (RECOMPILE)` (plan built for the real offset).

**Reproduce** (about 10 minutes for a full SQL run, 5 for the API run):

```powershell
dotnet run --project backend/tools/RequestTracker.Seeder -- --reset --anchor 2026-10-06     # exactly 100,000 rows, same data every time
./perf/Run-SqlPerf.ps1 -Label no-indexes -Config none          # clustered PK only (the indexes are rebuilt afterwards)
./perf/Run-SqlPerf.ps1 -Label with-indexes                       # indexes as currently in the database
./perf/Get-PlanSummary.ps1 -Label with-indexes                   # which index/operator each saved plan used
./perf/Measure-Api.ps1 -Label with-indexes                       # p50/p95 through the real API (starts and stops its own API instances)
./perf/Format-Results.ps1 -Labels no-indexes,with-indexes        # markdown tables like the ones below (add -Api for the API file)
sqlcmd -S "(localdb)\MSSQLLocalDB" -d RequestTracker -W -i perf/index-sizes.sql
sqlcmd -S "(localdb)\MSSQLLocalDB" -d RequestTracker -i perf/write-cost.sql    # logical reads of a status update
```

The "before" configuration is reproduced with `dotnet dotnet-ef database update InitialCreate --project backend/src/RequestTracker.Infrastructure --startup-project backend/src/RequestTracker.Api`
(and `database update` to return). Options: `-Only 07` runs one scenario family, `-Disable IX_Requests_Status_CreatedAt` tests removing one index, `-Runs` / `-Warmup` change N, `-Merge` adds scenarios to an existing result file.
`INTEGRATIONTESTS_SQLSERVER` is not used here; pass `-Server` to point the SQL script at another instance (for example the CI SQL Server container).

## 3. Results at SQL level (R32, R33)

Logical reads (8 KB pages) and warm median elapsed milliseconds per statement. `count` = the `COUNT(*)` of the list, `page` = the `OFFSET/FETCH` query.
Sources: [no-indexes](performance/results-no-indexes.csv), [initial-indexes](performance/results-initial-indexes.csv), [final-indexes](performance/results-final-indexes.csv).

| Scenario | Statement | reads (no-indexes) | reads (initial-indexes) | reads (final-indexes) | ms (no-indexes) | ms (initial-indexes) | ms (final-indexes) |
|---|---|---:|---:|---:|---:|---:|---:|
| 01-default | count | 2,010 | 151 | 138 | 34 | 23 | 23 |
| 01-default | page | 2,010 | 182 | 6 | 313 | 3 | 2 |
| 02-status-created | count | 2,010 | 40 | 37 | 28 | 6 | 5 |
| 02-status-created | page | 2,010 | 177 | 9 | 41 | 5 | 4 |
| 03-org-rare | count | 2,010 | 5 | 5 | 26 | 1 | 0 |
| 03-org-rare | page | 2,010 | 83 | 186 | 30 | 0 | 5 |
| 04-org-large | count | 2,010 | 31 | 31 | 17 | 2 | 2 |
| 04-org-large | page | 2,010 | 171 | 14 | 18 | 4 | 3 |
| 05-prefix-search | count | 2,010 | 72 | 72 | 19 | 18 | 15 |
| 05-prefix-search | page | 2,010 | 1,770 | 14 | 27 | 21 | 3 |
| 06-date-range | count | 2,010 | 5 | 22 | 25 | 1 | 1 |
| 06-date-range | page | 2,010 | 181 | 4 | 10 | 7 | 1 |
| 07b-deep-page-recompile | count | 2,010 | 151 | 138 | 23 | 32 | 20 |
| 07b-deep-page-recompile | page | 2,010 | 2,010 | 1,582 | 124 | 352 | 53 |
| 07-deep-page | count | 2,010 | 151 | 138 | 15 | 25 | 19 |
| 07-deep-page | page | 2,010 | 245,183 | 1,582 | 136 | 375 | 45 |
| 08-status-title | count | 2,010 | 25 | 23 | 21 | 4 | 4 |
| 08-status-title | page | 2,010 | 935 | 935 | 33 | 32 | 17 |
| 09-summary | status | 2,010 | 151 | 138 | 32 | 18 | 29 |
| 09-summary | priority | 2,010 | 151 | 138 | 28 | 21 | 32 |
| 10-status-daterange | count | 2,010 | 10 | 10 | 16 | 0 | 0 |
| 10-status-daterange | page | 2,010 | 183 | 7 | 28 | 3 | 2 |
| 11-assignee | count | 2,010 | 17 | 17 | 25 | 1 | 2 |
| 11-assignee | page | 2,010 | 183 | 23 | 26 | 8 | 4 |
| 12-search-rare | count | 2,010 | 7 | 7 | 24 | 2 | 2 |
| 12-search-rare | page | 2,010 | 11 | 1,975 | 20 | 0 | 51 |
| 13-priority-high | count | 2,010 | 151 | 30 | 25 | 20 | 5 |
| 13-priority-high | page | 2,010 | 549 | 9 | 75 | 6 | 3 |
| 14-sort-updatedat | count | 2,010 | 151 | 138 | 25 | 22 | 24 |
| 14-sort-updatedat | page | 2,010 | 2,010 | 178 | 291 | 266 | 4 |
| 15-sort-priority | count | 2,010 | 151 | 138 | 40 | 21 | 22 |
| 15-sort-priority | page | 2,010 | 2,010 | 184 | 266 | 257 | 1 |
| 16-sort-status | count | 2,010 | 151 | 138 | 38 | 27 | 28 |
| 16-sort-status | page | 2,010 | 2,010 | 191 | 269 | 207 | 1 |
| 17-status-priority | count | 2,010 | 10 | 10 | 29 | 2 | 2 |
| 17-status-priority | page | 2,010 | 464 | 14 | 37 | 13 | 4 |

How to read it: with no secondary index every statement reads the whole clustered index (2,010 pages), whatever it asks. The designed indexes cut counts to 5–151 reads and most page
queries to 83–183, **except** the deep page (245,183), the common prefix search (1,770) and the sorts on unindexed columns (2,010 plus a sort). The final configuration brings every page-1
statement to ≤ 191 reads (almost all ≤ 23), at the price of one regression family (12, section 7.4). Elapsed milliseconds are 1 ms-resolution medians and noisy at this scale (everything is
cached): they point the same way as the reads, but the reads are what to trust. Cold-cache runs are in the CSVs; for example the default page costs 313 ms warm / 338 ms cold with no indexes,
3 ms / 3 ms with the initial indexes and 2 ms / 2 ms with the final ones (physical + read-ahead reads 2,009, 50 and 84). One observation on noise: `03-org-rare` page reads 83 in one run and 186 in another with an identical
plan (nested-loop read-ahead), so differences of that kind between runs are not changes in the plan.

### 3.1 Execution plans (actual plans, saved)

Plan files: [no-indexes](performance/plans/no-indexes), [initial-indexes](performance/plans/initial-indexes), [final-indexes](performance/plans/final-indexes) (open in SSMS or Azure Data Studio;
names are `<scenario>-<count|page>.sqlplan`). `perf/Get-PlanSummary.ps1` extracts the access methods into [plan-summary-final-indexes.csv](performance/plan-summary-final-indexes.csv) (and the other two labels).

Default list, page 1 (scenario 01):

```
initial  Top(20) ← Nested Loops ← Index Scan IX_Requests_CreatedAt (ordered, backward)
                              └─ Clustered Index Seek PK_Requests   (key lookup per row, 20)        182 reads
final    Top(20) ← Index Scan IX_Requests_CreatedAt (ordered, backward, covering: no lookup)           6 reads
```
Plans: [initial](performance/plans/initial-indexes/01-default-page.sqlplan) · [final](performance/plans/final-indexes/01-default-page.sqlplan).
Neither has a sort operator: the index delivers `CreatedAt DESC, Id DESC` (the clustered key `Id` is the last key of every non-clustered index), so `OFFSET/FETCH` stops after 20 rows.

Deep page 4000 (scenario 07), page query:

```
initial (plan compiled for OFFSET 0, reused)  Top(80,020) ← Nested Loops ← Index Scan IX_Requests_CreatedAt (80,000 rows)
                                                                        └─ Clustered Index Seek PK_Requests (80,000 lookups)   245,183 reads
initial, OPTION (RECOMPILE)                   Top ← Sort(Top N) ← Clustered Index Scan PK_Requests (100,000 rows)               2,010 reads + sort CPU
final                                         Top(80,020) ← Index Scan IX_Requests_CreatedAt (80,000 rows, covering)            1,582 reads, no sort
```
Plans: [initial](performance/plans/initial-indexes/07-deep-page-page.sqlplan) · [initial, recompiled](performance/plans/initial-indexes/07b-deep-page-recompile-page.sqlplan) · [final](performance/plans/final-indexes/07-deep-page-page.sqlplan).

Sort by `updatedAt` (scenario 14), page query:

```
initial  Top(20) ← Sort(Top N) ← Clustered Index Scan PK_Requests (100,000 rows)                     2,010 reads, 207–266 ms
final    Top(20) ← Nested Loops ← Index Scan IX_Requests_UpdatedAt (ordered) + 20 key lookups         178 reads, 4 ms
```
Plans: [initial](performance/plans/initial-indexes/14-sort-updatedat-page.sqlplan) · [final](performance/plans/final-indexes/14-sort-updatedat-page.sqlplan).

Other representative plans (final configuration):

| Scenario | Count plan | Page plan |
|---|---|---|
| 02 status | Index Seek `IX_Requests_Status` (25,038 rows), 37 reads | ordered scan of `IX_Requests_CreatedAt`, filter on the included `Status`, 9 reads |
| 03 org (145 rows) | Index Seek `IX_Requests_OrganizationName_CreatedAt`, 5 reads | Seek on the org index (already ordered by CreatedAt) + 20 key lookups, 83–186 reads |
| 05 prefix search | Index Seek `IX_Requests_Title` ∪ Seek `IX_Requests_OrganizationName_CreatedAt`, 72 reads | ordered scan of `IX_Requests_CreatedAt` with the `LIKE` as a residual predicate, 14 reads |
| 06 date range | Index Seek `IX_Requests_CreatedAt` range, 22 reads | Index Seek (range, backward), 4 reads |
| 10 status + 3-month range | Index Seek `IX_Requests_Status_CreatedAt` (3,157 rows), 10 reads | Index Seek `IX_Requests_CreatedAt` range, 7 reads |
| 11 assignee | Index Seek `IX_Requests_AssignedTo_CreatedAt`, 17 reads | ordered scan of `IX_Requests_CreatedAt`, 23 reads |
| 13 priority | Index Seek `IX_Requests_Priority` (19,941 rows), 30 reads | ordered scan of `IX_Requests_CreatedAt`, 9 reads |
| 17 status + priority | Index Seek `IX_Requests_Status_Priority` (4,944 rows), 10 reads | ordered scan of `IX_Requests_CreatedAt`, 14 reads |
| 09 summary | `IX_Requests_Status` / `IX_Requests_Priority` scan + aggregate, 138 reads each | – |

Low-selectivity filters are not forced into seeks: `status=InProgress` (25%) and `priority=High` (20%) are served by an **ordered scan that stops after 20 matches** (9 reads), and their counts read a
narrow index. Where the optimizer scans, it is right to: a seek on a value that matches 25,000 rows followed by sorting them would cost more than walking the index in the requested order until the page is full.

## 4. Index decisions (R34)

### 4.1 Final list

| Index | Key (INCLUDE) | Why it exists (evidence) | Size |
|---|---|---|---:|
| `PK_Requests` (clustered) | `Id` | `GET /requests/{id}`, status update, key lookups from the other indexes | 2,010 pages (15.7 MB) |
| `IX_Requests_CreatedAt` | `CreatedAt` (INCLUDE `Title, OrganizationName, Status, Priority, AssignedTo, UpdatedAt, RowVersion`) | Default sort and every list ordered by createdAt. **Covers the list projection**, so the ordered scan needs no key lookups: page 1 = 6 reads (was 182), the 80,000-row deep scan 1,582 (was 245,183 with a reused plan), a common prefix search 14 (was 1,770), status / priority / assignee / large-organization page queries ≤ 23. Also serves the date-range seek | 1,975 pages (15.4 MB), was 225 |
| `IX_Requests_Status_Priority` | `Status, Priority` | Count of a combined status + priority filter: 10 reads, 1,975 without it (ablation 17). It is no longer used for the summary or unfiltered counts (the single-column indexes below are narrower) | 151 pages (1.2 MB) |
| `IX_Requests_Status_CreatedAt` | `Status, CreatedAt` | Count of status + date range: 10 reads vs 250 without. The page queries no longer need it (the covering index serves them) | 237 pages (1.9 MB) |
| `IX_Requests_OrganizationName_CreatedAt` | `OrganizationName, CreatedAt` | Exact organization filter: count 5–31 reads; page = seek in already-sorted order + 20 lookups for a rare organization. Also the second half of the prefix-search union and `SELECT DISTINCT OrganizationName` for lookups | 634 pages (5.0 MB) |
| `IX_Requests_AssignedTo_CreatedAt` | `AssignedTo, CreatedAt` | Exact assignee filter: count 17 reads vs 1,975 without | 517 pages (4.0 MB) |
| `IX_Requests_Title` | `Title` | Prefix-search count (seek, 72 reads vs 1,975), the rare-term count (7 vs 1,975) and the sort by title (935 vs 1,975) | 990 pages (7.7 MB) |
| `IX_Requests_UpdatedAt` | `UpdatedAt` | Sort by `updatedAt`: 178 reads / 4 ms instead of 2,010 reads / 266 ms | 225 pages (1.8 MB) |
| `IX_Requests_Priority` | `Priority` | Sort by `priority` (184 reads / 1 ms vs 2,010 / 257 ms) and the count of a priority filter (30 reads vs 151) | 138 pages (1.1 MB) |
| `IX_Requests_Status` | `Status` | Sort by `status` (191 reads / 1 ms vs 2,010 / 207 ms), status counts and the summary `GROUP BY Status` (138 reads, the narrowest index) | 138 pages (1.1 MB) |
| `IX_RequestStatusHistory_RequestId_ChangedAt` | `RequestId, ChangedAt` | History of one request, newest first, no sort (unchanged; the seeder writes no history, so it is not part of the 100k measurements) | – |

### 4.2 What changed and why

Two migrations, both applied cleanly from scratch (`Seeder --reset`) and on top of an existing database (`dotnet ef database update`):

1. [`CoveringCreatedAtIndex`](../backend/src/RequestTracker.Infrastructure/Persistence/Migrations/20261006195102_CoveringCreatedAtIndex.cs): `IX_Requests_CreatedAt` is dropped and recreated with the seven list-projection
   columns as `INCLUDE`. In the initial configuration the optimizer used it for the default sort but did a **key lookup for every row it read**: 182 reads for 20 rows, and 80,000 lookups
   (245,183 reads) for page 4000 when the plan compiled for page 1 was reused (7.1). Any plan of the form "walk `CreatedAt` in order and look every row up" costs as much as the number
   of rows it must skip. Covering removes the lookup, which turns that unbounded cost into a bounded one (at worst one scan of the 1,975-page index).
2. [`SortColumnIndexes`](../backend/src/RequestTracker.Infrastructure/Persistence/Migrations/20261006201107_SortColumnIndexes.cs): single-column indexes on `UpdatedAt`, `Priority` and `Status`. Evidence: with no index on the sort column the page query is
   a clustered scan plus a Top-N sort of all 100,000 rows (2,010 reads, 207–266 ms, 100–190 ms CPU) every time the user clicks one of those column headers; with the index it is an ordered scan that stops after
   20 rows (178–191 reads, 1–4 ms), because the clustered key `Id` appended to each index is the tie-breaker of every list sort (D-037). They also gave the optimizer a narrower index for counts.

No planned index was removed, because the optimizer picks each of them for a statement where its absence is expensive (4.3). `IX_Requests_Status_Priority` lost two of its uses to the
new single-column indexes and is the weakest member now.

### 4.3 Evidence for keeping the others (ablation, final configuration)

`./perf/Run-SqlPerf.ps1 -Label ablation-… -Disable <index> -Only <scenario> -Runs 5` disables one index for a run (it is rebuilt afterwards):

| Disabled | Statement | Reads with | Reads without | Verdict |
|---|---|---:|---:|---|
| `IX_Requests_Status_CreatedAt` | 10 count (status + date range) | 10 | 250 | keep: 25× fewer, grows with the table |
| `IX_Requests_Status_CreatedAt` | 02 page (status, sort createdAt) | 9 | 9 | the page query does not need it (the covering index serves it) |
| `IX_Requests_Status_Priority` | 17 count (status + priority) | 10 | 1,975 | keep: 200× |
| `IX_Requests_Status_Priority` | 01 count, 09 summary | 138 | 138 | not used any more (single-column indexes are narrower) |
| `IX_Requests_AssignedTo_CreatedAt` | 11 count | 17 | 1,975 | keep: 116× |
| `IX_Requests_Title` | 05 count (prefix search) | 72 | 1,975 | keep: 27× |
| `IX_Requests_Title` | 08 page (sort by title) | 935 | 1,975 | keep |
| `IX_Requests_Title` | 12 count / page (rare search) | 7 / 1,975 | 1,975 / 1,979 | count needs it; the page query scans either way |
| `IX_Requests_UpdatedAt` | 14 page (sort updatedAt) | 178 | 1,975 | keep: 11× (293 vs 4 ms) |
| `IX_Requests_Priority` | 15 page (sort priority) | 184 | 1,975 | keep: 11× (362 vs 1 ms) |
| `IX_Requests_Status` | 16 page (sort status) | 191 | 1,975 | keep: 10× (350 vs 1 ms) |

Result files: `docs/performance/results-ablation-*.csv` ([example](performance/results-ablation-no-status-priority-17.csv)). The ablation runs use 5 measured runs instead of 7.
`IX_Requests_Status_Priority` (1.2 MB) and `IX_Requests_Status_CreatedAt` (1.9 MB) each earn their place on one query shape (a combined status + priority count, a status + date-range count); both gaps grow
linearly with the table and the indexes are small, so they stay; they are the first candidates to drop if write cost ever matters more.

### 4.4 Cost of the changes (storage and writes)

| | Before (initial) | After (final) |
|---|---:|---:|
| Table + all indexes (`perf/index-sizes.sql`, after a rebuild) | 4,764 pages (37.2 MB) | 7,015 pages (54.8 MB), +47% |
| Status update (the only hot write), logical reads, warm (`perf/write-cost.sql`) | 11 | 22 |
| Bulk seed of 100,000 rows with all indexes (seeder, LocalDB) | 6.8–7.5 s | 10–17 s |

The covering index stores `Status`, `UpdatedAt` and `RowVersion`, and `IX_Requests_UpdatedAt` / `IX_Requests_Status` have those as keys, so every status change rewrites three more index entries
(+11 reads, still sub-millisecond); `Title`, `OrganizationName`, `Priority` and `AssignedTo` never change after insert, so `IX_Requests_Priority` costs nothing on update. The seeder is slower because
the indexes are maintained row by row during `SqlBulkCopy`; creating them after the load would restore the old time (not done, since `--reset` runs migrations first and 10–17 s is acceptable).
The workload is read-heavy (list and summary on every page load) and writes are rare single-row operations, so the trade is accepted.

## 5. Results at API level (R32)

p50 / p95 of 50 sequential requests per endpoint, in milliseconds (`GET /api/requests`, page size 20 unless noted), plan cache cleared before each run. Sources:
[api-initial-indexes.csv](performance/api-initial-indexes.csv), [api-final-indexes.csv](performance/api-final-indexes.csv).

| Scenario | p50 ms (initial-indexes) | p95 ms (initial-indexes) | p50 ms (final-indexes) | p95 ms (final-indexes) |
|---|---:|---:|---:|---:|
| 01 default page 1 | 28.9 | 45.1 | 33.5 | 42.7 |
| 02 status + createdAt sort | 11.3 | 15.5 | 14.3 | 21.8 |
| 03 organization exact | 4.9 | 7.8 | 6.4 | 18.3 |
| 04 prefix search (common) | 20.4 | 28.7 | 25.1 | 31.6 |
| 05 date range (1 week) | 8.6 | 10.3 | 10.4 | 13.9 |
| 06 deep page 4000 | 414 | 442.1 | 93.1 | 110.7 |
| 07 status + date range | 6.6 | 8.7 | 9.8 | 16.4 |
| 08 prefix search (rare) | 478.6 | 545.5 | 66.5 | 86.6 |
| 09 summary: first request after start (miss + JIT) | 358.1 | 358.1 | 377.2 | 377.2 |
| 09 summary: warm (cache hit) | 1.1 | 1.4 | 1.2 | 1.5 |
| 09 summary: cold (cache miss, JIT excluded) | 107.2 | 188.6 | 99.7 | 123.3 |

Reading it: scenarios 01–05 and 07 are dominated by fixed API cost (routing, validation, two SQL round trips, JSON), roughly 5–35 ms, and differ by a few milliseconds between the two runs in either
direction (the final run is 2–5 ms slower on five of them although SQL reads fell; I did not isolate the cause, it is within what repeated runs of this laptop show, and it is not the database part). The index changes show
where the database part was large: the deep page (414 → 93 ms p50) and the rare search (479 → 66 ms). The latter is the plan-reuse effect (7.1): `Permit application #8228` reuses the plan compiled for
`License` (an ordered scan of `CreatedAt` with a lookup per row, up to the whole table) until the covering index made every plan cheap. The sorts of scenarios 14–16 are not in this table
(the UI offers them, the script's endpoint list does not): their SQL cost is in section 3.

**Summary, cold vs warm** (R32, R37). Uncached = the two `GROUP BY` statements (138 reads each, 12–37 ms). Through the API:

| Case | p50 | p95 |
|---|---:|---:|
| First request after process start (miss + JIT + first connection), 1 sample, two runs | 358 / 377 ms | – |
| Cache miss (TTL expired), JIT excluded, 30 requests, runs before / after | 107 / 100 ms | 189 / 123 ms |
| Cache hit (HybridCache L1), 50 requests, runs before / after | 1.1 / 1.2 ms | 1.4 / 1.5 ms |

A hit is roughly 90× cheaper than a miss and does no I/O. The summary query is the same in both configurations (it only reads a narrow index), so the before/after difference is noise. At 100k rows the
query itself is cheap; the cache exists because the cost is one full-index scan per page load that grows linearly with the table (D-047), and it is invalidated after every committed status change.

## 6. No full loads: how the solution avoids loading all data into memory (R14, R36)

**Where the work happens.** `RequestQueryService.ListAsync` ([source](../backend/src/RequestTracker.Application/Requests/RequestQueryService.cs)) builds one `IQueryable<Request>` from
`db.Requests.AsNoTracking()`, adds every filter as a `Where`, and only then terminates it, twice, with database operators:

1. `filtered.CountAsync(ct)` runs `SELECT COUNT(*) … WHERE …` and returns one integer.
2. `ApplySort(...).Skip(skip).Take(pageSize).Select(RequestMappings.ToListItem).ToListAsync(ct)` runs `ORDER BY … OFFSET … FETCH NEXT …`; it is skipped altogether when the page starts past the last match.
   `ToListAsync` is the first and only materialization and it holds at most 100 rows (the page-size cap) as DTOs; entities are never tracked.
3. The summary and lookups use `GROUP BY` / `SELECT DISTINCT` in SQL (`RequestSummaryService`) and return at most 4 + 3 groups and the organization / assignee names.

A search of `Application` and `Infrastructure` for `ToList` / `ToArray` / `AsEnumerable` finds only those `…Async` terminators, the bulk id list (≤ 100 ids) and in-memory mapping of already-aggregated rows;
nothing is filtered, sorted or counted after a materialization. The generated SQL (Development log, 100k rows):

```sql
-- count: filters only, one scalar
SELECT COUNT(*) FROM [Requests] AS [r] WHERE [r].[Status] = @statuses1

-- page: filter + sort + paging + projection in one statement; only the list columns, 20 rows
SELECT [r].[Id], [r].[Title], [r].[OrganizationName], [r].[Status], [r].[Priority], [r].[AssignedTo], [r].[CreatedAt], [r].[UpdatedAt], [r].[RowVersion]
FROM [Requests] AS [r]
WHERE [r].[Status] = @statuses1
ORDER BY [r].[CreatedAt] DESC, [r].[Id] DESC
OFFSET @p ROWS FETCH NEXT @p2 ROWS ONLY

-- prefix search: LIKE 'term%' with our own escaping, sargable
WHERE [r].[Title] LIKE @pattern ESCAPE N'\' OR [r].[OrganizationName] LIKE @pattern ESCAPE N'\'

-- date range: half-open, sargable
WHERE [r].[CreatedAt] >= @start AND [r].[CreatedAt] < @endExclusive

-- summary
SELECT [r].[Status] AS [Key], COUNT(*) AS [Count] FROM [Requests] AS [r] GROUP BY [r].[Status]
```

Everything (filter, count, sort, page) is in the statement, with parameters. EF Core throws on a predicate it cannot translate instead of evaluating it on the client, so a `Where` that cannot run in SQL would fail loudly
rather than load the table. The seeder streams 10,000-row batches (`SqlBulkCopy`) for the same reason.

**How indexes serve each main scenario** (final configuration; reads from section 3):

| Scenario | Index used | How |
|---|---|---|
| Default list / sort by createdAt | `IX_Requests_CreatedAt` (covering) | ordered scan from the newest end, stops after 20 rows, no sort, no lookup (6 reads); the count scans the narrow `IX_Requests_Status` (138) |
| Filter by status | count: `IX_Requests_Status`; page: ordered scan of `IX_Requests_CreatedAt` | 37 / 9 reads. The filter keeps 25% of rows, so a seek followed by a sort would cost more |
| Filter by organization (exact) | `IX_Requests_OrganizationName_CreatedAt` | seek on the name; entries of one name are already in `CreatedAt` order, so no sort (count 5 reads; page 83–186 for the 145-row organization, 14 for the large one) |
| Filter by assignee (exact) | count: `IX_Requests_AssignedTo_CreatedAt`; page: ordered scan | 17 / 23 reads |
| Date range | `IX_Requests_CreatedAt` range seek (backward) | 22 / 4 reads; with a status: `IX_Requests_Status_CreatedAt` for the count (10) |
| Priority, or status + priority | count: `IX_Requests_Priority` / `IX_Requests_Status_Priority`; page: ordered scan | 30 / 9 and 10 / 14 reads |
| Prefix search | count: `IX_Requests_Title` ∪ `IX_Requests_OrganizationName_CreatedAt` seeks; page: ordered scan with the `LIKE` as a residual predicate | 72 / 14 reads for 7,180 matches. A leading wildcard is not supported by design (A4) |
| Sort by updatedAt / priority / status / title | `IX_Requests_UpdatedAt` / `_Priority` / `_Status` / `_Title` | ordered scan, 20 lookups (178–191 reads; title with a filter 935) |
| Summary | `IX_Requests_Status`, `IX_Requests_Priority` (narrow) | 138 reads each, cached for 60 s |

## 7. Bottlenecks and improvements (R35)

### 7.1 Deep OFFSET pages, and a plan compiled for page 1 reused for page 4000 (measured)

`OFFSET n` must produce and discard `n` rows. With a **plan compiled for the first request** (what the API's plan cache holds after `?page=1`), SQL Server walks `IX_Requests_CreatedAt` and does a key lookup
for each of the 80,000 skipped rows: **245,183 reads and 414 ms p50 through the API**. The same statement compiled for its real offset reads 2,010 pages (clustered scan + Top-N sort); with the covering index it reads
1,582 and needs no sort (**93 ms p50**). This is classic parameter sniffing: `OFFSET @p` is a parameter, so one cached plan serves page 1 and page 4000. It first showed up by accident: my first replay of page 4000 reused
the plan the API had just cached and read 245,506 pages, which is why the replay models the plan cache (scenario 07 vs 07b). The cost still grows linearly with the page number: page 4000 is acceptable, page 400,000 of a
10M-row table is not.

Improvements and trade-offs:
- **Keyset (seek) paging** (the documented improvement, not implemented): the client sends the last `(CreatedAt, Id)` instead of `page`, and the query is `WHERE (CreatedAt < @lastCreatedAt OR (CreatedAt = @lastCreatedAt AND Id < @lastId))
  ORDER BY … FETCH NEXT 20`, a seek with constant cost at any depth (about the 6 reads of page 1). Trade-offs: no jump to an arbitrary page number or "page 4000 of 5000" in the UI, the cursor must be opaque and tied to one sort
  order (another `sortBy` needs its own index and cursor), and a total count needs a separate query.
- **Cap the offset** (reject `page × pageSize` above, say, 10,000 and ask for a narrower filter): one validation rule, cheap, but changes behaviour.
- **`OPTION (RECOMPILE)` / `OPTIMIZE FOR`** only for deep offsets: removes the sniffing at the price of a compile per request, and needs an EF command interceptor. The covering index already bounds the damage, so not done.

### 7.2 `COUNT(*)` over large filtered sets

Every list request runs a count (D-041). It is cheap while a narrow index matches the filter (5–138 reads) and linear in the rows matched otherwise: an unfiltered count reads the whole 138-page `IX_Requests_Status`
index; a combination without a matching narrow index (for example `priority` + a date range, or two exact filters on different columns) scans the covering `CreatedAt` index (up to 1,975 reads).

Improvements: (a) **cached counts** keyed by the normalized filter (short TTL like the summary; stale by up to the TTL, memory per distinct filter); (b) an **approximate or lazy count**: for no filter, read
`sys.dm_db_partition_stats` (O(1), approximate under concurrent writes); (c) **"has next page" instead of a total**: fetch `pageSize + 1` rows (no count at all, but the UI loses "page x of y");
(d) an **indexed view** for the one or two hottest filters (extra write cost and locking on every update).

### 7.3 Prefix search has no infix matching, and rare terms scan

`LIKE 'term%'` is sargable (seeks on `IX_Requests_Title` and the organization index), `LIKE '%term%'` is not: it reads every row (a 1,975-page scan is ~20 ms here, a hundred times that at 10M rows). We do not offer it (A4), so a user typing
the middle of a word gets no match. A second, measured limitation (7.4): when the term is rare the page query still scans in `CreatedAt` order looking for 20 matches that do not exist.

Improvement: **SQL Server Full-Text Search** (`CONTAINS(Title, '"term*"')`) on `Title` and `OrganizationName`: matches words anywhere and can rank results. Trade-offs: needs the Full-Text feature (not in LocalDB, so local dev and tests would
need another path; fine in the SQL Server container), a catalog populated asynchronously (new rows searchable after a delay), word-based semantics (no arbitrary substring), and EF needs `FromSql` or a custom function. Alternatives: an n-gram/trigram
side table (exact infix, more storage and write cost) or an external engine (Elasticsearch, Azure AI Search: best relevance, another system to run).

### 7.4 What the changes made worse (honest regressions)

| Statement | Before | After | Why |
|---|---:|---:|---|
| 12 rare search, page query (1 match) | 11 reads (union of two seeks + sort of 1 row), API 4.9 ms when its own plan is used | 1,975 reads, SQL 20–51 ms, API 66 ms | with the covering index the optimizer prefers an ordered scan with a residual `LIKE` and a row goal of 20; for a term with one match it reads the whole index |
| 06 date range, count | 5 | 22 | the range seek reads the wide index instead of the narrow one |
| API p50 of the cheap page-1 scenarios | 5–29 ms | 6–34 ms | +2–5 ms in five scenarios, not explained by reads (see section 5) |
| Seeder (bulk load) / storage / status-update reads | 6.8–7.5 s / 37 MB / 11 | 10–17 s / 55 MB / 22 | section 4.4 |

These are accepted because the new worst case is **bounded** (about one scan of a 15 MB index, roughly 20–55 ms), while the old worst case was **unbounded** (245,183 reads; the API's rare-search p50 was 479 ms because it reused the
plan of a common search). A row-goal hint (`OPTION (USE HINT('DISABLE_OPTIMIZER_ROWGOAL'))`) would fix the rare-term case but pessimize common terms and needs an interceptor; full-text search (7.3) is the real fix. Note that when the old
plan for the rare term was its own (4.9 ms), the old configuration was better for that single case; it was the plan reuse that made it bad in a running API.

### 7.5 What breaks at 10 million rows (extrapolation, not measured)

Reads grow with the table, so a 100× larger table makes every *unbounded* shape about 100× more expensive, while seeks stay almost flat (an index seek adds a level, about 1–2 reads):

- `COUNT(*)` of an unfiltered list: 138 → ~14,000 pages (110 MB read per request); the summary scans are the same, which is why they are cached.
- Deep offsets: page 4000 → page 400,000 reads 100× more (≈ 158,000 pages even with the covering index); keyset paging becomes mandatory.
- Rare-term / rare-filter scans of the covering index: 1,975 → ~197,000 pages (1.5 GB) per request, which no longer fits comfortably in a small buffer pool, so latency turns from CPU to disk.
- Storage: the covering index alone is ~1.5 GB, everything ~5.5 GB; builds, rebuilds and seeding take minutes to hours (load first, create indexes after); every status update touches 6 index structures.
- `LIKE '%x%'` (if ever allowed) scans 1.5 GB per query; full-text or external search becomes necessary.
- Plan sniffing and stale statistics matter more (statistics update rarely on big tables); next steps would be partitioning by `CreatedAt`, archiving Completed requests, and a read replica for list and summary.

## 8. Limitations of this analysis

- LocalDB 2019 Express on a laptop, one user, whole database cached: elapsed times are indicative, logical reads are the reliable metric. The CI container (SQL Server 2022) may choose different plans.
- Sequential requests only: this measures cost per request, not throughput or lock contention under concurrent writers. The seeder writes no history, so the history query was not measured (it is a seek on
  `IX_RequestStatusHistory_RequestId_ChangedAt`).
- Medians of 7 warm runs (5 for ablations); the cold run is a single sample. Statistics are updated with `FULLSCAN` and indexes rebuilt before each run, so results describe a well-maintained database
  (a freshly bulk-loaded one reads slightly more pages).
- The scenarios use one representative value per filter shape; a different value (another organization, another search term) can choose a different plan, which is what the rare-term case shows.
- The section 7.5 numbers are linear extrapolations.
