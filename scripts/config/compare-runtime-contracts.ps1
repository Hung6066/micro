[CmdletBinding()]
param(
    [Parameter(Mandatory)] [string]$ExpectedFile,
    [Parameter(Mandatory)] [string]$ActualFile
)
$ErrorActionPreference = 'Stop'
function Read-Env([string]$path) {
    $result = @{}
    foreach ($line in Get-Content -LiteralPath $path) {
        if ($line -match '^\s*(#|$)') { continue }
        if ($line -notmatch '^\s*([A-Z0-9_]+)=(.*)$') { throw "Invalid env line in ${path}: $line" }
        $result[$Matches[1]] = $Matches[2].Trim()
    }
    return $result
}
$expected = Read-Env $ExpectedFile
$actual = Read-Env $ActualFile
$keys = @($expected.Keys | Where-Object { $_ -match '^(HIS_HOPE_|SERVICE_)' })
$drift = foreach ($key in $keys) {
    if (-not $actual.ContainsKey($key)) { [pscustomobject]@{ Key=$key; Expected=$expected[$key]; Actual='<missing>' } }
    elseif ($expected[$key] -ne $actual[$key]) { [pscustomobject]@{ Key=$key; Expected=$expected[$key]; Actual=$actual[$key] } }
}
if ($drift) { $drift | Format-Table -AutoSize; throw "RUNTIME_DRIFT_DETECTED count=$($drift.Count)" }
Write-Output 'RUNTIME_DRIFT_CLEAR'
