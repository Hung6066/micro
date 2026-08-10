[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$Kubeconfig,
    [ValidatePattern('^[a-z0-9]([-a-z0-9]*[a-z0-9])?$')]
    [string]$StorageClassName = 'viettel-shared',
    [string]$OutputPath,
    [switch]$RequireSnapshotClass,
    [switch]$RequireApprovalAnnotation
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

if (-not (Test-Path -LiteralPath $Kubeconfig -PathType Leaf)) {
    throw "Kubeconfig not found: $Kubeconfig"
}
$env:KUBECONFIG = (Resolve-Path -LiteralPath $Kubeconfig).Path

$checks = [System.Collections.Generic.List[object]]::new()
function Add-Check([string]$Name, [ValidateSet('pass', 'fail', 'blocked')][string]$Status, [string]$Detail) {
    $checks.Add([pscustomobject]@{ name = $Name; status = $Status; detail = $Detail })
}

try {
    $storageClasses = @(kubectl get storageclass -o json --request-timeout=15s | ConvertFrom-Json).items
    if ($LASTEXITCODE -ne 0) { throw 'Unable to inspect StorageClass resources.' }
    $storageClass = $storageClasses | Where-Object { $_.metadata.name -eq $StorageClassName } | Select-Object -First 1
    if ($null -eq $storageClass) {
        Add-Check 'storage-class' 'blocked' "Shared StorageClass '$StorageClassName' is not installed. Obtain the Viettel vSAN/NFS CSI class before rollout."
    } else {
        $provisioner = [string]$storageClass.provisioner
        if ($provisioner -match '^csi\.' -and $provisioner -notmatch '(?i)(local-path|longhorn)') {
            Add-Check 'storage-class' 'pass' "StorageClass '$StorageClassName' uses external CSI provisioner '$provisioner'."
        } else {
            Add-Check 'storage-class' 'fail' "StorageClass '$StorageClassName' must use a non-node-local CSI provisioner; found '$provisioner'."
        }

        if ($storageClass.allowVolumeExpansion -eq $true) {
            Add-Check 'volume-expansion' 'pass' "StorageClass '$StorageClassName' allows volume expansion."
        } else {
            Add-Check 'volume-expansion' 'fail' "StorageClass '$StorageClassName' must allow volume expansion."
        }

        $bindingMode = [string]$storageClass.volumeBindingMode
        if ($bindingMode -in @('WaitForFirstConsumer', 'Immediate')) {
            Add-Check 'binding-mode' 'pass' "StorageClass '$StorageClassName' uses supported binding mode '$bindingMode'."
        } else {
            Add-Check 'binding-mode' 'fail' "StorageClass '$StorageClassName' has unsupported binding mode '$bindingMode'."
        }

        if ($RequireApprovalAnnotation) {
            $approved = $false
            if ($null -ne $storageClass.metadata.annotations -and
                @($storageClass.metadata.annotations.PSObject.Properties.Name) -contains 'his-hope.io/approved-shared-storage') {
                $approved = [string]$storageClass.metadata.annotations.'his-hope.io/approved-shared-storage' -eq 'true'
            }
            if ($approved) { Add-Check 'owner-approval' 'pass' 'Shared storage class carries the reviewed approval annotation.' }
            else { Add-Check 'owner-approval' 'fail' 'Shared storage class is missing his-hope.io/approved-shared-storage=true.' }
        }

        if ($RequireSnapshotClass) {
            $snapshotClasses = @(kubectl get volumesnapshotclass.snapshot.storage.k8s.io -o json --request-timeout=15s | ConvertFrom-Json).items
            if ($LASTEXITCODE -ne 0) { throw 'Unable to inspect VolumeSnapshotClass resources.' }
            $matching = @($snapshotClasses | Where-Object { [string]$_.driver -eq $provisioner })
            if ($matching.Count -gt 0) {
                Add-Check 'snapshot-class' 'pass' "VolumeSnapshotClass exists for CSI driver '$provisioner'."
            } else {
                Add-Check 'snapshot-class' 'fail' "No VolumeSnapshotClass matches CSI driver '$provisioner'."
            }
        }
    }
} catch {
    Add-Check 'cluster-query' 'blocked' $_.Exception.Message
}

$failed = @($checks | Where-Object status -eq 'fail')
$blocked = @($checks | Where-Object status -eq 'blocked')
$status = if ($failed.Count -gt 0) { 'fail' } elseif ($blocked.Count -gt 0) { 'blocked' } else { 'pass' }
$result = [pscustomobject]@{
    status = $status
    storageClass = $StorageClassName
    checks = @($checks)
    generatedAtUtc = [DateTime]::UtcNow.ToString('o')
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
