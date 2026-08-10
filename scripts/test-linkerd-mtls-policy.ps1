[CmdletBinding()]
param(
    [ValidateSet('staging','production')][string]$Environment = 'staging',
    [string]$Kubeconfig = 'artifacts/kubeconfig-staging.yaml',
    [string]$Namespace = 'his-hope',
    [string]$Image = '',
    [string]$OutputPath = 'artifacts/evidence/linkerd-mtls-policy.json',
    [switch]$Apply,
    [switch]$AllowProduction
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

if ($Environment -eq 'production' -and $Apply -and -not $AllowProduction) {
    throw 'Production Linkerd policy probe requires -AllowProduction from the protected workflow.'
}
if ($Namespace -notmatch '^[a-z0-9]([-a-z0-9]*[a-z0-9])?$') { throw 'Namespace must be a DNS label.' }
if ($Apply -and $Image -notmatch '^[A-Za-z0-9./:_-]+@sha256:[0-9a-f]{64}$') {
    throw 'Apply requires an immutable grpcurl image reference ending in @sha256:<64 hex characters>.'
}
if (-not (Test-Path -LiteralPath $Kubeconfig -PathType Leaf)) { throw "Kubeconfig not found: $Kubeconfig" }

$output = [IO.Path]::GetFullPath($OutputPath)
$parent = Split-Path -Parent $output
if ($parent) { New-Item -ItemType Directory -Force -Path $parent | Out-Null }
$started = [DateTime]::UtcNow

function Write-Evidence([string]$Status, [bool]$Verified, [string]$Failure = $null) {
    $doc = [ordered]@{
        status = $Status
        executedAtUtc = $started.ToString('o')
        positiveAuthorizationVerified = $Verified
        negativeAuthorizationVerified = $Verified
        target = "$Environment/$Namespace/patient-service:5006"
        failure = $Failure
    }
    [IO.File]::WriteAllText($output, ($doc | ConvertTo-Json -Depth 5), [Text.UTF8Encoding]::new($false))
}

if (-not $Apply) {
    Write-Evidence 'skipped' $false
    Write-Output 'Linkerd mTLS policy probe DRY-RUN: no temporary pods or policy changes created.'
    exit 0
}

$env:KUBECONFIG = (Resolve-Path -LiteralPath $Kubeconfig).Path
$suffix = ([guid]::NewGuid().ToString('N')).Substring(0, 8)
$positive = "linkerd-mtls-positive-$suffix"
$negative = "linkerd-mtls-negative-$suffix"
$negativeSa = "linkerd-mtls-negative-$suffix"
$manifestPath = Join-Path ([IO.Path]::GetTempPath()) "linkerd-mtls-$suffix.yaml"

function Invoke-Kubectl([string[]]$Arguments) {
    $result = & kubectl @Arguments 2>&1
    if ($LASTEXITCODE -ne 0) { throw "kubectl $($Arguments -join ' ') failed." }
    return $result
}

try {
    Invoke-Kubectl @('create','serviceaccount',$negativeSa,'-n',$Namespace) | Out-Null
    $manifest = @"
apiVersion: v1
kind: Pod
metadata:
  name: $positive
  namespace: $Namespace
  labels:
    app.kubernetes.io/name: linkerd-mtls-positive
  annotations:
    linkerd.io/inject: enabled
spec:
  serviceAccountName: identity-service
  restartPolicy: Never
  containers:
  - name: probe
    image: $Image
    command: ["/bin/sh", "-c", "sleep 3600"]
---
apiVersion: v1
kind: Pod
metadata:
  name: $negative
  namespace: $Namespace
  labels:
    app.kubernetes.io/name: linkerd-mtls-negative
  annotations:
    linkerd.io/inject: enabled
spec:
  serviceAccountName: $negativeSa
  restartPolicy: Never
  containers:
  - name: probe
    image: $Image
    command: ["/bin/sh", "-c", "sleep 3600"]
"@
    Set-Content -LiteralPath $manifestPath -Value $manifest -Encoding utf8
    Invoke-Kubectl @('apply','-f',$manifestPath) | Out-Null
    Invoke-Kubectl @('wait','--for=condition=Ready',"pod/$positive",'-n',$Namespace,'--timeout=180s') | Out-Null
    Invoke-Kubectl @('wait','--for=condition=Ready',"pod/$negative",'-n',$Namespace,'--timeout=180s') | Out-Null

    $positiveContainers = (Invoke-Kubectl @('get','pod',$positive,'-n',$Namespace,'-o','json') | ConvertFrom-Json).spec.containers.name
    $negativeContainers = (Invoke-Kubectl @('get','pod',$negative,'-n',$Namespace,'-o','json') | ConvertFrom-Json).spec.containers.name
    if ($positiveContainers -notcontains 'linkerd-proxy' -or $negativeContainers -notcontains 'linkerd-proxy') { throw 'Injector did not add linkerd-proxy to both policy probe pods.' }

    $target = "$Namespace-patient-service.$Namespace.svc.cluster.local:5006"
    & kubectl exec $positive -n $Namespace -c probe -- grpcurl -plaintext $target list *> $null
    if ($LASTEXITCODE -ne 0) { throw 'Positive mTLS authorization probe was denied.' }
    & kubectl exec $negative -n $Namespace -c probe -- grpcurl -plaintext $target list *> $null
    if ($LASTEXITCODE -eq 0) { throw 'Negative mTLS authorization probe was accepted.' }

    Write-Evidence 'pass' $true
    Write-Output 'Linkerd mTLS policy probe PASS: authorized identity allowed and unauthorized identity denied.'
}
catch {
    $failure = $_.Exception.Message -replace '(?i)(password|token|secret|sas|client[_-]?secret)[^;\r\n]*', '$1=[redacted]'
    Write-Evidence 'fail' $false $failure
    throw
}
finally {
    & kubectl delete pod $positive $negative -n $Namespace --ignore-not-found --wait=false *> $null
    & kubectl delete serviceaccount $negativeSa -n $Namespace --ignore-not-found *> $null
    Remove-Item -LiteralPath $manifestPath -Force -ErrorAction SilentlyContinue
}
