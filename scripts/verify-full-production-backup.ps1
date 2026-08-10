[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$Context,
    [string]$Namespace = 'spire',
    [string]$ClusterName = 'spire-postgres',
    [string]$ObjectStoreName = 'spire-postgres-azure-store',
    [string]$ScheduledBackupName = 'spire-postgres-azure-backup'
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

function KText([string[]]$Args) {
    $v = & kubectl --context $Context @Args 2>$null
    if ($LASTEXITCODE -ne 0) { return $null }
    return ($v -join "`n")
}

$results = [ordered]@{}
$current = (& kubectl config current-context 2>$null).Trim()
$results.Context = if ($current -eq $Context) { 'PASS' } else { 'FAIL' }
$results.ApiReady = if ((KText @('get','--raw=/readyz')) -eq 'ok') { 'PASS' } else { 'BLOCKED' }
$results.ObjectStore = if ((KText @('get','objectstore',$ObjectStoreName,'-n',$Namespace,'-o','jsonpath={.status.phase}')) -match 'Ready') { 'PASS' } else { 'FAIL' }
$results.Cluster = if ((KText @('get','cluster',$ClusterName,'-n',$Namespace,'-o','jsonpath={.status.phase}')) -match 'healthy|Healthy') { 'PASS' } else { 'FAIL' }
$results.ScheduledBackup = if (KText @('get','scheduledbackup',$ScheduledBackupName,'-n',$Namespace,'-o','name')) { 'PASS' } else { 'FAIL' }
$results.EtcdSnapshotEvidence = 'SKIPPED: requires host-side snapshot inventory'
$results.VaultRestoreEvidence = 'SKIPPED: requires isolated Vault restore'
$results.PvcRestoreEvidence = 'BLOCKED: local-path migration/CSI restore required'
$results.HarborRestoreEvidence = 'SKIPPED: requires Harbor topology-specific archive'
$results.RedisRestoreEvidence = 'SKIPPED: requires isolated Redis restore'

$results.GetEnumerator() | ForEach-Object { '{0}: {1}' -f $_.Key, $_.Value }
if ($results.Values -contains 'FAIL') { exit 1 }
