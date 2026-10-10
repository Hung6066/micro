[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$SubscriptionId,
    [Parameter(Mandatory)][string]$ResourceGroup,
    [string]$Location = 'southeastasia',
    [string]$ParametersFile = '',
    [string]$Prefix = 'hishop',
    [string]$OutputPath = 'artifacts/azure/phase0-deployment.json',
    [switch]$WhatIf
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$repoRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$bicepFile = Join-Path $repoRoot 'infra/azure/phase0/main.bicep'
if (-not (Test-Path -LiteralPath $bicepFile)) { throw "Bicep template not found: $bicepFile" }

if (-not (Get-Command az -ErrorAction SilentlyContinue)) {
    throw 'Azure CLI (az) is required. Install from https://learn.microsoft.com/cli/azure/install-azure-cli'
}

$account = az account show --subscription $SubscriptionId 2>$null | ConvertFrom-Json
if (-not $account) {
    throw "Not logged in or subscription '$SubscriptionId' not accessible. Run: az login"
}

Write-Host "Target subscription: $($account.name) ($SubscriptionId)"

$rgExists = az group exists --name $ResourceGroup | ConvertFrom-Json
if (-not $rgExists) {
    if ($WhatIf) {
        Write-Host "[WhatIf] Would create resource group '$ResourceGroup' in '$Location'."
    }
    else {
        az group create --name $ResourceGroup --location $Location --tags environment=azure-staging phase=0 | Out-Null
        if ($LASTEXITCODE -ne 0) { throw "Failed to create resource group '$ResourceGroup'." }
    }
}

$deployArgs = @(
    'deployment', 'group', 'create',
    '--subscription', $SubscriptionId,
    '--resource-group', $ResourceGroup,
    '--template-file', $bicepFile,
    '--parameters', "prefix=$Prefix", "location=$Location"
)

if ($ParametersFile) {
    if (-not (Test-Path -LiteralPath $ParametersFile)) { throw "Parameters file not found: $ParametersFile" }
    $deployArgs += @('--parameters', "@$ParametersFile")
}

if ($WhatIf) {
    $deployArgs += @('--what-if')
    & az @deployArgs
    if ($LASTEXITCODE -ne 0) { throw 'Azure what-if deployment failed.' }
    return
}

& az @deployArgs
if ($LASTEXITCODE -ne 0) { throw 'Azure deployment failed.' }

$deploymentName = (az deployment group list --resource-group $ResourceGroup --query "[0].name" -o tsv).Trim()
$outputs = az deployment group show `
    --resource-group $ResourceGroup `
    --name $deploymentName `
    --query 'properties.outputs' `
    -o json | ConvertFrom-Json

$artifact = [ordered]@{
    deployedAtUtc   = (Get-Date).ToUniversalTime().ToString('o')
    subscriptionId  = $SubscriptionId
    resourceGroup   = $ResourceGroup
    location        = $Location
    prefix          = $Prefix
    postgresFqdn    = $outputs.postgresFqdn.value
    postgresDatabase = $outputs.postgresDatabase.value
    postgresAdminUser = $outputs.postgresAdminUser.value
    redisHostName   = $outputs.redisHostName.value
    redisSslPort    = $outputs.redisSslPort.value
    keyVaultName    = $outputs.keyVaultName.value
    keyVaultUri     = $outputs.keyVaultUri.value
    acrLoginServer  = $outputs.acrLoginServer.value
    acrName         = $outputs.acrName.value
    backupStorageAccount = $outputs.backupStorageAccount.value
    appInsightsConnectionString = $outputs.appInsightsConnectionString.value
}

$outDir = Split-Path -Parent (Join-Path $repoRoot $OutputPath)
if ($outDir -and -not (Test-Path -LiteralPath $outDir)) {
    New-Item -ItemType Directory -Force -Path $outDir | Out-Null
}

$fullOutput = Join-Path $repoRoot $OutputPath
$artifact | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $fullOutput -Encoding utf8
Write-Host "Deployment outputs written to $fullOutput"
Write-Host "Next: ./scripts/azure/Configure-AzureStagingSecrets.ps1 -DeploymentArtifact $OutputPath"
