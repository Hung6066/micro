[CmdletBinding(SupportsShouldProcess)]
param(
    [string]$Kubeconfig = 'D:\AI\micro\artifacts\kubeconfig-production.yaml',
    [switch]$Apply,
    [switch]$AllowProduction
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$boundaryPath = Join-Path $repoRoot 'k8s/gitops/boundaries'
$newline = [Environment]::NewLine
$expected = [ordered]@{
    'his-hope-data' = 'baseline'
    'his-hope-system' = 'privileged'
}

if (-not (Test-Path -LiteralPath $Kubeconfig -PathType Leaf)) {
    throw "Kubeconfig not found: $Kubeconfig"
}
if ($Apply -and -not $AllowProduction) {
    throw 'Production security-boundary bootstrap is blocked by default. Re-run with -AllowProduction after change approval.'
}

$rendered = & kubectl kustomize $boundaryPath --load-restrictor LoadRestrictionsNone 2>&1
if ($LASTEXITCODE -ne 0) { throw "Boundary render failed: $($rendered -join $newline)" }
$renderText = $rendered -join $newline
foreach ($name in $expected.Keys) {
    if ($renderText -notmatch "name:\s*$name") { throw "Boundary manifest is missing namespace $name." }
}
Write-Output 'Boundary manifest render PASS.'

function Get-EnforceLabel {
    param([Parameter(Mandatory)][string]$Namespace)
    $out = & kubectl --kubeconfig $Kubeconfig get namespace $Namespace -o json 2>&1
    if ($LASTEXITCODE -ne 0) { return $null }
    $obj = ($out -join $newline) | ConvertFrom-Json
    return [string]$obj.metadata.labels.'pod-security.kubernetes.io/enforce'
}

$missing = [System.Collections.Generic.List[string]]::new()
foreach ($entry in $expected.GetEnumerator()) {
    $value = Get-EnforceLabel -Namespace $entry.Key
    if ([string]::IsNullOrWhiteSpace($value)) {
        $missing.Add($entry.Key)
        continue
    }
    if ($value -ne $entry.Value) {
        throw "Namespace $($entry.Key) has enforce=$value; expected $($entry.Value)."
    }
    Write-Output "Existing namespace verified: $($entry.Key) enforce=$value"
}

if (-not $Apply) {
    if ($missing.Count -gt 0) {
        Write-Output "DRY-RUN: namespaces to create: $($missing -join ', ')"
    } else {
        Write-Output 'DRY-RUN: all security boundary namespaces already match the contract.'
    }
    Write-Output 'Re-run with -Apply after change approval to create/update only the namespace boundary objects.'
    exit 0
}

if ($PSCmdlet.ShouldProcess('GitOps security-boundary namespaces', 'Apply namespace labels and annotations server-side')) {
    & kubectl --kubeconfig $Kubeconfig apply --server-side --field-manager=his-hope-boundary-bootstrap -k $boundaryPath
    if ($LASTEXITCODE -ne 0) { throw 'Unable to apply security boundary namespaces.' }
}

foreach ($entry in $expected.GetEnumerator()) {
    $value = Get-EnforceLabel -Namespace $entry.Key
    if ($value -ne $entry.Value) { throw "Post-apply verification failed for $($entry.Key): enforce=$value" }
}
Write-Output 'Security boundary bootstrap PASS.'
