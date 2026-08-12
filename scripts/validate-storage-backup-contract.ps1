[CmdletBinding()]
param(
    [string]$RepositoryRoot = (Join-Path $PSScriptRoot '..'),
    [string]$OutputPath,
    [string]$SecureEnvFile,
    [switch]$RequireSecureEnv,
    [switch]$StaticOnly,
    [ValidatePattern('^[a-z0-9]([-a-z0-9]*[a-z0-9])?$')]
    [string]$StorageClassName = 'viettel-shared',
    [string]$EvidenceDirectory = 'artifacts/evidence'
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$root = [IO.Path]::GetFullPath($RepositoryRoot)
$results = [System.Collections.Generic.List[object]]::new()

function Add-Gate {
    param(
        [Parameter(Mandatory)][string]$Name,
        [Parameter(Mandatory)][ValidateSet('pass','blocked','skipped','fail')][string]$Status,
        [Parameter(Mandatory)][string]$Detail
    )
    $results.Add([ordered]@{ name = $Name; status = $Status; detail = $Detail })
}

function Read-RepoFile {
    param([Parameter(Mandatory)][string]$RelativePath)
    $path = Join-Path $root $RelativePath
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { return $null }
    return Get-Content -Raw -LiteralPath $path
}

function Read-SecureEnvKeys {
    param([Parameter(Mandatory)][string]$Path)
    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        return $null
    }
    $keys = @{}
    foreach ($line in Get-Content -LiteralPath $Path) {
        if ($line -match '^\s*(?:export\s+)?(?<key>[A-Z][A-Z0-9_]*)\s*=\s*(?<value>.*)\s*$') {
            $value = $Matches.value.Trim()
            if (($value.StartsWith('"') -and $value.EndsWith('"')) -or
                ($value.StartsWith("'") -and $value.EndsWith("'"))) {
                $value = $value.Substring(1, $value.Length - 2)
            }
            $keys[$Matches.key] = $value
        }
    }
    return $keys
}

$productionHa = Read-RepoFile 'k8s/production-ha/kustomization.yaml'
$azureObjectStore = Read-RepoFile 'k8s/production-ha/cnpg-barman-object-store-azure.yaml'
$cluster = Read-RepoFile 'k8s/production-ha/spire-postgres-cluster.yaml'
$clusterPatch = Read-RepoFile 'k8s/production-ha/spire-postgres-cluster-azure-patch.yaml'
$minio = Read-RepoFile 'k8s/production-ha/backup-object-store.yaml'
$velero = Read-RepoFile 'k8s/backup/velero-azure-values.yaml'
$longhornBootstrap = Read-RepoFile 'scripts/bootstrap-longhorn-storage.ps1'
$pvcMigration = Read-RepoFile 'scripts/migrate-database-continuity-pvc.ps1'
$sharedDataOverlay = Read-RepoFile 'k8s/overlays/prod-spire-azure-shared-storage/kustomization.yaml'
$sharedObservabilityOverlay = Read-RepoFile 'k8s/observability/overlays/prod-shared-storage/kustomization.yaml'
$prodStorageFiles = @(Get-ChildItem -LiteralPath (Join-Path $root 'k8s/overlays/prod') -File -ErrorAction SilentlyContinue |
    Where-Object { $_.Name -match 'storage|pvc|volume' })

