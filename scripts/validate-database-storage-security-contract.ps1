[CmdletBinding()]
param(
    [string]$RepositoryRoot,
    [string]$ProductionOverlay = 'k8s/overlays/prod-shared-storage',
    [string]$OutputPath
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

if ([string]::IsNullOrWhiteSpace($RepositoryRoot)) {
    $RepositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
}
$root = (Resolve-Path $RepositoryRoot).Path
$checks = [System.Collections.Generic.List[object]]::new()

function Add-Check([string]$Name, [ValidateSet('pass','fail','blocked')][string]$Status, [string]$Detail) {
    $checks.Add([pscustomobject]@{ name = $Name; status = $Status; detail = $Detail })
}

function Read-Required([string]$RelativePath) {
    $path = Join-Path $root $RelativePath
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
        Add-Check "file:$RelativePath" 'fail' 'Required production contract file is missing.'
        return $null
    }
    return Get-Content -LiteralPath $path -Raw
}

$secretProvider = Read-Required 'k8s/overlays/prod/runtime-secret-provider-class.yaml'
$workloadIdentity = Read-Required 'k8s/overlays/prod/workload-spiffe-patches.yaml'
$networkPolicy = Read-Required 'k8s/overlays/prod/runtime-dependency-network-policy.yaml'
$productionStorageOverlay = Read-Required (Join-Path $ProductionOverlay 'kustomization.yaml')
$azureObjectStore = Read-Required 'k8s/production-ha/cnpg-barman-object-store-azure.yaml'
$backupStore = Read-Required 'k8s/production-ha/backup-object-store.yaml'

if ($secretProvider) {
    $valid = $secretProvider -match 'provider:\s*vault' -and
        $secretProvider -match 'vaultSkipTLSVerify:\s*"false"' -and
        $secretProvider -match 'secret/data/his-hope/production/postgres' -and
        $secretProvider -match 'SECRET_POSTGRES_PASSWORD'
    if ($valid) { Add-Check 'vault-production-secret-provider' 'pass' 'Production database secret is sourced from Vault with TLS verification.' }
    else { Add-Check 'vault-production-secret-provider' 'fail' 'Vault production secret provider is incomplete or permits unverified TLS.' }
}

if ($workloadIdentity) {
    $valid = $workloadIdentity -match 'Vault__RequireVault\s*\r?\n\s*value:\s*"true"' -and
        $workloadIdentity -match 'Vault__AllowStaticToken\s*\r?\n\s*value:\s*"false"'
    if ($valid) { Add-Check 'workload-identity-no-static-token' 'pass' 'Production requires Vault and forbids static Vault tokens.' }
    else { Add-Check 'workload-identity-no-static-token' 'fail' 'Production workload identity does not fail closed against static Vault tokens.' }
}

if ($networkPolicy) {
    if ($networkPolicy -match 'kind:\s*NetworkPolicy' -and $networkPolicy -match 'policyTypes:') {
        Add-Check 'runtime-network-policy' 'pass' 'Production runtime dependency network policies are declared.'
    } else { Add-Check 'runtime-network-policy' 'fail' 'Production runtime dependency network policy is incomplete.' }
}

if ($productionStorageOverlay) {
    if ($productionStorageOverlay -match 'value:\s*viettel-shared') {
        Add-Check 'production-encrypted-storage' 'blocked' 'The selected external CSI class viettel-shared is platform-owned; encryption-at-rest, KMS binding and failure-domain evidence are still required.'
    } elseif ($productionStorageOverlay -match 'local-path') {
        Add-Check 'production-encrypted-storage' 'blocked' 'local-path does not prove provider encryption-at-rest, KMS binding, or durable failure-domain isolation.'
    } else {
        Add-Check 'production-encrypted-storage' 'blocked' 'Selected production StorageClass has no repository/provider attestation for encryption-at-rest and failure-domain isolation.'
    }
}

if ($azureObjectStore) {
    $azureContract = $azureObjectStore -match 'destinationPath:\s*https://' -and
        $azureObjectStore -match 'azureCredentials:' -and
        $azureObjectStore -match 'storageAccount:' -and
        $azureObjectStore -match 'storageSasToken:' -and
        $azureObjectStore -match 'retentionPolicy:\s*30d'
    if ($azureObjectStore -match 'REPLACE_ME') {
        Add-Check 'azure-backup-destination' 'blocked' 'Azure backup destination is still a placeholder; provider, account, private endpoint and retention cannot be verified.'
    } elseif (-not $azureContract) {
        Add-Check 'azure-backup-destination' 'fail' 'Azure backup ObjectStore must use HTTPS, Azure credentials and a 30-day retention policy.'
    } else {
        Add-Check 'azure-backup-destination' 'pass' 'Azure backup destination and retention are configured.'
    }
}

if ($backupStore) {
    $tlsConfigured = $backupStore -match 'https://minio-' -and
        $backupStore -match 'scheme:\s*HTTPS' -and
        $backupStore -match 'secretName:\s*minio-tls'
    if (-not $tlsConfigured) {
        Add-Check 'backup-object-store-tls' 'blocked' 'MinIO backup transport must use HTTPS probes/endpoints and a required private TLS Secret.'
    } else { Add-Check 'backup-object-store-tls' 'pass' 'Backup object-store transport uses HTTPS and a required private TLS Secret.' }
    if ($backupStore -notmatch 'mc mb --with-lock' -or $backupStore -notmatch 'mc retention set --default COMPLIANCE') {
        Add-Check 'backup-object-lock' 'blocked' 'Backup bucket enables versioning but has no repository evidence for object lock/WORM retention.'
    } else { Add-Check 'backup-object-lock' 'pass' 'Backup object-lock/WORM contract is present.' }
}

$failed = @($checks | Where-Object status -eq 'fail')
$blocked = @($checks | Where-Object status -eq 'blocked')
$status = if ($failed.Count -gt 0) { 'fail' } elseif ($blocked.Count -gt 0) { 'blocked' } else { 'pass' }
$result = [pscustomobject]@{
    status = $status
    checks = @($checks)
    generatedAtUtc = [DateTime]::UtcNow.ToString('o')
}
$json = $result | ConvertTo-Json -Depth 8
if ($OutputPath) {
    $dir = Split-Path -Parent $OutputPath
    if ($dir) { New-Item -ItemType Directory -Force -Path $dir | Out-Null }
    [IO.File]::WriteAllText([IO.Path]::GetFullPath($OutputPath), $json, [Text.UTF8Encoding]::new($false))
}
Write-Output $json
if ($status -ne 'pass') { exit 60 }
exit 0
