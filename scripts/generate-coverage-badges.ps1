# Generate coverage badges from ReportGenerator output
# Run this after the coverage job in CI

param(
    [string]$coverageFile = "coverage-report/Summary.json"
)

if (-not (Test-Path $coverageFile)) {
    Write-Host "Coverage summary not found at $coverageFile"
    exit 1
}

$summary = Get-Content $coverageFile | ConvertFrom-Json
$backendCoverage = $summary.summary.Percentage
$frontendCoverage = $summary.summary.FrontendPercentage

Write-Host "=== Coverage Summary ==="
Write-Host "Backend Coverage: $backendCoverage%"
Write-Host "Frontend Coverage: $frontendCoverage%"
Write-Host ""

if ($backendCoverage -ge 80) {
    Write-Host "✅ BACKEND: $backendCoverage% (passes 80% threshold)"
} else {
    Write-Host "❌ BACKEND: $backendCoverage% (below 80% threshold)"
}

if ($frontendCoverage -ge 75) {
    Write-Host "✅ FRONTEND: $frontendCoverage% (passes 75% threshold)"
} else {
    Write-Host "❌ FRONTEND: $frontendCoverage% (below 75% threshold)"
}
