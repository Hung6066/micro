[CmdletBinding()]
param(
    [string]$RepositoryRoot,
    [string]$ProductionOverlay = 'k8s/overlays/prod-shared-storage',
    [string]$OutputPath,
    [string]$AttestationPath,
    [ValidatePattern('^[A-Fa-f0-9]{64}$')][string]$ExpectedAttestationSha256,
    [switch]$AllowEnvironmentBlocked
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
$cnpgObjectStore = Read-Required 'k8s/production-ha/cnpg-barman-object-store.yaml'
$attestation = $null
$attestationLoaded = $false
$attestationIntegrityPassed = $true

if (-not [string]::IsNullOrWhiteSpace($AttestationPath)) {
    if (-not (Test-Path -LiteralPath $AttestationPath -PathType Leaf)) {
        Add-Check 'production-storage-attestation' 'fail' 'The supplied production storage attestation file does not exist.'
    } else {
        try {
            if (-not [string]::IsNullOrWhiteSpace($ExpectedAttestationSha256)) {
                $hashAlgorithm = [Security.Cryptography.SHA256]::Create()
                try {
                    $actualHash = [BitConverter]::ToString($hashAlgorithm.ComputeHash([IO.File]::ReadAllBytes($AttestationPath))).Replace('-', '')
                } finally {
                    $hashAlgorithm.Dispose()
                }
                if ($actualHash -ine $ExpectedAttestationSha256) {
                    $attestationIntegrityPassed = $false
                    Add-Check 'production-storage-attestation' 'fail' 'Storage attestation SHA-256 does not match the protected expected digest.'
                }
            }
            $attestation = Get-Content -LiteralPath $AttestationPath -Raw | ConvertFrom-Json
            $attestationLoaded = $true
            if ([string]$attestation.schemaVersion -ne 'production-storage-attestation.v1') {
                $attestationIntegrityPassed = $false
                Add-Check 'production-storage-attestation' 'fail' 'Storage attestation schema version is unsupported.'
            } elseif ([string]$attestation.status -ne 'pass') {
                $attestationIntegrityPassed = $false
                Add-Check 'production-storage-attestation' 'blocked' 'Storage attestation exists but is not in pass status.'
            } else {
                Add-Check 'production-storage-attestation' 'pass' 'Protected storage attestation is present and reports pass.'
            }
        } catch {
            $attestationIntegrityPassed = $false
            Add-Check 'production-storage-attestation' 'fail' 'Storage attestation is not valid JSON.'
        }
    }
}

$attestationPassed = $attestationLoaded -and
    $attestationIntegrityPassed -and
    [string]$attestation.schemaVersion -eq 'production-storage-attestation.v1' -and
    [string]$attestation.status -eq 'pass' -and
    @($attestation.checks).Count -gt 0 -and
    @($attestation.checks | Where-Object { [string]$_.status -ne 'pass' }).Count -eq 0 -and
    (@('csi-encryption-at-rest', 'csi-kms-binding', 'csi-failure-domain', 'csi-snapshot-restore', 'azure-cmk', 'azure-private-access', 'azure-worm-mode', 'backup-restore-verified') | Where-Object { $attestation.checks.name -notcontains $_ }).Count -eq 0

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
    if ($attestationPassed -and $productionStorageOverlay -match 'value:\s*viettel-shared') {
        Add-Check 'production-encrypted-storage' 'pass' 'External CSI encryption-at-rest, KMS binding and failure-domain evidence is covered by the protected storage attestation.'
    } elseif ($productionStorageOverlay -match 'value:\s*viettel-shared') {
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
    if ($attestationPassed -and $azureObjectStore -match 'REPLACE_ME') {
        Add-Check 'azure-backup-destination' 'pass' 'Protected storage attestation covers the production Azure destination; repository template remains placeholder-only by design.'
    } elseif ($azureObjectStore -match 'REPLACE_ME') {
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

    $networkPolicyRestricted = $backupStore -match 'policyTypes:\s*\[Ingress,\s*Egress\]' -and
        $backupStore -match 'app\.kubernetes\.io/name:\s*minio-backup-bucket' -and
        $backupStore -match 'cnpg\.io/cluster:\s*spire-postgres'
    if ($networkPolicyRestricted) {
        Add-Check 'backup-network-policy' 'pass' 'MinIO ingress/egress is restricted to the backup job, MinIO peers and CNPG cluster.'
    } else {
        Add-Check 'backup-network-policy' 'blocked' 'MinIO network policy must restrict both directions to backup workloads and DNS.'
    }
}

if ($cnpgObjectStore) {
    $cnpgTlsConfigured = $cnpgObjectStore -match 'endpointURL:\s*https://' -and
        $cnpgObjectStore -match 'endpointCA:' -and
        $cnpgObjectStore -match 'name:\s*minio-tls' -and
        $cnpgObjectStore -match 'key:\s*ca\.crt'
    if ($cnpgTlsConfigured) {
        Add-Check 'cnpg-backup-object-store-tls' 'pass' 'CNPG backup ObjectStore uses HTTPS and the MinIO private CA.'
    } else {
        Add-Check 'cnpg-backup-object-store-tls' 'blocked' 'CNPG backup ObjectStore must use HTTPS with endpointCA=minio-tls/ca.crt.'
    }
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
if ($AllowEnvironmentBlocked -and $status -eq 'blocked') { exit 0 }
if ($status -ne 'pass') { exit 60 }
exit 0
