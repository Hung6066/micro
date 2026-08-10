[CmdletBinding(SupportsShouldProcess)]
param(
    [ValidateSet('staging', 'production')][string]$Environment = 'staging',
    [string]$Kubeconfig = 'artifacts/kubeconfig-staging.yaml',
    [ValidatePattern('^0\.[0-9]+\.[0-9]+$')][string]$Version = '0.10.5',
    [string]$Namespace = 'cosign-system',
    [switch]$Apply,
    [switch]$AllowProduction
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

if ($Environment -eq 'production' -and -not $AllowProduction) {
    throw 'Production Policy Controller bootstrap is blocked by default; use -AllowProduction after staging admission tests and change approval.'
}
if (-not (Test-Path -LiteralPath $Kubeconfig -PathType Leaf)) {
    throw "Kubeconfig not found: $Kubeconfig"
}
if (-not (Get-Command helm -ErrorAction SilentlyContinue)) { throw 'Helm 3 is required.' }
if (-not (Get-Command kubectl -ErrorAction SilentlyContinue)) { throw 'kubectl is required.' }

$env:KUBECONFIG = (Resolve-Path -LiteralPath $Kubeconfig).Path
$release = 'policy-controller'
$chart = 'sigstore/policy-controller'

& helm repo add sigstore https://sigstore.github.io/helm-charts --force-update | Out-Null
& helm repo update | Out-Null
if ($LASTEXITCODE -ne 0) { throw 'Unable to update the Sigstore Helm repository.' }

$chartInfo = & helm show chart $chart --version $Version 2>&1
if ($LASTEXITCODE -ne 0) { throw "Pinned Sigstore Policy Controller chart $Version is unavailable." }
if (($chartInfo -join "`n") -notmatch 'name:\s*policy-controller') { throw 'Unexpected chart metadata.' }

$helmArgs = @(
    'upgrade', $release, $chart,
    '--install', '--atomic', '--create-namespace',
    '--namespace', $Namespace,
    '--version', $Version,
    '--set', 'webhook.failurePolicy=Fail'
)
if (-not $Apply) {
    Write-Output "DRY-RUN: verified $chart version $Version for $Environment. No cluster mutation requested."
    & helm @helmArgs '--dry-run=client' | Out-Null
    if ($LASTEXITCODE -ne 0) { throw 'Helm client dry-run failed.' }
    exit 0
}

if ($PSCmdlet.ShouldProcess("$Environment/$Namespace", "Install Sigstore Policy Controller $Version")) {
    & helm @helmArgs
    if ($LASTEXITCODE -ne 0) { throw 'Sigstore Policy Controller Helm upgrade failed.' }
    & kubectl rollout status deployment/$release -n $Namespace --timeout=10m
    if ($LASTEXITCODE -ne 0) { throw 'Sigstore Policy Controller did not become Ready.' }
    & kubectl get crd clusterimagepolicies.policy.sigstore.dev -o name
    if ($LASTEXITCODE -ne 0) { throw 'ClusterImagePolicy CRD is not available after bootstrap.' }
}

Write-Output "Sigstore Policy Controller bootstrap PASS: environment=$Environment version=$Version namespace=$Namespace"
