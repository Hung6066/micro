[CmdletBinding()]
param(
    [string]$DeploymentArtifact = 'artifacts/azure/phase0-deployment.json',
    [string]$EnvTemplate = 'config/environments/azure-staging.env.example',
    [string]$PostgresPassword = '',
    [switch]$Apply
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$repoRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$artifactPath = Join-Path $repoRoot $DeploymentArtifact
if (-not (Test-Path -LiteralPath $artifactPath)) {
    throw "Deployment artifact not found: $artifactPath. Run Deploy-Phase0.ps1 first."
}

$artifact = Get-Content -LiteralPath $artifactPath -Raw | ConvertFrom-Json
$kvName = $artifact.keyVaultName
if ([string]::IsNullOrWhiteSpace($kvName)) { throw 'keyVaultName missing from deployment artifact.' }

if (-not $Apply) {
    Write-Output @"
Dry-run only. Re-run with -Apply to store secrets in Key Vault '$kvName'.

Required manual inputs when applying:
  -PostgresPassword '<strong-password-used-in-bicep>'
  - Redis primary key: az redis list-keys --name $($artifact.redisHostName -replace '\..*','') --resource-group $($artifact.resourceGroup)
"@
    return
}

if ([string]::IsNullOrWhiteSpace($PostgresPassword)) {
    throw 'PostgresPassword is required when -Apply is set.'
}

function Set-KvSecret([string]$Name, [string]$Value) {
    az keyvault secret set --vault-name $kvName --name $Name --value $Value | Out-Null
    if ($LASTEXITCODE -ne 0) { throw "Failed to set Key Vault secret '$Name'." }
}

$redisName = ($artifact.redisHostName -split '\.')[0]
$redisKeys = az redis list-keys --name $redisName --resource-group $artifact.resourceGroup -o json | ConvertFrom-Json
$redisKey = $redisKeys.primaryKey
if ([string]::IsNullOrWhiteSpace($redisKey)) { throw 'Unable to read Redis primary key.' }

$jwtKey = [Convert]::ToBase64String((1..64 | ForEach-Object { Get-Random -Maximum 256 }))

Set-KvSecret 'his-hope/postgres-admin-password' $PostgresPassword
Set-KvSecret 'his-hope/redis-primary-key' $redisKey
Set-KvSecret 'his-hope/jwt-signing-key' $jwtKey

$runtimePayload = @{
    postgresHost     = $artifact.postgresFqdn
    postgresDatabase = $artifact.postgresDatabase
    postgresUser     = $artifact.postgresAdminUser
    redisHost        = $artifact.redisHostName
    redisSslPort     = $artifact.redisSslPort
    acrLoginServer   = $artifact.acrLoginServer
    appInsights      = $artifact.appInsightsConnectionString
} | ConvertTo-Json -Compress

Set-KvSecret 'his-hope-azure-staging-runtime' $runtimePayload
Write-Output "Secrets stored in Key Vault '$kvName'. Update DNS/TLS and deploy Identity with ASPNETCORE_ENVIRONMENT=Azure.Staging."
