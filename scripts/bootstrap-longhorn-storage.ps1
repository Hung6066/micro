[CmdletBinding(SupportsShouldProcess)]
param(
    [ValidateSet('staging', 'production')][string]$Environment = 'staging',
    [string]$Kubeconfig = 'artifacts/kubeconfig-staging.yaml',
    [ValidatePattern('^1\.[0-9]+\.[0-9]+$')][string]$Version = '1.12.0',
    [string]$Namespace = 'longhorn-system',
    [switch]$Apply,
    [switch]$AllowProduction
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

if ($Environment -eq 'production' -and -not $AllowProduction) {
    throw 'Production Longhorn bootstrap is blocked by default; validate staging and obtain change approval first.'
}
if (-not (Test-Path -LiteralPath $Kubeconfig -PathType Leaf)) { throw "Kubeconfig not found: $Kubeconfig" }
if (-not (Get-Command helm -ErrorAction SilentlyContinue)) { throw 'Helm 3 is required.' }
if (-not (Get-Command kubectl -ErrorAction SilentlyContinue)) { throw 'kubectl is required.' }

$env:KUBECONFIG = (Resolve-Path -LiteralPath $Kubeconfig).Path
if ($Environment -eq 'production') {
    $nodes = @(kubectl get nodes -o json | ConvertFrom-Json).items
    if ($LASTEXITCODE -ne 0 -or $nodes.Count -eq 0) {
        throw 'Unable to inspect production nodes before Longhorn bootstrap.'
    }
    $unprepared = @($nodes | Where-Object {
            $labels = $_.metadata.labels
            $ready = $false
            if ($null -ne $labels -and
                @($labels.PSObject.Properties.Name) -contains 'his-hope.io/longhorn-data-ready') {
                $ready = $labels.PSObject.Properties['his-hope.io/longhorn-data-ready'].Value -eq 'true'
            }
            -not $ready
        })
    if ($unprepared.Count -gt 0) {
        $names = ($unprepared | ForEach-Object { $_.metadata.name }) -join ', '
        throw "Longhorn production bootstrap blocked: every node must have his-hope.io/longhorn-data-ready=true after a dedicated data-disk/iSCSI audit. Missing: $names"
    }
}
& helm repo add longhorn https://charts.longhorn.io --force-update | Out-Null
& helm repo update | Out-Null
if ($LASTEXITCODE -ne 0) { throw 'Unable to update the Longhorn Helm repository.' }

$chartInfo = & helm show chart longhorn/longhorn --version $Version 2>&1
if ($LASTEXITCODE -ne 0) { throw "Pinned Longhorn chart $Version is unavailable." }
if (($chartInfo -join "`n") -notmatch 'name:\s*longhorn') { throw 'Unexpected Longhorn chart metadata.' }

$helmArgs = @(
    'upgrade', 'longhorn', 'longhorn/longhorn', '--install', '--atomic',
    '--create-namespace', '--namespace', $Namespace, '--version', $Version,
    '--set', 'persistence.defaultClass=true',
    '--set', 'persistence.defaultClassReplicaCount=3'
)
if (-not $Apply) {
    Write-Output "DRY-RUN: verified Longhorn chart $Version for $Environment. No cluster mutation requested."
    & helm @helmArgs '--dry-run=client' | Out-Null
    if ($LASTEXITCODE -ne 0) { throw 'Longhorn Helm client dry-run failed.' }
    exit 0
}

if ($PSCmdlet.ShouldProcess("$Environment/$Namespace", "Install Longhorn $Version with three replicas")) {
    & helm @helmArgs
    if ($LASTEXITCODE -ne 0) { throw 'Longhorn Helm upgrade failed.' }
    & kubectl rollout status daemonset/longhorn-manager -n $Namespace --timeout=15m
    if ($LASTEXITCODE -ne 0) { throw 'Longhorn manager did not become Ready.' }
    & kubectl get crd volumesnapshots.snapshot.storage.k8s.io -o name
    if ($LASTEXITCODE -ne 0) { throw 'CSI VolumeSnapshot CRD is not available after Longhorn bootstrap.' }

    @"
apiVersion: snapshot.storage.k8s.io/v1
kind: VolumeSnapshotClass
metadata:
  name: longhorn
  labels:
    velero.io/csi-volumesnapshot-class: "true"
driver: driver.longhorn.io
deletionPolicy: Retain
"@ | kubectl apply --server-side --field-manager=his-hope-storage-bootstrap -f -
    if ($LASTEXITCODE -ne 0) { throw 'Unable to apply the Longhorn VolumeSnapshotClass.' }
}

Write-Output "Longhorn storage bootstrap PASS: environment=$Environment version=$Version replicaCount=3"