$requiredAzureKeys = @(
    'AZURE_STORAGE_ACCOUNT',
    'AZURE_STORAGE_CONTAINER',
    'AZURE_STORAGE_ENDPOINT',
    'AZURE_STORAGE_SAS_TOKEN'
)
if ($StaticOnly) {
    Add-Gate 'secure-azure-source' 'skipped' 'Runtime Azure values are validated by the protected production workflow; static CI does not read secrets.'
} elseif ($SecureEnvFile) {
    $secureKeys = Read-SecureEnvKeys -Path ([IO.Path]::GetFullPath($SecureEnvFile))
    if ($null -eq $secureKeys) {
        Add-Gate 'secure-azure-source' 'blocked' 'Secure Azure env file is missing; no values were read or printed'
    } else {
        $missingKeys = @($requiredAzureKeys | Where-Object {
                -not $secureKeys.ContainsKey($_) -or [string]::IsNullOrWhiteSpace([string]$secureKeys[$_]) -or
                [string]$secureKeys[$_] -match 'REPLACE_ME|REPLACE_WITH|<[^>]+>'
            })
        if ($missingKeys.Count -eq 0) {
            Add-Gate 'secure-azure-source' 'pass' "Secure Azure env file contains the required key set ($($requiredAzureKeys.Count) keys); values are intentionally redacted"
        } else {
            Add-Gate 'secure-azure-source' 'blocked' "Secure Azure env file is incomplete or contains placeholders for: $($missingKeys -join ', ')"
        }
    }
} elseif ($RequireSecureEnv) {
    Add-Gate 'secure-azure-source' 'blocked' 'A secure Azure env file is required; pass -SecureEnvFile without exposing its values'
}

if ($productionHa -and $productionHa -match 'cnpg-barman-object-store-azure\.yaml') {
    Add-Gate 'cnpg-manifest-set' 'pass' 'production-ha includes the Azure ObjectStore and ScheduledBackup manifests'
} else {
    Add-Gate 'cnpg-manifest-set' 'fail' 'production-ha does not include the Azure CNPG backup manifest'
}

if ($azureObjectStore -and $azureObjectStore -match 'kind:\s+ObjectStore' -and $azureObjectStore -match 'kind:\s+ScheduledBackup') {
    Add-Gate 'cnpg-schedule-contract' 'pass' 'Azure ObjectStore and six-hour ScheduledBackup are declared'
} else {
    Add-Gate 'cnpg-schedule-contract' 'fail' 'Azure ObjectStore/ScheduledBackup declaration is incomplete'
}

if ($azureObjectStore -and $azureObjectStore -notmatch 'REPLACE_ME|REPLACE_WITH') {
    Add-Gate 'cnpg-destination-configured' 'pass' 'Azure destination is configured without repository placeholders'
} elseif ($StaticOnly) {
    Add-Gate 'cnpg-destination-configured' 'skipped' 'Azure destination values are injected from the protected runtime secret source.'
} else {
    Add-Gate 'cnpg-destination-configured' 'blocked' 'Azure destination still contains a placeholder; inject runtime values from the secure secret source'
}

$configuredObjectStore = if ($clusterPatch -and $clusterPatch -match 'barmanObjectName:\s*([^\s\r\n]+)') { $Matches[1] } else { $null }
$azureObjectStoreName = if ($azureObjectStore -and $azureObjectStore -match 'kind:\s+ObjectStore[\s\S]*?metadata:\s*[\s\S]*?name:\s*([^\s\r\n]+)') { $Matches[1] } else { $null }
if ($configuredObjectStore -and $azureObjectStoreName -and $configuredObjectStore -eq $azureObjectStoreName) {
    Add-Gate 'cnpg-objectstore-reference' 'pass' "CNPG production patch references '$configuredObjectStore'"
} else {
    Add-Gate 'cnpg-objectstore-reference' 'fail' "CNPG patch/object store names do not match (patch='$configuredObjectStore', objectStore='$azureObjectStoreName')"
}

if ($sharedDataOverlay -and $sharedDataOverlay -match "value:\s*$([regex]::Escape($StorageClassName))") {
    Add-Gate 'replicated-storage' 'skipped' "Backup PVCs are pinned to external CSI '$StorageClassName'; runtime provisioner and restore evidence are required before declaring durable replication"
} elseif ($minio -and $minio -match 'storageClassName:\s*local-path') {
    Add-Gate 'replicated-storage' 'blocked' 'Backup MinIO PVCs use local-path; production requires a replicated CSI class before this is a durable backup target'
} elseif ($minio) {
    Add-Gate 'replicated-storage' 'pass' 'Backup object-store PVCs use a non-local storage class'
} else {
    Add-Gate 'replicated-storage' 'fail' 'Backup object-store manifest is missing'
}

