# Scenario definitions for Run-SqlPerf.ps1. Each statement is the SQL EF Core generates (copied from the API log
# with Microsoft.EntityFrameworkCore.Database.Command at Information), replayed through sp_executesql with the same
# parameter types. Values are representative of the seeded data (seed 42, anchor 2026-10-06, 100k rows):
#   Evergreen Health = rarest organization (145 rows), Acme Ltd = largest (5,812 rows, ~5.8%),
#   Status InProgress = 25,038 rows (25%), CreatedAt spans 2024-10-06 .. 2026-10-05.

$cols = '[r].[Id], [r].[Title], [r].[OrganizationName], [r].[Status], [r].[Priority], [r].[AssignedTo], [r].[CreatedAt], [r].[UpdatedAt], [r].[RowVersion]'
$byCreated = 'ORDER BY [r].[CreatedAt] DESC, [r].[Id] DESC'
$searchWhere = "[r].[Title] LIKE @pattern ESCAPE N'\' OR [r].[OrganizationName] LIKE @pattern ESCAPE N'\'"

function New-ListScenario($name, $title, $where, $paramDecl, $paramValues, $order, $skip, [string]$option = "", [switch]$prime) {
    $w = if ($where) { "`nWHERE $where" } else { '' }
    [pscustomobject]@{
        Name  = $name
        Title = $title
        Statements = @(
            [pscustomobject]@{ Kind = 'count'; Sql = "SELECT COUNT(*)`nFROM [Requests] AS [r]$w"; Decl = $paramDecl; Values = $paramValues },
            [pscustomobject]@{ Kind = 'page'; Sql = "SELECT $cols`nFROM [Requests] AS [r]$w`n$order`nOFFSET @p ROWS FETCH NEXT @p1 ROWS ONLY$option";
                              Decl = ($paramDecl + ', @p int, @p1 int').TrimStart(', '); Values = ($paramValues + ", @p = $skip, @p1 = 20").TrimStart(', ');
                              # Same statement text executed first with page-1 values: compiles (and caches) the plan the
                              # API would already have in its plan cache, so the measured run reuses a "sniffed" plan.
                              PrimeValues = if ($prime) { ($paramValues + ', @p = 0, @p1 = 20').TrimStart(', ') } else { $null } }
        )
    }
}

$scenarios = @(
    (New-ListScenario '01-default' 'Default sort, page 1' '' '' '' $byCreated 0),
    (New-ListScenario '02-status-created' 'status=InProgress, sort createdAt desc, page 1' '[r].[Status] = @statuses1' '@statuses1 tinyint' '@statuses1 = 2' $byCreated 0),
    (New-ListScenario '03-org-rare' 'organizationName = Evergreen Health (145 rows), page 1' '[r].[OrganizationName] = @organizationName' '@organizationName nvarchar(200)' "@organizationName = N'Evergreen Health'" $byCreated 0),
    (New-ListScenario '04-org-large' 'organizationName = Acme Ltd (5,812 rows), page 1' '[r].[OrganizationName] = @organizationName' '@organizationName nvarchar(200)' "@organizationName = N'Acme Ltd'" $byCreated 0),
    (New-ListScenario '05-prefix-search' "search = 'License' (7,180 matches), page 1" $searchWhere '@pattern nvarchar(200)' "@pattern = N'License%'" $byCreated 0),
    (New-ListScenario '06-date-range' 'createdFrom/To = one week, page 1' '[r].[CreatedAt] >= @start AND [r].[CreatedAt] < @endExclusive' '@start datetime2, @endExclusive datetime2' "@start = '2026-09-01', @endExclusive = '2026-09-08'" $byCreated 0),
    (New-ListScenario '07-deep-page' 'Default sort, page 4000 (OFFSET 79,980), plan already cached for page 1 (as in the API)' '' '' '' $byCreated 79980 -prime),
    (New-ListScenario '07b-deep-page-recompile' 'Default sort, page 4000, OPTION (RECOMPILE): plan built for the real OFFSET' '' '' '' $byCreated 79980 "`nOPTION (RECOMPILE)"),
    (New-ListScenario '08-status-title' 'status=New, sort title desc, page 1' '[r].[Status] = @statuses1' '@statuses1 tinyint' '@statuses1 = 1' 'ORDER BY [r].[Title] DESC, [r].[Id] DESC' 0),
    [pscustomobject]@{
        Name = '09-summary'; Title = 'GET /summary uncached: two GROUP BY queries'
        Statements = @(
            [pscustomobject]@{ Kind = 'status'; Sql = "SELECT [r].[Status] AS [Key], COUNT(*) AS [Count]`nFROM [Requests] AS [r]`nGROUP BY [r].[Status]"; Decl = ''; Values = '' },
            [pscustomobject]@{ Kind = 'priority'; Sql = "SELECT [r].[Priority] AS [Key], COUNT(*) AS [Count]`nFROM [Requests] AS [r]`nGROUP BY [r].[Priority]"; Decl = ''; Values = '' }
        )
    },
    (New-ListScenario '10-status-daterange' 'status=InProgress + 3-month date range (3,157 rows), page 1' '[r].[Status] = @statuses1 AND [r].[CreatedAt] >= @start AND [r].[CreatedAt] < @endExclusive' '@statuses1 tinyint, @start datetime2, @endExclusive datetime2' "@statuses1 = 2, @start = '2026-07-01', @endExclusive = '2026-10-01'" $byCreated 0),
    (New-ListScenario '11-assignee' 'assignedTo = tamar.katz (2,504 rows), page 1' '[r].[AssignedTo] = @assignedTo' '@assignedTo nvarchar(100)' "@assignedTo = N'tamar.katz'" $byCreated 0),
    (New-ListScenario '12-search-rare' "search = 'Permit application #8228' (1 match), page 1" $searchWhere '@pattern nvarchar(200)' "@pattern = N'Permit application #8228%'" $byCreated 0),
    (New-ListScenario '13-priority-high' 'priority=High (19,941 rows), page 1' '[r].[Priority] = @priorities1' '@priorities1 tinyint' '@priorities1 = 3' $byCreated 0),
    (New-ListScenario '14-sort-updatedat' 'No filter, sort updatedAt desc (no index on the sort column), page 1' '' '' '' 'ORDER BY [r].[UpdatedAt] DESC, [r].[Id] DESC' 0),
    (New-ListScenario '15-sort-priority' 'No filter, sort priority desc (no index on the sort column), page 1' '' '' '' 'ORDER BY [r].[Priority] DESC, [r].[Id] DESC' 0),
    (New-ListScenario '16-sort-status' 'No filter, sort status desc (no index on the sort column), page 1' '' '' '' 'ORDER BY [r].[Status] DESC, [r].[Id] DESC' 0),
    (New-ListScenario '17-status-priority' 'status=InProgress + priority=High (about 5,000 rows), page 1' '[r].[Status] = @statuses1 AND [r].[Priority] = @priorities1' '@statuses1 tinyint, @priorities1 tinyint' '@statuses1 = 2, @priorities1 = 3' $byCreated 0)
)
