<#
.SYNOPSIS
  Summarizes the saved actual plans of one run (docs/performance/plans/<Label>/*.sqlplan): the indexes each statement
  used, the main operators and the actual rows read at the leaf operators. Writes plan-summary-<Label>.csv.
#>
param([Parameter(Mandatory)][string]$Label)

$ErrorActionPreference = 'Stop'
$outDir = Join-Path (Split-Path -Parent $PSScriptRoot) 'docs/performance'
$rows = foreach ($file in Get-ChildItem (Join-Path $outDir "plans/$Label") -Filter *.sqlplan | Sort-Object Name) {
    [xml]$plan = Get-Content $file.FullName -Raw
    $ns = New-Object Xml.XmlNamespaceManager($plan.NameTable)
    $ns.AddNamespace('p', 'http://schemas.microsoft.com/sqlserver/2004/07/showplan')
    $ops = $plan.SelectNodes('//p:RelOp', $ns)
    $leafs = foreach ($op in $ops) {
        $obj = $op.SelectSingleNode('.//p:Object[@Index]', $ns)
        if ($obj -and $op.PhysicalOp -match 'Scan|Seek|Lookup') {
            $actual = ($op.SelectNodes('.//p:RunTimeCountersPerThread', $ns) | Measure-Object -Property ActualRows -Sum).Sum
            '{0} {1} ({2} rows)' -f $op.PhysicalOp, $obj.Index.Trim('[', ']'), $actual
        }
    }
    [pscustomobject]@{
        Plan = $file.BaseName
        Operators = (($ops | ForEach-Object PhysicalOp | Select-Object -Unique) -join ', ')
        LeafAccess = ($leafs | Select-Object -Unique) -join '; '
        Sort = [bool]($ops | Where-Object { $_.PhysicalOp -match 'Sort' })
    }
}
$csv = Join-Path $outDir "plan-summary-$Label.csv"
$rows | Export-Csv -Path $csv -NoTypeInformation
$rows | Format-Table Plan, LeafAccess, Sort -AutoSize -Wrap
Write-Host "Wrote $csv"
