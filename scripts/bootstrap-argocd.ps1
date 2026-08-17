[CmdletBinding(SupportsShouldProcess)]
param(
    [Parameter(Mandatory = $true)][string]$ManifestSha256,
    [ValidateSet('staging', 'production')][string]$Environment = 'staging',
    [string]$Kubeconfig = 'artifacts/kubeconfig-staging.yaml',
    [ValidatePattern('^v[0-9]+\.[0-9]+\.[0-9]+$')][string]$Version = 'v3.4.1',
    [switch]$HighAvailability,
    [switch]$AllowProduction
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
if ($ManifestSha256 -notmatch '^[0-9a-fA-F]{64}$') { throw 'ManifestSha256 must be a 64-character SHA-256 digest.' }
if ($Environment -eq 'production' -and -not $AllowProduction) {
    throw 'Production bootstrap is blocked by default. Re-run with -AllowProduction after staging validation and change approval.'
}
if (-not (Test-Path -LiteralPath $Kubeconfig -PathType Leaf)) { throw "Kubeconfig not found: $Kubeconfig" }

$manifestKind = if ($HighAvailability) { 'ha/install.yaml' } else { 'install.yaml' }
$uri = "https://raw.githubusercontent.com/argoproj/argo-cd/$Version/manifests/$manifestKind"
$temp = Join-Path ([IO.Path]::GetTempPath()) "argocd-$Version-$([guid]::NewGuid()).yaml"
try {
    Invoke-WebRequest -Uri $uri -OutFile $temp -UseBasicParsing
    $actual = (Get-FileHash -LiteralPath $temp -Algorithm SHA256).Hash
    if ($actual -ne $ManifestSha256.ToUpperInvariant()) {
        throw "Argo CD manifest checksum mismatch. Expected $ManifestSha256, received $actual."
    }
    # Argo CD desired state lives in Git and the Kubernetes API. The pinned HA
    # install is intentionally cache-only (emptyDir); fail closed if a future
    # upstream manifest silently introduces a PVC that would consume the
    # node-local local-path class without an approved shared CSI backend.
    $manifestText = Get-Content -LiteralPath $temp -Raw
    if ($manifestText -match '(?m)^kind:\s*PersistentVolumeClaim\s*$' -or
        $manifestText -match '(?ms)volumeClaimTemplates:.*?storageClassName:') {
        throw 'Argo CD manifest contains persistent storage. Use an explicitly reviewed shared CSI profile before bootstrap.'
    }
    $env:KUBECONFIG = (Resolve-Path -LiteralPath $Kubeconfig).Path
    if ($PSCmdlet.ShouldProcess("$Environment cluster configured by $Kubeconfig", "Install Argo CD $Version")) {
        kubectl create namespace argocd --dry-run=client -o yaml | kubectl apply --server-side -f -
        if ($LASTEXITCODE -ne 0) { throw 'Failed to create or reconcile the argocd namespace.' }
        # The upstream manifest intentionally omits namespace on some
        # namespaced objects and expects `kubectl apply -n argocd`. Without
        # this flag those objects silently land in `default` on a clean K3s.
        kubectl apply --server-side --force-conflicts -n argocd -f $temp
        if ($LASTEXITCODE -ne 0) { throw 'Failed to apply the verified Argo CD manifest.' }
        if ($HighAvailability) {
            # This K3s topology taints all three control-plane nodes and has
            # only two workers. The upstream HA profile requests three
            # anti-affine Redis/Haproxy replicas, so explicitly allow Argo CD
            # system pods on tainted control-plane nodes; this does not affect
            # application pods.
            $controlPlaneToleration = '{"spec":{"template":{"spec":{"tolerations":[{"key":"node-role.kubernetes.io/control-plane","operator":"Exists","effect":"NoSchedule"}]}}}}'
            foreach ($kind in @('deployment', 'statefulset')) {
                $resources = @(kubectl get $kind -n argocd -o name)
                foreach ($resource in $resources) {
                    kubectl patch $resource -n argocd --type=merge --patch $controlPlaneToleration | Out-Null
                    if ($LASTEXITCODE -ne 0) { throw "Failed to add control-plane toleration to Argo CD resource $resource." }
                }
            }
        }
        kubectl wait --for=condition=Available deployment --all -n argocd --timeout=10m
        if ($LASTEXITCODE -ne 0) { throw 'Argo CD deployments did not become Available.' }
    }
    Write-Output "Argo CD bootstrap manifest verified: environment=$Environment version=$Version mode=$(if ($HighAvailability) {'ha'} else {'standard'}) sha256=$actual"
}
finally {
    Remove-Item -LiteralPath $temp -Force -ErrorAction SilentlyContinue
}
