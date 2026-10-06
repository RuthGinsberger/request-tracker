<#
.SYNOPSIS
  End-to-end latency of the central endpoints (p50 / p95 over N sequential requests) against a locally started API,
  and cold vs warm for GET /api/requests/summary.

.DESCRIPTION
  Builds the API in Release, starts it twice in Production mode (no EF logging, no OpenAPI) on the LocalDB database:
    * warm instance (port 5290): summary cache TTL 1 hour, so only the first summary request is a miss;
    * cold instance (port 5291): summary cache TTL 1 second, and the script sleeps 1.2 s between requests, so every
      request recomputes the aggregation (a cache miss = the uncached query cost plus HybridCache overhead).
  Requests are issued one at a time (no concurrency): this measures request cost, not throughput.
  Order matters on purpose: the default page 1 request runs first, so the cached plan for the page query is the one
  compiled for OFFSET 0, exactly as in a running API; the deep-page scenario then reuses it.

.EXAMPLE
  ./perf/Measure-Api.ps1 -Label final            # needs the seeded database (Seeder --reset) and the migrations applied
#>
param(
    [Parameter(Mandatory)][string]$Label,
    [int]$Requests = 50,
    [int]$Warmup = 5,
    [string]$ConnectionString = 'Server=(localdb)\MSSQLLocalDB;Database=RequestTracker;Trusted_Connection=True;TrustServerCertificate=True',
    [string]$SqlcmdServer = '(localdb)\MSSQLLocalDB',
    [string]$Database = 'RequestTracker'
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Net.Http
$root = Split-Path -Parent $PSScriptRoot
$outDir = Join-Path $root 'docs/performance'
$apiProject = Join-Path $root 'backend/src/RequestTracker.Api'

# The plan cache is shared by every client of the instance. Clear it so the plans are the ones the API itself compiles
# (otherwise plans left by perf/Run-SqlPerf.ps1 would be reused and flatter or distort the results).
sqlcmd -S $SqlcmdServer -d $Database -b -Q 'DBCC FREEPROCCACHE WITH NO_INFOMSGS' | Out-Null
if ($LASTEXITCODE -ne 0) { throw 'could not clear the plan cache' }

dotnet build $apiProject -c Release --nologo -v q | Out-Null
if ($LASTEXITCODE -ne 0) { throw 'build failed' }
$dll = Get-ChildItem (Join-Path $apiProject 'bin/Release') -Filter RequestTracker.Api.dll -Recurse | Select-Object -First 1

function Start-Api([int]$Port, [int]$SummaryTtlSeconds) {
    if ([Net.NetworkInformation.IPGlobalProperties]::GetIPGlobalProperties().GetActiveTcpListeners().Port -contains $Port) { throw "Port $Port is already in use (a previous run still alive?)" }
    $psi = [Diagnostics.ProcessStartInfo]::new('dotnet', "`"$($dll.FullName)`"")
    $psi.WorkingDirectory = $dll.DirectoryName
    $psi.UseShellExecute = $false
    $psi.RedirectStandardOutput = $true
    $psi.RedirectStandardError = $true
    $psi.Environment['ASPNETCORE_ENVIRONMENT'] = 'Production'
    $psi.Environment['ASPNETCORE_URLS'] = "http://localhost:$Port"
    $psi.Environment['ConnectionStrings__Default'] = $ConnectionString
    $psi.Environment['Cache__SummaryTtlSeconds'] = "$SummaryTtlSeconds"
    $p = [Diagnostics.Process]::Start($psi)
    $p.BeginOutputReadLine(); $p.BeginErrorReadLine()
    $client = [Net.Http.HttpClient]::new(); $client.Timeout = [TimeSpan]::FromSeconds(60)
    for ($i = 0; $i -lt 60; $i++) {
        try { if ($client.GetAsync("http://localhost:$Port/health").GetAwaiter().GetResult().IsSuccessStatusCode) { return [pscustomobject]@{ Process = $p; Client = $client; Base = "http://localhost:$Port" } } } catch { }
        Start-Sleep -Milliseconds 500
    }
    throw "API on port $Port did not start"
}

function Measure-Get($api, [string]$Path) {
    $sw = [Diagnostics.Stopwatch]::StartNew()
    $resp = $api.Client.GetAsync($api.Base + $Path).GetAwaiter().GetResult()
    $null = $resp.Content.ReadAsStringAsync().GetAwaiter().GetResult()
    $sw.Stop()
    if (-not $resp.IsSuccessStatusCode) { throw "$Path returned $([int]$resp.StatusCode)" }
    return $sw.Elapsed.TotalMilliseconds
}

function Get-Percentile($values, [double]$p) {
    $s = @($values | Sort-Object)
    return $s[[Math]::Min($s.Count - 1, [Math]::Ceiling($p * $s.Count) - 1)]
}

$rows = [System.Collections.Generic.List[object]]::new()
function Add-Row([string]$Scenario, [string]$Path, $times) {
    $rows.Add([pscustomobject]@{
        Label = $Label; Scenario = $Scenario; Path = $Path; N = @($times).Count
        P50Ms = [Math]::Round((Get-Percentile $times 0.5), 1); P95Ms = [Math]::Round((Get-Percentile $times 0.95), 1)
        MaxMs = [Math]::Round((($times | Measure-Object -Maximum).Maximum), 1)
    })
}

$warm = $null; $cold = $null
$warm = Start-Api 5290 3600
try { $cold = Start-Api 5291 1 } catch { $warm.Process.Kill(); throw }
try {
    $scenarios = [ordered]@{
        '01 default page 1'            = '/api/requests?pageSize=20'
        '02 status + createdAt sort'   = '/api/requests?status=InProgress&sortBy=createdAt&sortDirection=desc&pageSize=20'
        '03 organization exact'        = '/api/requests?organizationName=Evergreen%20Health&pageSize=20'
        '04 prefix search (common)'    = '/api/requests?search=License&pageSize=20'
        '05 date range (1 week)'       = '/api/requests?createdFrom=2026-09-01&createdTo=2026-09-07&pageSize=20'
        '06 deep page 4000'            = '/api/requests?page=4000&pageSize=20'
        '07 status + date range'       = '/api/requests?status=InProgress&createdFrom=2026-07-01&createdTo=2026-09-30&pageSize=20'
        '08 prefix search (rare)'      = '/api/requests?search=Permit%20application%20%238228&pageSize=20'
    }
    foreach ($name in $scenarios.Keys) {
        $path = $scenarios[$name]
        1..$Warmup | ForEach-Object { Measure-Get $warm $path | Out-Null }
        $times = 1..$Requests | ForEach-Object { Measure-Get $warm $path }
        Add-Row $name $path $times
        Write-Host "$name done"
    }

    # Summary: first request after process start (cache miss incl. JIT), warm = served from HybridCache, cold = miss on every request.
    $first = Measure-Get $warm '/api/requests/summary'
    Add-Row '09 summary: first request after start (miss + JIT)' '/api/requests/summary' @($first)
    Add-Row '09 summary: warm (cache hit)' '/api/requests/summary' (1..$Requests | ForEach-Object { Measure-Get $warm '/api/requests/summary' })

    Measure-Get $cold '/api/requests/summary' | Out-Null   # JIT, discarded
    $misses = 1..30 | ForEach-Object { Start-Sleep -Milliseconds 1200; Measure-Get $cold '/api/requests/summary' }
    Add-Row '09 summary: cold (cache miss, JIT excluded)' '/api/requests/summary' $misses
}
finally {
    foreach ($api in @($warm, $cold)) { if ($api -and -not $api.Process.HasExited) { $api.Process.Kill() } }
}

$csv = Join-Path $outDir "api-$Label.csv"
$rows | Export-Csv -Path $csv -NoTypeInformation
$rows | Format-Table Scenario, N, P50Ms, P95Ms, MaxMs -AutoSize
Write-Host "Wrote $csv"
