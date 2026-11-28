[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][ValidatePattern('^[a-z0-9]{3,24}$')][string]$AccountName,
    [Parameter(Mandatory = $true)][ValidatePattern('^[a-z0-9-]{3,63}$')][string]$ContainerName,
    [ValidateRange(1, 36500)][int]$RetentionDays = 30,
    [string]$ResourceGroup,
    [switch]$Apply,
    [switch]$AllowProduction,
    [ValidateSet('LOCK-WORM')][string]$Confirmation
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

if ($Apply -and -not $AllowProduction) {
    throw 'Production WORM apply is blocked by default; rerun with -AllowProduction after change approval.'
}
if ($Apply -and $Confirmation -ne 'LOCK-WORM') {
    throw 'Applying an immutable policy requires -Confirmation LOCK-WORM.'
}

$common = @('--account-name', $AccountName, '--container-name', $ContainerName, '--only-show-errors')
$containerShow = @('storage', 'container', 'show', '--account-name', $AccountName, '--name', $ContainerName, '--only-show-errors')
if ($ResourceGroup) {
    $common += @('--resource-group', $ResourceGroup)
    $containerShow += @('--resource-group', $ResourceGroup)
}

function Invoke-AzJson([string[]]$Arguments) {
    $raw = & az @Arguments
    if ($LASTEXITCODE -ne 0) { throw "Azure CLI command failed with exit code $LASTEXITCODE." }
    if ([string]::IsNullOrWhiteSpace(($raw -join "`n"))) { return $null }
    return ($raw -join "`n") | ConvertFrom-Json
}

$container = Invoke-AzJson ($containerShow + @('--output', 'json'))
if (-not $container) { throw 'Azure container metadata was not returned.' }

$account = Invoke-AzJson @('storage', 'account', 'show', '--name', $AccountName, '--only-show-errors', '--output', 'json')
if (-not $account) { throw 'Azure storage account metadata was not returned.' }
$blobEncryption = $account.encryption.services.blob
Write-Output "Storage account state: httpsOnly=$($account.enableHttpsTrafficOnly) minimumTlsVersion=$($account.minimumTlsVersion) allowBlobPublicAccess=$($account.allowBlobPublicAccess) keySource=$($account.encryption.keySource) infrastructureEncryption=$($account.encryption.requireInfrastructureEncryption) sku=$($account.sku.name)"

$properties = $container.properties
$hasPolicy = [bool]$properties.hasImmutabilityPolicy
$versioning = [bool]$container.immutableStorageWithVersioningEnabled
Write-Output "Container '$ContainerName' current state: hasImmutabilityPolicy=$hasPolicy immutableStorageWithVersioningEnabled=$versioning publicAccess=$($properties.publicAccess)"

if ($Apply -and -not $versioning) {
    throw 'Production apply requires immutable storage with versioning. Provision a new container with that capability and migrate/verify data before locking WORM.'
}
if ($Apply -and $properties.publicAccess -and $properties.publicAccess -ne 'None') {
    throw 'Production apply requires private container access.'
}
if ($Apply -and $account.enableHttpsTrafficOnly -ne $true) {
    throw 'Production apply requires HTTPS-only storage account traffic.'
}
if ($Apply -and $account.minimumTlsVersion -ne 'TLS1_2') {
    throw 'Production apply requires storage account minimum TLS version TLS1_2.'
}
if ($Apply -and $account.allowBlobPublicAccess -ne $false) {
    throw 'Production apply requires blob public access to be disabled.'
}
if ($Apply -and $account.encryption.keySource -ne 'Microsoft.Keyvault') {
    throw 'Production apply requires a customer-managed Key Vault encryption key.'
}
if ($Apply -and $account.encryption.requireInfrastructureEncryption -ne $true) {
    throw 'Production apply requires infrastructure-level encryption.'
}

$existingPolicy = Invoke-AzJson (@('storage', 'container', 'immutability-policy', 'show') + $common + @('--output', 'json'))
$existingDays = if ($existingPolicy) { [int]$existingPolicy.immutabilityPeriodSinceCreationInDays } else { 0 }
$existingMode = if ($existingPolicy) { [string]$existingPolicy.policyMode } else { '' }
if ($existingMode -eq 'Locked' -and $existingDays -ge $RetentionDays) {
    Write-Output "Azure Blob WORM policy already satisfies retention: mode=Locked retentionDays=$existingDays"
    exit 0
}

if (-not $Apply) {
    if ($existingMode -eq 'Locked') {
        Write-Output "DRY-RUN: would extend the locked immutability policy from $existingDays to $RetentionDays days. No Azure mutation performed."
    } else {
        Write-Output "DRY-RUN: would create/update an unlocked $RetentionDays-day immutability policy, then lock it. No Azure mutation performed."
    }
    exit 0
}

if ($existingMode -eq 'Locked') {
    $locked = Invoke-AzJson (@('storage', 'container', 'immutability-policy', 'extend') + $common + @('--period', $RetentionDays, '--if-match', '*', '--output', 'json'))
    if (-not $locked) { throw 'Azure locked immutability policy extension did not return a policy.' }
} else {
    $create = Invoke-AzJson (@('storage', 'container', 'immutability-policy', 'create') + $common + @('--period', $RetentionDays, '--allow-protected-append-writes', 'false', '--output', 'json'))
    if (-not $create) { throw 'Azure immutability policy create did not return a policy.' }

    $locked = Invoke-AzJson (@('storage', 'container', 'immutability-policy', 'lock') + $common + @('--if-match', '*', '--output', 'json'))
    if (-not $locked) { throw 'Azure immutability policy lock did not return a policy.' }
}

Write-Output "Azure Blob WORM policy applied: container=$ContainerName retentionDays=$RetentionDays mode=Locked"
