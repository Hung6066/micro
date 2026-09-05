[CmdletBinding()]
param([string]$Root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$profiles = @{
    'tests/Load/baseline-load-test.js' = @('AUTH_TOKEN', 'setup()', 'Authorization')
    'tests/Load/k6/manufacturing-scale.js' = @('AUTH_TOKEN', 'TENANT_KEY', 'X-HisHope-Tenant', 'outbox')
    'tests/Load/k6/identity-scale.js' = @('AUTH_TOKEN', 'switchable-tenants', 'provisioning/delivery-health', 'security-signals/outbox')
}
foreach ($relative in $profiles.Keys) {
    $path = Join-Path $Root $relative
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { throw "Load profile missing: $relative" }
    $source = Get-Content -LiteralPath $path -Raw
    foreach ($required in $profiles[$relative]) {
        if ($source -notmatch [regex]::Escape($required)) { throw "$relative is missing required contract token '$required'." }
    }
}
Write-Host "Load profile contract passed: $($profiles.Count) authenticated profiles cover tenant and outbox workloads."
