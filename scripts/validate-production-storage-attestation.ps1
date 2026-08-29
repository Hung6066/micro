[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$AttestationPath,
    [string]$OutputPath,
    [ValidateRange(1, 36500)][int]$MinimumRetentionDays = 30
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

if (-not (Test-Path -LiteralPath $AttestationPath -PathType Leaf)) {
    throw "Attestation file not found: $AttestationPath"
}

$document = Get-Content -LiteralPath $AttestationPath -Raw | ConvertFrom-Json
$checks = [System.Collections.Generic.List[object]]::new()

function Add-Check([string]$Name, [ValidateSet('pass', 'fail', 'blocked')][string]$Status, [string]$Detail) {
    $checks.Add([pscustomobject]@{ name = $Name; status = $Status; detail = $Detail })
}

function Get-Required([object]$Object, [string]$Path) {
    $current = $Object
    foreach ($part in ($Path -split '\.')) {
        if ($null -eq $current -or $current.PSObject.Properties.Name -notcontains $part) { return $null }
        $current = $current.$part
    }
    return $current
}

function Require-True([string]$Name, [string]$Path) {
    $value = Get-Required $document $Path
    if ($value -eq $true) { Add-Check $Name 'pass' "$Path=true." }
    else { Add-Check $Name 'blocked' "$Path phải là true và cần evidence provider tương ứng." }
}

function Require-Value([string]$Name, [string]$Path, [string]$Expected) {
    $value = [string](Get-Required $document $Path)
    if ($value -eq $Expected) { Add-Check $Name 'pass' "$Path=$Expected." }
    else { Add-Check $Name 'blocked' "$Path phải bằng '$Expected'." }
}

$metadata = Get-Required $document 'metadata'
if ($null -ne $metadata -and
    -not [string]::IsNullOrWhiteSpace([string](Get-Required $document 'metadata.changeTicket')) -and
    -not [string]::IsNullOrWhiteSpace([string](Get-Required $document 'metadata.evidenceBundleUri')) -and
    -not [string]::IsNullOrWhiteSpace([string](Get-Required $document 'metadata.storageOwner')) -and
    -not [string]::IsNullOrWhiteSpace([string](Get-Required $document 'metadata.securityApprover'))) {
    Add-Check 'attestation-metadata' 'pass' 'Change, evidence bundle, owner và independent approver đều được khai báo.'
} else {
    Add-Check 'attestation-metadata' 'blocked' 'Thiếu change ticket, evidence bundle, storage owner hoặc independent security approver.'
}

Require-True 'azure-https-only' 'azure.httpsOnly'
Require-Value 'azure-tls' 'azure.minimumTlsVersion' 'TLS1_2'
Require-True 'azure-private-access' 'azure.privateAccess'
Require-True 'azure-public-blob-disabled' 'azure.allowBlobPublicAccessDisabled'
Require-Value 'azure-cmk' 'azure.keySource' 'Microsoft.Keyvault'
Require-True 'azure-infrastructure-encryption' 'azure.infrastructureEncryption'
Require-True 'azure-immutable-versioning' 'azure.immutableStorageWithVersioningEnabled'
Require-Value 'azure-worm-mode' 'azure.immutabilityPolicyMode' 'Locked'

$retention = Get-Required $document 'azure.retentionDays'
if ($null -ne $retention -and [int]$retention -ge $MinimumRetentionDays) {
    Add-Check 'azure-retention' 'pass' "azure.retentionDays=$retention (minimum=$MinimumRetentionDays)."
} else {
    Add-Check 'azure-retention' 'blocked' "azure.retentionDays phải >= $MinimumRetentionDays."
}

Require-True 'csi-external' 'csi.externalCsi'
Require-True 'csi-encryption-at-rest' 'csi.encryptionAtRest'
Require-True 'csi-kms-binding' 'csi.kmsBinding'
Require-True 'csi-failure-domain' 'csi.failureDomain'
Require-True 'csi-snapshot-restore' 'csi.snapshotRestore'
Require-True 'backup-objectstore-ready' 'backup.objectStoreReady'
Require-True 'backup-restore-verified' 'backup.restoreVerified'
Require-True 'backup-checksum-verified' 'backup.checksumVerified'
Require-True 'backup-rpo-rto-measured' 'backup.rpoRtoMeasured'

$failed = @($checks | Where-Object status -eq 'fail')
$blocked = @($checks | Where-Object status -eq 'blocked')
$status = if ($failed.Count -gt 0) { 'fail' } elseif ($blocked.Count -gt 0) { 'blocked' } else { 'pass' }
$result = [pscustomobject]@{
    schemaVersion = 'production-storage-attestation.v1'
    status = $status
    generatedAtUtc = [DateTime]::UtcNow.ToString('o')
    checks = @($checks)
}
$json = $result | ConvertTo-Json -Depth 8
if ($OutputPath) {
    $directory = Split-Path -Parent $OutputPath
    if ($directory) { New-Item -ItemType Directory -Force -Path $directory | Out-Null }
    [IO.File]::WriteAllText([IO.Path]::GetFullPath($OutputPath), $json, [Text.UTF8Encoding]::new($false))
}
Write-Output $json
if ($status -eq 'fail') { exit 30 }
if ($status -eq 'blocked') { exit 70 }
exit 0
