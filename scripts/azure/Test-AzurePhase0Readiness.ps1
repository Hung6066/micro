[CmdletBinding()]
param(
    [string]$DeploymentArtifact = 'artifacts/azure/phase0-deployment.json',
    [string]$ResourceGroup = ''
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$repoRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$artifactPath = Join-Path $repoRoot $DeploymentArtifact
if (-not (Test-Path -LiteralPath $artifactPath)) {
    throw "Deployment artifact not found: $artifactPath"
}

$artifact = Get-Content -LiteralPath $artifactPath -Raw | ConvertFrom-Json
$rg = if ($ResourceGroup) { $ResourceGroup } else { $artifact.resourceGroup }

$checks = [System.Collections.Generic.List[object]]::new()

function Add-Check([string]$Name, [bool]$Passed, [string]$Detail) {
    $checks.Add([pscustomobject]@{ name = $Name; passed = $Passed; detail = $Detail })
}

$pgState = az postgres flexible-server show `
    --resource-group $rg `
    --name (($artifact.postgresFqdn -split '\.')[0]) `
    --query 'state' -o tsv 2>$null
Add-Check 'postgres-server' ($pgState -eq 'Ready') "state=$pgState"

$redisState = az redis show `
    --resource-group $rg `
    --name (($artifact.redisHostName -split '\.')[0]) `
    --query 'provisioningState' -o tsv 2>$null
Add-Check 'redis-cache' ($redisState -eq 'Succeeded') "state=$redisState"

$kvState = az keyvault show --name $artifact.keyVaultName --query 'properties.provisioningState' -o tsv 2>$null
Add-Check 'key-vault' ($kvState -eq 'Succeeded') "state=$kvState"

$acrState = az acr show --name $artifact.acrName --resource-group $rg --query 'provisioningState' -o tsv 2>$null
Add-Check 'container-registry' ($acrState -eq 'Succeeded') "state=$acrState"

$failed = @($checks | Where-Object { -not $_.passed })
$report = [ordered]@{
    checkedAtUtc = (Get-Date).ToUniversalTime().ToString('o')
    resourceGroup = $rg
    checks = $checks
    passed = ($failed.Count -eq 0)
}

$outDir = Join-Path $repoRoot 'artifacts/azure'
if (-not (Test-Path -LiteralPath $outDir)) { New-Item -ItemType Directory -Force -Path $outDir | Out-Null }
$reportPath = Join-Path $outDir 'phase0-readiness.json'
$report | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $reportPath -Encoding utf8

$report | ConvertTo-Json -Depth 5
if ($failed.Count -gt 0) {
    throw "Azure Phase 0 readiness failed: $($failed.name -join ', ')"
}
