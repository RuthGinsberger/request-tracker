<#
.SYNOPSIS
  Prints the markdown comparison tables used in docs/PERFORMANCE.md from the CSV files the other scripts wrote.
.EXAMPLE
  ./perf/Format-Results.ps1 -Labels no-indexes,initial-indexes,final-indexes
  ./perf/Format-Results.ps1 -Api -Labels initial-indexes,final-indexes
#>
param([Parameter(Mandatory)][string[]]$Labels, [switch]$Api)

$Labels = @($Labels | ForEach-Object { $_ -split "," })
$dir = Join-Path (Split-Path -Parent $PSScriptRoot) 'docs/performance'
$data = @{}
foreach ($l in $Labels) { $data[$l] = @(Import-Csv (Join-Path $dir ($(if ($Api) { "api-$l.csv" } else { "results-$l.csv" })))) }

if ($Api) {
    '| Scenario | ' + (($Labels | ForEach-Object { "p50 ms ($_) | p95 ms ($_)" }) -join ' | ') + ' |'
    '|---|' + (($Labels | ForEach-Object { '---:|---:' }) -join '|') + '|'
    foreach ($row in $data[$Labels[0]]) {
        $cells = foreach ($l in $Labels) { $r = $data[$l] | Where-Object Scenario -eq $row.Scenario; "$($r.P50Ms) | $($r.P95Ms)" }
        "| $($row.Scenario) | " + ($cells -join ' | ') + ' |'
    }
    return
}

'| Scenario | Statement | ' + (($Labels | ForEach-Object { "reads ($_)" }) -join ' | ') + ' | ' + (($Labels | ForEach-Object { "ms ($_)" }) -join ' | ') + ' |'
'|---|---|' + (($Labels | ForEach-Object { '---:' }) -join '|') + '|' + (($Labels | ForEach-Object { '---:' }) -join '|') + '|'
foreach ($row in $data[$Labels[0]]) {
    $reads = @(); $ms = @()
    foreach ($l in $Labels) {
        $r = $data[$l] | Where-Object { $_.Scenario -eq $row.Scenario -and $_.Statement -eq $row.Statement }
        $reads += ('{0:N0}' -f [int]$r.LogicalReads); $ms += $r.ElapsedMs
    }
    "| $($row.Scenario) | $($row.Statement) | " + ($reads -join ' | ') + ' | ' + ($ms -join ' | ') + ' |'
}
