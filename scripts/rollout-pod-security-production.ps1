[CmdletBinding(SupportsShouldProcess)]
param(
    [string]$Kubeconfig = 'D:\AI\micro\artifacts\kubeconfig-production.yaml',
    [string]$Namespace = 'his-hope',
    [string]$SystemNamespace = 'his-hope-system',
    [switch]$Apply,
    [switch]$AllowProduction
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$renderedPath = Join-Path $repoRoot 'artifacts/k8s/prod.yaml'
$newline = [Environment]::NewLine

if (-not (Test-Path -LiteralPath $Kubeconfig -PathType Leaf)) {
    throw "Kubeconfig not found: $Kubeconfig"
}
if ($Apply -and $Namespace -eq 'his-hope' -and -not $AllowProduction) {
    throw 'Production Pod Security rollout is blocked by default. Re-run with -AllowProduction after change approval.'
}

function Invoke-Kubectl {
    param([Parameter(Mandatory)][string[]]$Arguments)
    $output = & kubectl --kubeconfig $Kubeconfig @Arguments 2>&1
    if ($LASTEXITCODE -ne 0) {
        throw "kubectl failed: kubectl --kubeconfig $Kubeconfig $($Arguments -join ' ')$newline$($output -join $newline)"
    }
    return @($output)
}

function Invoke-KubectlText {
    param([Parameter(Mandatory)][string[]]$Arguments)
    return ((Invoke-Kubectl $Arguments) -join $newline).Trim()
}

Write-Output 'Preflight: render production overlay.'
$rendered = & kubectl kustomize (Join-Path $repoRoot 'k8s/overlays/prod') --load-restrictor LoadRestrictionsNone 2>&1
if ($LASTEXITCODE -ne 0) { throw "Production Kustomize render failed.$newline$($rendered -join $newline)" }
[IO.File]::WriteAllText($renderedPath, ($rendered -join $newline), [Text.UTF8Encoding]::new($false))

$checker = & python (Join-Path $repoRoot 'scripts/check-restricted-workloads.py') $renderedPath 2>&1
if ($LASTEXITCODE -ne 0 -or ($checker -join $newline) -notmatch 'TOTAL_NONCOMPLIANT_CONTAINERS=0') {
    throw "Restricted workload preflight failed.$newline$($checker -join $newline)"
}
Write-Output ($checker -join $newline)

Write-Output 'Preflight: inspect live workload security contexts before enforcing restricted.'
$liveWorkloadPath = Join-Path $env:TEMP "his-hope-live-workloads-$PID.yaml"
try {
    $liveWorkloads = Invoke-Kubectl @('get', 'deployments,statefulsets,daemonsets', '-n', $Namespace, '-o', 'yaml')
    [IO.File]::WriteAllText($liveWorkloadPath, ($liveWorkloads -join $newline), [Text.UTF8Encoding]::new($false))
    $liveChecker = & python (Join-Path $repoRoot 'scripts/check-restricted-workloads.py') $liveWorkloadPath 2>&1
    if ($LASTEXITCODE -ne 0 -or ($liveChecker -join $newline) -notmatch 'TOTAL_NONCOMPLIANT_CONTAINERS=0') {
        throw "Live restricted workload preflight failed.$newline$($liveChecker -join $newline)"
    }
    Write-Output ($liveChecker -join $newline)
}
finally {
    Remove-Item -LiteralPath $liveWorkloadPath -Force -ErrorAction SilentlyContinue
}

$readyNodes = @(Invoke-Kubectl @('get', 'nodes', '--no-headers', '-o', 'custom-columns=READY:.status.conditions[?(@.type=="Ready")].status') |
    Where-Object { $_.Trim() -eq 'True' }).Count
if ($readyNodes -lt 3) { throw "At least 3 Ready nodes are required; found $readyNodes." }
Write-Output "Ready nodes: $readyNodes"

$currentEnforce = Invoke-KubectlText @('get', 'namespace', $Namespace, '-o', 'jsonpath={.metadata.labels.pod-security\.kubernetes\.io/enforce}')
$currentSystemEnforce = Invoke-KubectlText @('get', 'namespace', $SystemNamespace, '-o', 'jsonpath={.metadata.labels.pod-security\.kubernetes\.io/enforce}')
if ($currentSystemEnforce -ne 'privileged') {
    throw "$SystemNamespace must remain the explicit privileged boundary for the hostPath seccomp installer; found '$currentSystemEnforce'."
}
Write-Output "Current $Namespace enforce=$currentEnforce; $SystemNamespace enforce=$currentSystemEnforce"

if (-not $Apply) {
    Write-Output "DRY-RUN: preflight passed. Re-run with -Apply after change approval to set $Namespace enforce=restricted."
    exit 0
}

if ($PSCmdlet.ShouldProcess("namespace/$Namespace", 'Set Pod Security enforce/warn/audit to restricted')) {
    Invoke-Kubectl @(
        'label', 'namespace', $Namespace,
        'pod-security.kubernetes.io/enforce=restricted',
        'pod-security.kubernetes.io/enforce-version=latest',
        'pod-security.kubernetes.io/warn=restricted',
        'pod-security.kubernetes.io/audit=restricted',
        '--overwrite'
    ) | Out-Null
}

$verified = Invoke-KubectlText @('get', 'namespace', $Namespace, '-o', 'jsonpath={.metadata.labels.pod-security\.kubernetes\.io/enforce}')
if ($verified -ne 'restricted') { throw "Pod Security label verification failed: $verified" }
Write-Output "Pod Security rollout PASS: $Namespace enforce=restricted."
Write-Output 'Workload restart/rollout is intentionally separate; run the approved deployment rollout and release validator before declaring runtime compliance.'