$localProdStorage = @($prodStorageFiles | Select-String -Pattern 'storageClassName:\s*local-path' -List)
$prodKustomization = Read-RepoFile 'k8s/overlays/prod-spire-azure-shared-storage/kustomization.yaml'
if ($prodKustomization -and $prodKustomization -match "value:\s*$([regex]::Escape($StorageClassName))") {
    Add-Gate 'production-pvc-storage' 'skipped' "Production data overlay pins stateful claims to external CSI '$StorageClassName'; protected runtime migration and restore evidence are still required"
} elseif ($localProdStorage.Count -gt 0) {
    Add-Gate 'production-pvc-storage' 'blocked' 'Production overlay still selects local-path for stateful PVCs; replace it only after the shared CSI class and restore drill exist'
} else {
    Add-Gate 'production-pvc-storage' 'blocked' 'Production shared-CSI overlay is missing; no durable stateful storage intent is active'
}

$observabilityProd = $sharedObservabilityOverlay
if ($observabilityProd -and $observabilityProd -match "value:\s*$([regex]::Escape($StorageClassName))") {
    Add-Gate 'observability-pvc-storage' 'skipped' "Production observability overlay pins stateful claims to external CSI '$StorageClassName'; runtime class and restore evidence are required"
} else {
    Add-Gate 'observability-pvc-storage' 'blocked' 'Production observability shared-CSI overlay must pin stateful PVCs to the approved external CSI class'
}

if ($minio -and $minio -match '(?m)^\s*image:\s*[^\r\n]*:latest(?:@|\s|$)') {
    Add-Gate 'backup-image-integrity' 'fail' 'Backup manifest contains a mutable :latest image tag'
} elseif ($minio -and $minio -notmatch '(?m)^\s*image:\s*[^\r\n]+@sha256:[0-9a-f]{64}') {
    Add-Gate 'backup-image-integrity' 'fail' 'Backup manifest has an image without a sha256 digest'
} else {
    Add-Gate 'backup-image-integrity' 'pass' 'Backup images are digest pinned'
}

if ($velero -and $velero -notmatch 'REPLACE_WITH|REPLACE_ME') {
    Add-Gate 'velero-azure-contract' 'pass' 'Velero Azure provider values contain no placeholders'
} elseif ($StaticOnly) {
    Add-Gate 'velero-azure-contract' 'skipped' 'Velero Azure values are injected from the protected runtime secret source.'
} else {
    Add-Gate 'velero-azure-contract' 'blocked' 'Velero Azure values are a template; production credentials and account identifiers must be injected out-of-band'
}

if ($sharedDataOverlay -and $sharedObservabilityOverlay -and
    $sharedDataOverlay -match "value:\s*$([regex]::Escape($StorageClassName))" -and
    $sharedObservabilityOverlay -match "value:\s*$([regex]::Escape($StorageClassName))") {
    Add-Gate 'replicated-storage-bootstrap' 'skipped' "External CSI '$StorageClassName' bootstrap is platform-owned; runtime provisioner, approval and restore gates are required"
} elseif ($longhornBootstrap -and $longhornBootstrap -match "Version = '1\.12\.0'" -and
    $longhornBootstrap -match 'defaultClassReplicaCount=3' -and
    $longhornBootstrap -match 'VolumeSnapshotClass' -and
    $longhornBootstrap -match 'longhorn-data-ready' -and
    $longhornBootstrap -match 'AllowProduction') {
    Add-Gate 'replicated-storage-bootstrap' 'pass' 'Longhorn bootstrap is version-pinned, three-replica configured, production-protected and requires a per-node dedicated-data readiness label; runtime installation remains separately required'
} else {
    Add-Gate 'replicated-storage-bootstrap' 'fail' 'External CSI/Longhorn bootstrap contract is missing pinning, replica policy or production protection'
}

