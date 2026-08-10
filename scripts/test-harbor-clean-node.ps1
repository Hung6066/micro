[CmdletBinding()]
param(
    [ValidateSet('staging', 'production')][string]$Environment = 'staging',
    [string]$Kubeconfig = 'artifacts/kubeconfig-staging.yaml',
    [Parameter(Mandatory)][ValidatePattern('@sha256:[0-9a-f]{64}$')][string]$TestImage,
    [Parameter(Mandatory)][string]$NodeName,
    [string]$OutputPath = 'artifacts/evidence/harbor-clean-node-test.json',
    [switch]$AllowProduction,
    [switch]$WhatIf
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

if ($Environment -eq 'production' -and -not $AllowProduction) {
    throw 'Production Harbor clean-node test is blocked by default; use the protected workflow with -AllowProduction.'
}
if (-not (Test-Path -LiteralPath $Kubeconfig -PathType Leaf)) { throw "Kubeconfig not found: $Kubeconfig" }
if ($TestImage -notmatch '^harbor\.[^/]+/his-hope/[a-z0-9][a-z0-9./-]*@sha256:[0-9a-f]{64}$') {
    throw 'TestImage must be an approved Harbor his-hope digest reference.'
}

$output = [IO.Path]::GetFullPath($OutputPath)
$outputDirectory = Split-Path -Parent $output
if ($outputDirectory) { New-Item -ItemType Directory -Force -Path $outputDirectory | Out-Null }
$startedAt = [DateTime]::UtcNow
$namespace = "harbor-clean-node-$($startedAt.ToString('yyyyMMddHHmmss'))"
$podName = 'registry-pull'
$status = 'fail'
$verified = $false
$failure = $null
$phase = $null
$reason = $null

function Write-Evidence {
    $rto = if ($verified) { ([DateTime]::UtcNow - $startedAt).TotalMinutes } else { 0 }
    $document = [pscustomobject]@{
        status = $status
        executedAtUtc = $startedAt.ToString('o')
        rpoMinutes = 0
        rtoMinutes = [math]::Round($rto, 3)
        restoreVerified = $verified
        target = "$Environment/$NodeName"
        image = $TestImage
        nodeName = $NodeName
        podPhase = $phase
        podReason = $reason
        failure = $failure
    }
    [IO.File]::WriteAllText($output, ($document | ConvertTo-Json -Depth 6), [Text.UTF8Encoding]::new($false))
}

if ($WhatIf) {
    $status = 'skipped'
    $verified = $false
    Write-Evidence
    Write-Output "DRILL DRY-RUN: Harbor pull test would run on node $NodeName with digest-pinned image."
    exit 0
}

$env:KUBECONFIG = (Resolve-Path -LiteralPath $Kubeconfig).Path
try {
    & kubectl create namespace $namespace --dry-run=client -o yaml | & kubectl apply --server-side --field-manager=his-hope-harbor-drill -f - | Out-Null
    if ($LASTEXITCODE -ne 0) { throw 'Unable to create isolated drill namespace.' }
    $manifest = @"
apiVersion: v1
kind: Pod
metadata:
  name: $podName
  namespace: $namespace
  labels:
    app.kubernetes.io/name: harbor-clean-node-test
    app.kubernetes.io/part-of: his-hope-dr
spec:
  restartPolicy: Never
  nodeName: $NodeName
  automountServiceAccountToken: false
  securityContext:
    runAsNonRoot: true
    runAsUser: 65532
    runAsGroup: 65532
    seccompProfile:
      type: RuntimeDefault
  containers:
    - name: pull-test
      image: $TestImage
      imagePullPolicy: Always
      command: ["/bin/sh", "-c"]
      args: ["printf harbor-pull-ok"]
      securityContext:
        allowPrivilegeEscalation: false
        readOnlyRootFilesystem: true
        capabilities:
          drop: [ALL]
"@
    $manifest | & kubectl apply --server-side --field-manager=his-hope-harbor-drill -f - | Out-Null
    if ($LASTEXITCODE -ne 0) { throw 'Unable to create Harbor clean-node test pod.' }

    $deadline = [DateTime]::UtcNow.AddMinutes(10)
    do {
        $phase = (& kubectl get pod $podName -n $namespace -o jsonpath='{.status.phase}' 2>$null).Trim()
        if ($phase -in @('Succeeded', 'Failed')) { break }
        Start-Sleep -Seconds 5
    } while ([DateTime]::UtcNow -lt $deadline)

    $reason = (& kubectl get pod $podName -n $namespace -o jsonpath='{.status.reason}' 2>$null).Trim()
    if ($phase -ne 'Succeeded') {
        if ([string]::IsNullOrWhiteSpace($phase)) { $phase = 'Unknown' }
        throw "Harbor pull test did not succeed (phase=$phase reason=$reason)."
    }
    $status = 'pass'
    $verified = $true
    Write-Evidence
    Write-Output "Harbor clean-node pull PASS: digest verified on node $NodeName."
}
catch {
    $failure = $_.Exception.Message -replace '(?i)(password|token|secret|sas)[^;\r\n]*', '$1=[redacted]'
    Write-Evidence
    throw
}
finally {
    & kubectl delete namespace $namespace --ignore-not-found --wait=false *> $null
}
