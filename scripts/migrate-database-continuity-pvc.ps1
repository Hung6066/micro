[CmdletBinding(SupportsShouldProcess)]
param(
    [string]$Kubeconfig = 'D:\AI\micro\artifacts\kubeconfig-production.yaml',
    [string]$Namespace = 'his-hope',
    [string]$Deployment = 'his-hope-database-continuity',
    [string]$SourcePvc = 'his-hope-database-continuity-backups',
    [string]$TargetPvc = 'his-hope-database-continuity-backups-longhorn',
    [string]$StorageClass = 'longhorn',
    [string]$CopyImage = 'harbor.his-hope.local:9443/his-hope/busybox:1.36@sha256:b7f3d86d6e84fc17718c48bcde1450807faa2d56704205c697b4bd5df7b9e29f',
    [string]$OutputPath,
    [switch]$Apply,
    [switch]$AllowProduction
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

if (-not (Test-Path -LiteralPath $Kubeconfig -PathType Leaf)) {
    throw "Kubeconfig not found: $Kubeconfig"
}
if ($Apply -and $Namespace -eq 'his-hope' -and -not $AllowProduction) {
    throw 'Production PVC migration is blocked by default. Re-run with -AllowProduction after change approval.'
}

function Invoke-KubectlJson {
    param([Parameter(Mandatory)][string[]]$Arguments)
    $output = & kubectl --kubeconfig $Kubeconfig @Arguments 2>&1
    if ($LASTEXITCODE -ne 0) {
        throw "kubectl failed: kubectl --kubeconfig $Kubeconfig $($Arguments -join ' ')"
    }
    return (($output -join "`n") | ConvertFrom-Json)
}

function Invoke-KubectlText {
    param([Parameter(Mandatory)][string[]]$Arguments)
    $output = & kubectl --kubeconfig $Kubeconfig @Arguments 2>&1
    if ($LASTEXITCODE -ne 0) {
        throw "kubectl failed: kubectl --kubeconfig $Kubeconfig $($Arguments -join ' ')"
    }
    return ($output -join "`n").Trim()
}

$source = Invoke-KubectlJson @('get', 'pvc', $SourcePvc, '-n', $Namespace, '-o', 'json')
$sourceClass = [string]$source.spec.storageClassName
if ([string]::IsNullOrWhiteSpace($sourceClass)) { throw "Source PVC $SourcePvc has no storage class." }
if ($sourceClass -eq $StorageClass) {
    Write-Output "Source PVC already uses $StorageClass; no migration is required."
    exit 0
}

$sourceSize = [string]$source.spec.resources.requests.storage
if ([string]::IsNullOrWhiteSpace($sourceSize)) { throw "Source PVC $SourcePvc has no requested size." }
$deploymentObject = Invoke-KubectlJson @('get', 'deployment', $Deployment, '-n', $Namespace, '-o', 'json')
$originalReplicas = [int]$deploymentObject.spec.replicas
$sourceVolume = [string]$source.spec.volumeName
$nodeName = $null
if (-not [string]::IsNullOrWhiteSpace($sourceVolume)) {
    $pv = Invoke-KubectlJson @('get', 'pv', $sourceVolume, '-o', 'json')
    $terms = @($pv.spec.nodeAffinity.required.nodeSelectorTerms)
    foreach ($term in $terms) {
        foreach ($expr in @($term.matchExpressions)) {
            if ($expr.key -eq 'kubernetes.io/hostname' -and @($expr.values).Count -gt 0) {
                $nodeName = [string]$expr.values[0]
                break
            }
        }
        if ($nodeName) { break }
    }
}

$existingTarget = $null
try { $existingTarget = Invoke-KubectlJson @('get', 'pvc', $TargetPvc, '-n', $Namespace, '-o', 'json') } catch { }
if ($existingTarget -and [string]$existingTarget.spec.storageClassName -ne $StorageClass) {
    throw "Target PVC $TargetPvc already exists with an unexpected storage class. Refusing to modify it."
}

$copyPod = "${TargetPvc}-copy"

function Write-Summary {
    param([Parameter(Mandatory)][string]$Status, [string]$Detail)
    if ([string]::IsNullOrWhiteSpace($OutputPath)) { return }
    $resolved = [IO.Path]::GetFullPath($OutputPath)
    New-Item -ItemType Directory -Force -Path (Split-Path -Parent $resolved) | Out-Null
    [ordered]@{
        status = $Status
        generatedAtUtc = [DateTime]::UtcNow.ToString('o')
        namespace = $Namespace
        sourcePvc = $SourcePvc
        sourceStorageClass = $sourceClass
        targetPvc = $TargetPvc
        targetStorageClass = $StorageClass
        detail = $Detail
    } | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath $resolved -Encoding utf8
}
$nodeYaml = if ($nodeName) { "`n      nodeName: $nodeName" } else { '' }
$targetManifest = @"
apiVersion: v1
kind: PersistentVolumeClaim
metadata:
  name: $TargetPvc
  namespace: $Namespace
  labels:
    app.kubernetes.io/name: database-continuity
    app.kubernetes.io/component: backup-migration
spec:
  accessModes: [ReadWriteOnce]
  resources:
    requests:
      storage: $sourceSize
  storageClassName: $StorageClass
---
apiVersion: v1
kind: Pod
metadata:
  name: $copyPod
  namespace: $Namespace
  labels:
    app.kubernetes.io/name: database-continuity
    app.kubernetes.io/component: backup-migration
spec:
  restartPolicy: Never
  imagePullSecrets:
    - name: harbor-pull
  securityContext:
    runAsNonRoot: true
    runAsUser: 1654
    runAsGroup: 1654
    fsGroup: 1654
    seccompProfile:
      type: RuntimeDefault$nodeYaml
  containers:
    - name: copier
      image: $CopyImage
      imagePullPolicy: IfNotPresent
      command: ["sh", "-ec"]
      args:
        - |
          set -eu
          if [ -f /dst/.migration-complete ]; then exit 0; fi
          cp -a /src/. /dst/
          sync
          find /dst -type f ! -name .migration-sha256 ! -name .migration-complete -exec sha256sum {} + | sort > /dst/.migration-sha256
          touch /dst/.migration-complete
      securityContext:
        allowPrivilegeEscalation: false
        capabilities:
          drop: [ALL]
        seccompProfile:
          type: RuntimeDefault
      volumeMounts:
        - name: source
          mountPath: /src
          readOnly: true
        - name: target
          mountPath: /dst
  volumes:
    - name: source
      persistentVolumeClaim:
        claimName: $SourcePvc
    - name: target
      persistentVolumeClaim:
        claimName: $TargetPvc
"@
$manifestParts = @($targetManifest -split '(?m)^---\s*$') | Where-Object { -not [string]::IsNullOrWhiteSpace($_) }
if ($manifestParts.Count -ne 2) { throw 'Internal migration manifest split failed.' }
$pvcManifest = $manifestParts[0].Trim()
$copyPodManifest = $manifestParts[1].Trim()

Write-Output "Source PVC: $SourcePvc storageClass=$sourceClass size=$sourceSize"
Write-Output "Target PVC: $TargetPvc storageClass=$StorageClass"
if ($nodeName) { Write-Output "Pinned copy node: $nodeName" }
if (-not $Apply) {
    Write-Output 'DRY-RUN: no PVC, pod, scale or deployment mutation performed.'
    Write-Output 'Re-run with -Apply -AllowProduction only after backup/restore approval.'
    Write-Summary -Status 'dry-run' -Detail 'Preflight only; no mutation performed.'
    exit 0
}
if ($WhatIfPreference) {
    Write-Output 'WHATIF: no PVC, pod, scale or deployment mutation performed.'
    Write-Summary -Status 'what-if' -Detail 'WhatIf requested; no mutation performed.'
    exit 0
}

if ($PSCmdlet.ShouldProcess("$Namespace/$TargetPvc", 'Create Longhorn migration PVC')) {
    $pvcManifest | & kubectl --kubeconfig $Kubeconfig apply --server-side --field-manager=his-hope-pvc-migration -f - | Out-Null
    if ($LASTEXITCODE -ne 0) { throw 'Unable to create the target PVC.' }
}
Invoke-KubectlText @('wait', '--for=jsonpath={.status.phase}=Bound', "pvc/$TargetPvc", '-n', $Namespace, '--timeout=10m') | Out-Null

 $scaledDown = $false
try {
    if ($PSCmdlet.ShouldProcess("deployment/$Deployment", 'Scale database-continuity down before copy')) {
        Invoke-KubectlText @('scale', "deployment/$Deployment", '-n', $Namespace, '--replicas=0') | Out-Null
        Invoke-KubectlText @('wait', '--for=delete', "pod", '-l', 'app.kubernetes.io/name=database-continuity,app.kubernetes.io/component=platform', '-n', $Namespace, '--timeout=5m') | Out-Null
        $scaledDown = $true
    }

    if ($PSCmdlet.ShouldProcess("pod/$copyPod", 'Copy source PVC to Longhorn target')) {
        $copyPodManifest | & kubectl --kubeconfig $Kubeconfig apply --server-side --field-manager=his-hope-pvc-migration -f - | Out-Null
        if ($LASTEXITCODE -ne 0) { throw 'Unable to create the copy pod.' }
    }
    Invoke-KubectlText @('wait', '--for=jsonpath={.status.phase}=Succeeded', "pod/$copyPod", '-n', $Namespace, '--timeout=10m') | Out-Null
}
finally {
    if ($scaledDown) {
        Invoke-KubectlText @('scale', "deployment/$Deployment", '-n', $Namespace, "--replicas=$originalReplicas") | Out-Null
    }
}

Write-Output 'Copy PASS: target contains .migration-complete and checksum manifest.'
Write-Output "Next controlled step: patch $Deployment to claimName=$TargetPvc in a separate reviewed GitOps PR, then scale it back up and run go-live validation."
Write-Output "Rollback: keep $SourcePvc; revert claimName and scale $Deployment to its previous replica count."
Write-Summary -Status 'pass' -Detail 'Target PVC copied and verified; deployment claimName was intentionally not changed.'