if ($pvcMigration -and $pvcMigration -match 'AllowProduction' -and
    $pvcMigration -match 'storageClassName: \$StorageClass' -and
    $pvcMigration -match '\.migration-complete' -and
    $pvcMigration -match 'Rollback: keep') {
    Add-Gate 'pvc-migration-tooling' 'pass' 'Database-continuity PVC migration is explicit, dry-run by default, checksum-marked and preserves rollback PVC.'
} else {
    Add-Gate 'pvc-migration-tooling' 'fail' 'Database-continuity PVC migration tooling is missing production guard, copy verification or rollback preservation.'
}

$longhornEvidencePath = Join-Path $root (Join-Path $EvidenceDirectory 'longhorn-snapshot-restore.json')
if ($StaticOnly) {
    Add-Gate 'csi-restore-drill' 'skipped' 'CSI restore evidence is a protected runtime gate, not a static repository check.'
} elseif (Test-Path -LiteralPath $longhornEvidencePath -PathType Leaf) {
    try {
        $longhornEvidence = Get-Content -LiteralPath $longhornEvidencePath -Raw | ConvertFrom-Json
        if ($longhornEvidence.status -eq 'pass' -and $longhornEvidence.restoreVerified -eq $true -and
            $null -ne $longhornEvidence.rtoMinutes -and $null -ne $longhornEvidence.executedAtUtc) {
            Add-Gate 'csi-restore-drill' 'pass' 'Longhorn isolated snapshot/restore checksum drill has measured evidence'
        } else {
            Add-Gate 'csi-restore-drill' 'blocked' 'Longhorn restore evidence exists but is not a verified measured pass'
        }
    } catch {
        Add-Gate 'csi-restore-drill' 'blocked' 'Longhorn restore evidence is not valid JSON'
    }
} else {
    Add-Gate 'csi-restore-drill' 'blocked' 'Longhorn isolated snapshot/restore evidence is missing'
}

$snapshotFiles = @(Get-ChildItem -LiteralPath (Join-Path $root 'k8s') -Recurse -File -ErrorAction SilentlyContinue |
    Select-String -Pattern '^kind:\s*VolumeSnapshotClass\s*$' -List)
if ($snapshotFiles.Count -gt 0) {
    Add-Gate 'volume-snapshot-class' 'pass' 'A VolumeSnapshotClass manifest exists'
} elseif ($sharedDataOverlay -and $sharedDataOverlay -match "value:\s*$([regex]::Escape($StorageClassName))") {
    Add-Gate 'volume-snapshot-class' 'skipped' "VolumeSnapshotClass for external CSI '$StorageClassName' is platform-owned; protected runtime validation must prove driver matching"
} else {
    Add-Gate 'volume-snapshot-class' 'blocked' 'No VolumeSnapshotClass is committed; CSI snapshot/restore cannot be proven'
}

$restore = Read-RepoFile 'scripts/restore-postgres.ps1'
$continuity = Read-RepoFile 'scripts/database-continuity-executor.sh'
if ($restore -and $restore -match 'ConfirmRestore' -and $continuity -and $continuity -match 'isolated') {
    Add-Gate 'restore-safety' 'pass' 'Restore tooling requires explicit confirmation and restricts continuity restore to isolated targets'
} else {
    Add-Gate 'restore-safety' 'fail' 'Restore tooling does not provide both explicit confirmation and isolated-target protection'
}

$summary = [ordered]@{
    generatedAtUtc = [DateTime]::UtcNow.ToString('o')
    repositoryRoot = $root
    gates = @($results)
}

if ($OutputPath) {
    $resolved = [IO.Path]::GetFullPath($OutputPath)
    New-Item -ItemType Directory -Force -Path (Split-Path -Parent $resolved) | Out-Null
    $summary | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $resolved -Encoding utf8
}

$results | ForEach-Object { '{0}: {1} - {2}' -f $_.name, $_.status.ToUpperInvariant(), $_.detail }
if (@($results | Where-Object status -eq 'fail').Count -gt 0) { exit 30 }
if (@($results | Where-Object status -eq 'blocked').Count -gt 0) { exit 70 }
exit 0
