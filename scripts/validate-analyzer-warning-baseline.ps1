[CmdletBinding()]
param(
    [string]$SolutionPath = 'His.Hope.sln',
    [string]$BaselinePath = 'config/analyzer-warning-baseline.json',
    [string]$Configuration = 'Release',
    [switch]$UpdateBaseline
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$solution = (Resolve-Path (Join-Path $root $SolutionPath)).Path
$baselineFile = Join-Path $root $BaselinePath
$logFile = Join-Path $root 'artifacts/analyzer-baseline-build.log'
New-Item -ItemType Directory -Force -Path (Split-Path -Parent $logFile) | Out-Null

& dotnet build $solution --no-restore --configuration $Configuration -t:Rebuild -m:1 `
    -p:AnalysisLevel=latest-recommended -v:minimal *> $logFile
if ($LASTEXITCODE -ne 0) {
    throw "Analyzer build failed with exit code $LASTEXITCODE. See $logFile."
}

$warningCounts = Get-Content -LiteralPath $logFile | ForEach-Object {
    if ($_ -match '^\s*(?<count>\d+)\s+Warning\(s\)\s*$') {
        [int]$Matches.count
    }
}
if (-not $warningCounts) {
    throw "Analyzer build did not emit a warning summary. See $logFile."
}
$current = ($warningCounts | Measure-Object -Maximum).Maximum

if ($UpdateBaseline) {
    $record = [ordered]@{
        analysisLevel = 'latest-recommended'
        configuration = $Configuration
        warningCount = $current
        generatedAtUtc = [DateTime]::UtcNow.ToString('O')
    }
    New-Item -ItemType Directory -Force -Path (Split-Path -Parent $baselineFile) | Out-Null
    $record | ConvertTo-Json | Set-Content -LiteralPath $baselineFile -Encoding utf8
    Write-Host "Analyzer warning baseline updated: $current"
    exit 0
}

if (-not (Test-Path -LiteralPath $baselineFile -PathType Leaf)) {
    throw "Analyzer warning baseline missing: $baselineFile. Run with -UpdateBaseline deliberately."
}
$baseline = Get-Content -LiteralPath $baselineFile -Raw | ConvertFrom-Json
$expected = [int]$baseline.warningCount
if ($current -gt $expected) {
    throw "Analyzer warning baseline exceeded: current=$current baseline=$expected. Fix warnings or review the baseline change."
}

Write-Host "Analyzer warning baseline passed: current=$current baseline=$expected"
