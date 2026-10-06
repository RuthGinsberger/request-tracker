<#
.SYNOPSIS
  Replays the EF-generated list/summary SQL against the seeded database and records logical reads, CPU and elapsed
  time (median of N warm runs, plus one cold-cache run) and the actual execution plan of every statement.

.PARAMETER Label
  Name of the run; results go to docs/performance/results-<Label>.csv and plans to docs/performance/plans/<Label>/.

.PARAMETER Config
  current : measure with the indexes as they exist in the database (apply migrations first).
  none    : disable every non-clustered index on Requests for the run (clustered PK only), then rebuild them.

.EXAMPLE
  dotnet run --project backend/tools/RequestTracker.Seeder -- --reset --anchor 2026-10-06
  ./perf/Run-SqlPerf.ps1 -Label no-indexes -Config none
  ./perf/Run-SqlPerf.ps1 -Label with-indexes
#>
param(
    [Parameter(Mandatory)][string]$Label,
    [ValidateSet('current', 'none')][string]$Config = 'current',
    [int]$Runs = 7,
    [int]$Warmup = 3,
    [string]$Only = "",
    [switch]$NoRebuild,
    [switch]$Merge,
    [string[]]$Disable = @(),
    [string]$Server = '(localdb)\MSSQLLocalDB',
    [string]$Database = 'RequestTracker'
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$outDir = Join-Path $root 'docs/performance'
$planDir = Join-Path $outDir "plans/$Label"
New-Item -ItemType Directory -Force $planDir | Out-Null
. (Join-Path $PSScriptRoot 'Scenarios.ps1')

function Invoke-Sql([string]$Sql, [switch]$Plan) {
    $file = [IO.Path]::GetTempFileName()
    try {
        Set-Content -Path $file -Value $Sql -Encoding UTF8
        $sqlcmdArgs = @('-S', $Server, '-d', $Database, '-i', $file, '-y', '0', '-b')
        $out = & sqlcmd @sqlcmdArgs
        if ($LASTEXITCODE -ne 0) { throw "sqlcmd failed: $($out -join "`n")" }
        return $out
    }
    finally { Remove-Item $file -ErrorAction SilentlyContinue }
}

function New-Batch($stmt, [string]$Prefix) {
    $sql = $stmt.Sql.Replace("'", "''")
    $exec = "EXEC sp_executesql N'$sql', N'$($stmt.Decl)', "
    $call = if ($stmt.Decl) { "$exec$($stmt.Values);" } else { "EXEC sp_executesql N'$sql';" }
    # Statements with PrimeValues first run once with page-1 values in the same session, from an empty plan cache, so the
    # measured execution reuses the plan compiled for page 1 (what the API's plan cache holds after the first request).
    $prime = if ($stmt.PrimeValues) { "DBCC FREEPROCCACHE WITH NO_INFOMSGS;`n$exec$($stmt.PrimeValues);`n" } else { '' }
    return "$prime$Prefix`n$call`n"
}

function Read-Stats($out) {
    $reads = 0; $phys = 0; $ra = 0
    foreach ($line in $out) {
        if ($line -match "^Table '.*logical reads (\d+), physical reads (\d+).*, read-ahead reads (\d+)") { $reads += [int]$Matches[1]; $phys += [int]$Matches[2]; $ra += [int]$Matches[3] }
    }
    # The statement's own timing is the first "Execution Times" block after the last "Table ..." line (a second
    # block that follows is the batch total).
    $lastTable = 0
    for ($i = 0; $i -lt $out.Count; $i++) { if ($out[$i] -like "Table '*") { $lastTable = $i } }
    $cpu = 0; $elapsed = 0
    for ($i = $lastTable; $i -lt $out.Count; $i++) {
        if ($out[$i] -match 'CPU time = (\d+) ms,\s+elapsed time = (\d+) ms') { $cpu = [int]$Matches[1]; $elapsed = [int]$Matches[2]; break }
    }
    [pscustomobject]@{ Reads = $reads; Physical = ($phys + $ra); Cpu = $cpu; Elapsed = $elapsed }
}


function Get-Median($values) {
    $s = @($values | Sort-Object); $n = $s.Count
    if ($n % 2) { return $s[($n - 1) / 2] } else { return ($s[$n / 2 - 1] + $s[$n / 2]) / 2 }
}

$indexes = @(Invoke-Sql "SET NOCOUNT ON; SELECT name FROM sys.indexes WHERE object_id = OBJECT_ID('dbo.Requests') AND type = 2;" | Where-Object { $_ -match '^IX_' })
$serverVersion = (Invoke-Sql "SET NOCOUNT ON; SELECT @@VERSION;" | Where-Object { $_ -match "SQL Server" } | Select-Object -First 1) -join ' '
Write-Host "Label=$Label Config=$Config Runs=$Runs Indexes=[$($indexes -join ', ')]"
Write-Host $serverVersion

try {
    if (-not $NoRebuild -and $Config -eq "current") { Invoke-Sql "ALTER INDEX ALL ON dbo.Requests REBUILD;" | Out-Null }
    if ($Config -eq 'none') { $Disable = $indexes }
    if ($Disable.Count) {
        foreach ($ix in $Disable) { Invoke-Sql "ALTER INDEX [$ix] ON dbo.Requests DISABLE;" | Out-Null }
    }
    Invoke-Sql "UPDATE STATISTICS dbo.Requests WITH FULLSCAN;" | Out-Null

    $rows = foreach ($sc in ($scenarios | Where-Object { -not $Only -or $_.Name -like "$Only*" })) {
        foreach ($st in $sc.Statements) {
            Write-Host ("{0} {1}" -f $sc.Name, $st.Kind)
            # Plan (actual) first; it also warms the plan cache and the buffer pool.
            $planOut = Invoke-Sql (New-Batch $st 'DBCC FREEPROCCACHE WITH NO_INFOMSGS; SET STATISTICS XML ON;') -Plan
            $xml = $planOut | Where-Object { $_ -like '<ShowPlanXML*' } | Select-Object -First 1
            if ($xml) { Set-Content -Path (Join-Path $planDir "$($sc.Name)-$($st.Kind).sqlplan") -Value $xml -Encoding UTF8 }

            $stats = 1..($Warmup + $Runs) | ForEach-Object { Read-Stats (Invoke-Sql (New-Batch $st 'SET STATISTICS IO ON; SET STATISTICS TIME ON;')) } | Select-Object -Skip $Warmup
            $cold = Read-Stats (Invoke-Sql (New-Batch $st "CHECKPOINT; DBCC DROPCLEANBUFFERS WITH NO_INFOMSGS; SET STATISTICS IO ON; SET STATISTICS TIME ON;"))
            [pscustomobject]@{
                Label = $Label; Scenario = $sc.Name; Statement = $st.Kind
                LogicalReads = (Get-Median ($stats.Reads)); CpuMs = (Get-Median ($stats.Cpu)); ElapsedMs = (Get-Median ($stats.Elapsed))
                ColdElapsedMs = $cold.Elapsed; ColdPhysicalReads = $cold.Physical
            }
        }
    }
    $csv = Join-Path $outDir "results-$Label.csv"
    if ($Merge -and (Test-Path $csv)) {
        # -Merge: keep the rows of scenarios not re-measured in this run (for adding one scenario to an existing result file).
        $kept = @(Import-Csv $csv | Where-Object { $_.Scenario -notin @($rows.Scenario) })
        $rows = @($kept) + @($rows) | Sort-Object Scenario, @{ Expression = { if ($_.Statement -in "count", "status") { 0 } else { 1 } } }
    }
    $rows | Export-Csv -Path $csv -NoTypeInformation
    $rows | Format-Table -AutoSize
    Write-Host "Wrote $csv and plans in $planDir"
}
finally {
    if ($Config -eq 'none') { $Disable = $indexes }
    if ($Disable.Count) {
        foreach ($ix in $Disable) { Invoke-Sql "ALTER INDEX [$ix] ON dbo.Requests REBUILD;" | Out-Null }
    }
}
