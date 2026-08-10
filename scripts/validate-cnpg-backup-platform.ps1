[CmdletBinding()]
param(
    [switch]$RunBackup,
    [switch]$RunRestore,
    [string]$Kubeconfig,
    [string]$OutputPath,
    [string]$Namespace = 'spire',
    [string]$ClusterName = 'spire-postgres',
    [string]$BackupNamespace = 'backup',
    [string]$ObjectStoreName = 'spire-postgres-azure-store',
    [string]$ScheduledBackupName = 'spire-postgres-azure-backup'
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

if (-not [string]::IsNullOrWhiteSpace($Kubeconfig)) {
    $resolvedKubeconfig = [IO.Path]::GetFullPath($Kubeconfig)
    if (-not (Test-Path -LiteralPath $resolvedKubeconfig -PathType Leaf)) {
        throw "Kubeconfig does not exist: $resolvedKubeconfig"
    }
    $env:KUBECONFIG = $resolvedKubeconfig
}

function Write-Evidence {
    param(
        [Parameter(Mandatory)][ValidateSet('pass','fail','blocked','unavailable')][string]$Status,
        [Parameter(Mandatory)][string]$Message
    )
    if ([string]::IsNullOrWhiteSpace($OutputPath)) { return }
    $doc = [pscustomobject]@{
        status = $Status
        generatedAtUtc = [DateTime]::UtcNow.ToString('o')
        checks = @([pscustomobject]@{ name = 'cnpg-backup-platform'; status = $Status; detail = $Message })
    }
    $parent = Split-Path -Parent $OutputPath
    if ($parent) { New-Item -ItemType Directory -Force -Path $parent | Out-Null }
    $doc | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $OutputPath -Encoding utf8NoBOM
}

trap {
    $safeMessage = [string]$_.Exception.Message
    $safeMessage = [regex]::Replace($safeMessage, '(?i)(token|password|secret|sas|client[_-]?secret)=\S+', '$1=<redacted>')
    $safeMessage = [regex]::Replace($safeMessage, '(?i)(authorization:\s*bearer\s+)\S+', '$1<redacted>')
    Write-Host "CNPG backup platform contract failed: $safeMessage"
    Write-Evidence -Status 'fail' -Message $safeMessage
    exit 1
}

function K {
    param([Parameter(Mandatory)][string[]]$Args)
    & kubectl @Args
    if ($LASTEXITCODE -ne 0) { throw "kubectl failed: kubectl $($Args -join ' ')" }
}

function KText {
    param([Parameter(Mandatory)][string[]]$Args)
    $value = & kubectl @Args
    if ($LASTEXITCODE -ne 0) { throw "kubectl failed: kubectl $($Args -join ' ')" }
    $value -join "`n"
}

$crd = KText @('get', 'crd', 'objectstores.barmancloud.cnpg.io', '-o', 'name')
if ($crd -notmatch 'objectstores') { throw 'Barman Cloud ObjectStore CRD is not installed.' }

$plugin = KText @('get', 'deploy', 'barman-cloud', '-n', 'cnpg-system', '-o', 'jsonpath={.status.availableReplicas}')
if ($plugin -ne '1') { throw "Barman Cloud plugin is not available (replicas=$plugin)." }

$objectStore = KText @('get', 'objectstore', $ObjectStoreName, '-n', $Namespace, '-o', 'jsonpath={.status.phase}')
if ([string]::IsNullOrWhiteSpace($objectStore) -or $objectStore -notmatch '^ready$') {
    throw "ObjectStore is not ready (phase=$objectStore)."
}

$cluster = KText @('get', 'cluster', $ClusterName, '-n', $Namespace, '-o', 'jsonpath={.status.phase}')
if ($cluster -notmatch 'Cluster in healthy state|healthy|Healthy') {
    throw "CNPG cluster is not healthy (phase=$cluster)."
}

$scheduled = KText @('get', 'scheduledbackup', $ScheduledBackupName, '-n', $Namespace, '-o', 'jsonpath={.spec.method}:{.spec.pluginConfiguration.name}')
if ($scheduled -ne 'plugin:barman-cloud.cloudnative-pg.io') {
    throw "ScheduledBackup is not configured for the Barman Cloud plugin: $scheduled"
}

$archive = KText @('get', 'cluster', $ClusterName, '-n', $Namespace, '-o', 'jsonpath={.status.currentPrimary}')
if ([string]::IsNullOrWhiteSpace($archive)) { throw 'CNPG cluster has no current primary.' }
$configuredObjectStore = KText @('get', 'cluster', $ClusterName, '-n', $Namespace, '-o', 'jsonpath={.spec.plugins[?(@.name=="barman-cloud.cloudnative-pg.io")].parameters.barmanObjectName}')
if ($configuredObjectStore -ne $ObjectStoreName) {
    throw "Cluster is configured with barmanObjectName '$configuredObjectStore', expected '$ObjectStoreName'."
}

if ($RunBackup) {
    $name = "spire-postgres-smoke-$([DateTime]::UtcNow.ToString('yyyyMMddHHmmss'))"
    @"
apiVersion: postgresql.cnpg.io/v1
kind: Backup
metadata:
  name: $name
  namespace: $Namespace
spec:
  cluster:
    name: $ClusterName
  method: plugin
  pluginConfiguration:
    name: barman-cloud.cloudnative-pg.io
"@ | kubectl apply -f - | Out-Null
    if ($LASTEXITCODE -ne 0) { throw "Unable to create backup $name." }

    $deadline = (Get-Date).AddMinutes(5)
    do {
        Start-Sleep -Seconds 5
        $phase = KText @('get', 'backup', $name, '-n', $Namespace, '-o', 'jsonpath={.status.phase}')
        if ($phase -match 'completed|Completed') { break }
        if ($phase -match 'failed|Failed') { throw "Backup $name failed." }
    } while ((Get-Date) -lt $deadline)
    if ($phase -notmatch 'completed|Completed') { throw "Backup $name did not complete before timeout (phase=$phase)." }
    Write-Output "CNPG backup PASS: $name completed through the plugin."
}

if ($RunRestore) {
    throw 'Restore gate requires a dedicated restore namespace and a retention-approved target cluster; no destructive restore was run by default. Supply a reviewed restore manifest and run it explicitly.'
}

Write-Output "CNPG platform PASS: plugin=$plugin, ObjectStore phase='$objectStore', cluster='$cluster', schedule='$scheduled', primary='$archive'."
Write-Evidence -Status 'pass' -Message 'CNPG plugin, ObjectStore, cluster, scheduled backup and current primary checks passed.'
