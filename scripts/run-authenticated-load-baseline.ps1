[CmdletBinding()]
param(
    [string]$BaseUrl = $(if ($env:BASE_URL) { $env:BASE_URL } else { 'http://localhost:5000' }),
    [int]$Vus = 50,
    [string]$Duration = '2m',
    [string]$Script = 'tests/Load/baseline-load-test.js'
)

$ErrorActionPreference = 'Stop'
if ([string]::IsNullOrWhiteSpace($env:AUTH_TOKEN)) {
    throw 'AUTH_TOKEN is required; refusing to create an unauthenticated production baseline.'
}
if (-not (Get-Command k6 -ErrorAction SilentlyContinue)) { throw 'k6 executable not found.' }
if (-not (Test-Path -LiteralPath $Script -PathType Leaf)) { throw "Load script not found: $Script" }

$env:BASE_URL = $BaseUrl
k6 run --vus $Vus --duration $Duration $Script
pwsh -NoProfile -File (Join-Path $PSScriptRoot 'validate-load-test-baseline.ps1')
