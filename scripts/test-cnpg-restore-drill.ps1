[CmdletBinding()]
param(
    [ValidateSet('staging', 'production')][string]$Environment = 'staging',
    [string]$Kubeconfig = 'artifacts/kubeconfig-staging.yaml',
    [Parameter(Mandatory)][string]$RestoreManifest,
    [Parameter(Mandatory)][string]$TargetNamespace,
    [double]$RpoMinutes = 0,
    [string]$OutputPath = 'artifacts/evidence/database-restore-drill.json',
    [switch]$AllowProduction,
    [switch]$WhatIf
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

if ($Environment -eq 'production' -and -not $AllowProduction -and -not $WhatIf) {
    throw 'Production CNPG restore is blocked by default; use the protected workflow with -AllowProduction.'
}
if (-not (Test-Path -LiteralPath $Kubeconfig -PathType Leaf)) { throw "Kubeconfig not found: $Kubeconfig" }
if (-not (Test-Path -LiteralPath $RestoreManifest -PathType Leaf)) { throw "Restore manifest not found: $RestoreManifest" }
if ($TargetNamespace -in @('spire', 'his-hope', 'his-hope-prod', 'default', 'kube-system')) {
    throw "TargetNamespace '$TargetNamespace' is not an isolated restore namespace."
}
if ($RpoMinutes -lt 0 -or [double]::IsNaN($RpoMinutes) -or [double]::IsInfinity($RpoMinutes)) {
    throw 'RpoMinutes must be a finite non-negative number.'
}

$output = [IO.Path]::GetFullPath($OutputPath)
$parent = Split-Path -Parent $output
if ($parent) { New-Item -ItemType Directory -Force -Path $parent | Out-Null }
$startedAt = [DateTime]::UtcNow
$status = 'fail'
$verified = $false
$clusterName = $null
$failure = $null

function Write-Evidence {
    $rto = if ($verified) { ([DateTime]::UtcNow - $startedAt).TotalMinutes } else { 0 }
    $doc = [pscustomobject]@{
        status = $status
        executedAtUtc = $startedAt.ToString('o')
        rpoMinutes = $RpoMinutes
        rtoMinutes = [math]::Round($rto, 3)
        restoreVerified = $verified
        target = "$Environment/$TargetNamespace"
        cluster = $clusterName
        failure = $failure
    }
    [IO.File]::WriteAllText($output, ($doc | ConvertTo-Json -Depth 6), [Text.UTF8Encoding]::new($false))
}

if ($WhatIf) {
    $status = 'skipped'
    Write-Evidence
    Write-Output "DRILL DRY-RUN: CNPG restore manifest would be applied only to isolated namespace $TargetNamespace."
    exit 0
}

$env:KUBECONFIG = (Resolve-Path -LiteralPath $Kubeconfig).Path
try {
    $manifestText = Get-Content -LiteralPath $RestoreManifest -Raw
    if ($manifestText -match '(?im)^\s*namespace:\s*(spire|his-hope|his-hope-prod)\s*$') {
        throw 'Restore manifest targets a production namespace; refusing in-place restore.'
    }

    $namespaceYaml = @"
apiVersion: v1
kind: Namespace
metadata:
  name: $TargetNamespace
  labels:
    his-hope.io/dr-target: "true"
    pod-security.kubernetes.io/enforce: restricted
"@
    $namespaceYaml | & kubectl apply --server-side --field-manager=his-hope-cnpg-drill -f - | Out-Null
    if ($LASTEXITCODE -ne 0) { throw 'Unable to create isolated restore namespace.' }
    Get-Content -LiteralPath $RestoreManifest -Raw | & kubectl apply --server-side --field-manager=his-hope-cnpg-drill -f - | Out-Null
    if ($LASTEXITCODE -ne 0) { throw 'Unable to apply reviewed CNPG restore manifest.' }

    $deadline = [DateTime]::UtcNow.AddMinutes(20)
    do {
        $clusterName = (& kubectl get cluster -n $TargetNamespace -o jsonpath='{.items[0].metadata.name}' 2>$null).Trim()
        if (-not [string]::IsNullOrWhiteSpace($clusterName)) {
            $phase = (& kubectl get cluster $clusterName -n $TargetNamespace -o jsonpath='{.status.phase}' 2>$null).Trim()
            if ($phase -match 'healthy|Healthy|Cluster in healthy state') { break }
        }
        Start-Sleep -Seconds 10
    } while ([DateTime]::UtcNow -lt $deadline)
    if ([string]::IsNullOrWhiteSpace($clusterName) -or $phase -notmatch 'healthy|Healthy|Cluster in healthy state') {
        throw "CNPG restore cluster did not become healthy before timeout (phase=$phase)."
    }

    $primaryPod = (& kubectl get pods -n $TargetNamespace -l 'cnpg.io/instanceRole=primary' -o jsonpath='{.items[0].metadata.name}' 2>$null).Trim()
    if ([string]::IsNullOrWhiteSpace($primaryPod)) {
        $primaryPod = (& kubectl get pods -n $TargetNamespace -l role=primary -o jsonpath='{.items[0].metadata.name}' 2>$null).Trim()
    }
    if ([string]::IsNullOrWhiteSpace($primaryPod)) { throw 'Restored CNPG primary pod was not found.' }
    $probe = (& kubectl exec $primaryPod -n $TargetNamespace -- psql -U postgres -d postgres -tAc 'SELECT 1' 2>$null).Trim()
    if ($LASTEXITCODE -ne 0 -or $probe -ne '1') { throw 'Restored PostgreSQL smoke query did not return 1.' }

    $status = 'pass'
    $verified = $true
    Write-Evidence
    Write-Output "CNPG restore drill PASS: cluster=$clusterName, smoke=SELECT 1."
}
catch {
    $failure = $_.Exception.Message -replace '(?i)(password|token|secret|sas|client[_-]?secret)[^;\r\n]*', '$1=[redacted]'
    Write-Evidence
    throw
}
finally {
    & kubectl delete namespace $TargetNamespace --ignore-not-found --wait=false *> $null
}
