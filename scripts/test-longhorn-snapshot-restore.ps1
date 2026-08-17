[CmdletBinding()]
param(
    [ValidateSet('staging', 'production')][string]$Environment = 'staging',
    [string]$Kubeconfig = 'artifacts/kubeconfig-staging.yaml',
    [string]$StorageClass = 'longhorn',
    [Parameter(Mandatory)][ValidatePattern('@sha256:[0-9a-f]{64}$')][string]$DrillImage,
    [string]$OutputPath = 'artifacts/evidence/longhorn-snapshot-restore.json',
    [switch]$AllowProduction
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

if ($Environment -eq 'production' -and -not $AllowProduction) {
    throw 'Production restore drill is blocked by default; run in an isolated approved target with -AllowProduction.'
}
if (-not (Test-Path -LiteralPath $Kubeconfig -PathType Leaf)) { throw "Kubeconfig not found: $Kubeconfig" }

$env:KUBECONFIG = (Resolve-Path -LiteralPath $Kubeconfig).Path
$suffix = [DateTime]::UtcNow.ToString('yyyyMMddHHmmss')
$namespace = "longhorn-drill-$suffix"
$sourcePvc = 'source-pvc'
$restorePvc = 'restore-pvc'
$sourcePod = 'source-writer'
$restorePod = 'restore-reader'
$snapshot = 'source-snapshot'
$started = [DateTime]::UtcNow
$status = 'fail'
$restoreVerified = $false
$sourceChecksum = $null
$restoreChecksum = $null
$failure = $null

function K([string[]]$Args) {
    & kubectl @Args 2>&1
    if ($LASTEXITCODE -ne 0) { throw "kubectl failed: kubectl $($Args -join ' ')" }
}
function KText([string[]]$Args) {
    $out = & kubectl @Args 2>&1
    if ($LASTEXITCODE -ne 0) { throw "kubectl failed: kubectl $($Args -join ' ')" }
    return ($out -join "`n").Trim()
}
function Wait-Phase([string]$Kind, [string]$Name, [string]$JsonPath, [string]$Expected, [int]$TimeoutSeconds = 300) {
    $deadline = [DateTime]::UtcNow.AddSeconds($TimeoutSeconds)
    do {
        $value = KText @('get', $Kind, $Name, '-n', $namespace, '-o', "jsonpath=$JsonPath")
        if ($value -eq $Expected) { return }
        Start-Sleep -Seconds 3
    } while ([DateTime]::UtcNow -lt $deadline)
    throw "$Kind/$Name did not reach $Expected before timeout (actual=$value)."
}

try {
    K @('create', 'namespace', $namespace)
    @"
apiVersion: v1
kind: PersistentVolumeClaim
metadata:
  name: $sourcePvc
  namespace: $namespace
spec:
  accessModes: [ReadWriteOnce]
  storageClassName: $StorageClass
  resources:
    requests:
      storage: 1Gi
---
apiVersion: v1
kind: Pod
metadata:
  name: $sourcePod
  namespace: $namespace
spec:
  restartPolicy: Never
  containers:
    - name: writer
      image: $DrillImage
      command: ["sh", "-c"]
      args: ["printf 'his-hope-longhorn-restore-drill' > /data/marker && sha256sum /data/marker && sleep 3600"]
      volumeMounts:
        - name: data
          mountPath: /data
  volumes:
    - name: data
      persistentVolumeClaim:
        claimName: $sourcePvc
"@ | kubectl apply --server-side --field-manager=his-hope-longhorn-drill -f - | Out-Null
    if ($LASTEXITCODE -ne 0) { throw 'Unable to create source PVC/pod.' }

    Wait-Phase 'pvc' $sourcePvc '{.status.phase}' 'Bound'
    Wait-Phase 'pod' $sourcePod '{.status.phase}' 'Running'
    $sourceChecksum = (KText @('exec', $sourcePod, '-n', $namespace, '--', 'sha256sum', '/data/marker')).Split(' ')[0]

    @"
apiVersion: snapshot.storage.k8s.io/v1
kind: VolumeSnapshot
metadata:
  name: $snapshot
  namespace: $namespace
spec:
  volumeSnapshotClassName: longhorn
  source:
    persistentVolumeClaimName: $sourcePvc
"@ | kubectl apply --server-side --field-manager=his-hope-longhorn-drill -f - | Out-Null
    if ($LASTEXITCODE -ne 0) { throw 'Unable to create VolumeSnapshot.' }
    Wait-Phase 'volumesnapshot' $snapshot '{.status.readyToUse}' 'true' 600

    @"
apiVersion: v1
kind: PersistentVolumeClaim
metadata:
  name: $restorePvc
  namespace: $namespace
spec:
  accessModes: [ReadWriteOnce]
  storageClassName: $StorageClass
  resources:
    requests:
      storage: 1Gi
  dataSource:
    name: $snapshot
    kind: VolumeSnapshot
    apiGroup: snapshot.storage.k8s.io
---
apiVersion: v1
kind: Pod
metadata:
  name: $restorePod
  namespace: $namespace
spec:
  restartPolicy: Never
  containers:
    - name: reader
      image: $DrillImage
      command: ["sh", "-c"]
      args: ["sha256sum /data/marker && sleep 30"]
      volumeMounts:
        - name: data
          mountPath: /data
  volumes:
    - name: data
      persistentVolumeClaim:
        claimName: $restorePvc
"@ | kubectl apply --server-side --field-manager=his-hope-longhorn-drill -f - | Out-Null
    if ($LASTEXITCODE -ne 0) { throw 'Unable to create restored PVC/pod.' }
    Wait-Phase 'pvc' $restorePvc '{.status.phase}' 'Bound' 600
    Wait-Phase 'pod' $restorePod '{.status.phase}' 'Running'
    $restoreChecksum = (KText @('exec', $restorePod, '-n', $namespace, '--', 'sha256sum', '/data/marker')).Split(' ')[0]
    $restoreVerified = $sourceChecksum -eq $restoreChecksum
    if (-not $restoreVerified) { throw "Checksum mismatch: source=$sourceChecksum restore=$restoreChecksum" }
    $status = 'pass'
} catch {
    $failure = $_.Exception.Message
} finally {
    & kubectl delete namespace $namespace --ignore-not-found --wait=false 2>$null | Out-Null
}

$rto = if ($status -eq 'pass') { ([DateTime]::UtcNow - $started).TotalMinutes } else { $null }
$evidence = [pscustomobject]@{
    status = $status
    environment = $Environment
    target = "isolated namespace/$namespace"
    restoreVerified = $restoreVerified
    rpoMinutes = 0
    rtoMinutes = if ($null -ne $rto) { [math]::Round($rto, 2) } else { $null }
    sourceChecksum = $sourceChecksum
    restoreChecksum = $restoreChecksum
    executedAtUtc = $started.ToString('o')
    failure = $failure
}
$directory = Split-Path -Parent $OutputPath
if ($directory) { New-Item -ItemType Directory -Force -Path $directory | Out-Null }
$evidence | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $OutputPath -Encoding utf8
Write-Output ($evidence | ConvertTo-Json -Depth 6)
if ($status -ne 'pass') { exit 70 }
exit 0
