[CmdletBinding()]
param(
    [string]$BackupNamespace = 'backup',
    [string]$DatabaseNamespace = 'spire',
    [string]$CredentialsSecret = 'minio-credentials',
    [string]$DatabaseCredentialsSecret = 'spire-postgres-backup-credentials'
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

function Invoke-Kubectl {
    param([Parameter(Mandatory)][string[]]$Arguments)
    & kubectl @Arguments
    if ($LASTEXITCODE -ne 0) { throw "kubectl failed: kubectl $($Arguments -join ' ')" }
}

function Get-SecretValue {
    param([string]$Namespace, [string]$Name, [string]$Key)
    $encoded = (& kubectl -n $Namespace get secret $Name -o "jsonpath={.data.$Key}" 2>$null)
    if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($encoded)) { return $null }
    [Text.Encoding]::UTF8.GetString([Convert]::FromBase64String($encoded.Trim()))
}

function New-RandomSecret {
    $bytes = New-Object byte[] 32
    [Security.Cryptography.RandomNumberGenerator]::Fill($bytes)
    [Convert]::ToBase64String($bytes).TrimEnd('=')
}

$namespaceYaml = & kubectl create namespace $BackupNamespace --dry-run=client -o yaml
if ($LASTEXITCODE -ne 0) { throw "Unable to render namespace $BackupNamespace." }
$namespaceYaml | kubectl apply -f - | Out-Null

$accessKey = Get-SecretValue $BackupNamespace $CredentialsSecret 'ACCESS_KEY_ID'
$secretKey = Get-SecretValue $BackupNamespace $CredentialsSecret 'ACCESS_SECRET_KEY'
$rootUser = Get-SecretValue $BackupNamespace $CredentialsSecret 'MINIO_ROOT_USER'
$rootPassword = Get-SecretValue $BackupNamespace $CredentialsSecret 'MINIO_ROOT_PASSWORD'

if ([string]::IsNullOrWhiteSpace($accessKey)) {
    $accessKey = 'hisHopeBackup'
    $secretKey = New-RandomSecret
    $rootUser = $accessKey
    $rootPassword = $secretKey
    $secretYaml = & kubectl create secret generic $CredentialsSecret -n $BackupNamespace `
        --from-literal=ACCESS_KEY_ID=$accessKey `
        --from-literal=ACCESS_SECRET_KEY=$secretKey `
        --from-literal=MINIO_ROOT_USER=$rootUser `
        --from-literal=MINIO_ROOT_PASSWORD=$rootPassword `
        --dry-run=client -o yaml
    if ($LASTEXITCODE -ne 0) { throw 'Unable to render MinIO credentials secret.' }
    $secretYaml | kubectl apply -f - | Out-Null
}

$dbSecretYaml = & kubectl create secret generic $DatabaseCredentialsSecret -n $DatabaseNamespace `
    --from-literal=ACCESS_KEY_ID=$accessKey `
    --from-literal=ACCESS_SECRET_KEY=$secretKey `
    --dry-run=client -o yaml
if ($LASTEXITCODE -ne 0) { throw 'Unable to render CNPG object-store credentials secret.' }
$dbSecretYaml | kubectl apply -f - | Out-Null

Invoke-Kubectl @('apply', '-f', 'k8s/production-ha/backup-object-store.yaml')
Invoke-Kubectl @('apply', '-f', 'k8s/production-ha/cnpg-barman-object-store.yaml')
Invoke-Kubectl @('rollout', 'status', "statefulset/minio", '-n', $BackupNamespace, '--timeout=180s')
Invoke-Kubectl @('wait', '--for=condition=complete', 'job/minio-backup-bucket', '-n', $BackupNamespace, '--timeout=180s')

Write-Output 'CNPG object-store bootstrap PASS: credentials injected into Kubernetes secrets, MinIO is ready, bucket is versioned, and ObjectStore/ScheduledBackup are applied.'
