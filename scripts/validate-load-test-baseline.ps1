[CmdletBinding()]
param(
    [string]$SummaryPath = 'tests/load/results/baseline-summary.json',
    [double]$MaxP95Ms = 500,
    [double]$MaxErrorRate = 0.01
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

if (-not (Test-Path -LiteralPath $SummaryPath -PathType Leaf)) {
    Write-Warning "Load test summary missing: $SummaryPath. Run k6 tests/Load/baseline-load-test.js first."
    exit 70
}

$summary = Get-Content -LiteralPath $SummaryPath -Raw | ConvertFrom-Json
$p95 = [double]$summary.metrics.http_req_duration.values.'p(95)'
$errorRate = [double]$summary.metrics.errors.values.rate

if ($p95 -gt $MaxP95Ms) {
    throw "Load baseline p95 ${p95}ms exceeds ${MaxP95Ms}ms."
}
if ($errorRate -gt $MaxErrorRate) {
    throw "Load baseline error rate $errorRate exceeds $MaxErrorRate."
}

Write-Host "Load baseline gate passed: p95=${p95}ms errorRate=$errorRate"
