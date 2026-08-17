[CmdletBinding()]
param(
    [ValidateSet('staging', 'production')][string]$Environment = 'staging',
    [string]$Kubeconfig = 'artifacts/kubeconfig-staging.yaml',
    [string]$Namespace = 'his-hope',
    [string]$StatefulSet = 'vault',
    [string]$Pod = 'vault-0',
    [string]$OutputPath = 'artifacts/evidence/vault-recovery-drill.json',
    [switch]$AllowProduction,
    [switch]$WhatIf
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

if ($Environment -eq 'production' -and -not $AllowProduction) {
    throw 'Production Vault recovery drill is blocked by default; use the protected workflow with -AllowProduction.'
}
if (-not (Test-Path -LiteralPath $Kubeconfig -PathType Leaf)) { throw "Kubeconfig not found: $Kubeconfig" }
if ($Pod -notmatch '^vault-[0-9]+$') { throw 'Pod must be a Vault StatefulSet pod name.' }

$output = [IO.Path]::GetFullPath($OutputPath)
$parent = Split-Path -Parent $output
if ($parent) { New-Item -ItemType Directory -Force -Path $parent | Out-Null }
$startedAt = [DateTime]::UtcNow
$status = 'fail'
$verified = $false
$failure = $null

function Write-Evidence {
    $rto = if ($verified) { ([DateTime]::UtcNow - $startedAt).TotalMinutes } else { 0 }
    $doc = [pscustomobject]@{
        status = $status
        executedAtUtc = $startedAt.ToString('o')
        rpoMinutes = 0
        rtoMinutes = [math]::Round($rto, 3)
        restoreVerified = $verified
        target = "$Environment/$Namespace/$Pod"
        recoveryMode = 'raft-member-restart-with-Azure-Key-Vault-auto-unseal'
        failure = $failure
    }
    [IO.File]::WriteAllText($output, ($doc | ConvertTo-Json -Depth 6), [Text.UTF8Encoding]::new($false))
}

if ($WhatIf) {
    $status = 'skipped'
    Write-Evidence
    Write-Output "DRILL DRY-RUN: Vault pod $Pod would be restarted and auto-unseal verified."
    exit 0
}

$env:KUBECONFIG = (Resolve-Path -LiteralPath $Kubeconfig).Path
function Vault-Status {
    $raw = & kubectl exec $Pod -n $Namespace -- sh -c 'VAULT_ADDR=https://127.0.0.1:8200 VAULT_CACERT=/run/tls/ca.crt vault status -format=json' 2>$null
    if ($LASTEXITCODE -ne 0) { throw 'Vault status command failed.' }
    try { return (($raw -join "`n") | ConvertFrom-Json) } catch { throw 'Vault status response was not valid JSON.' }
}

try {
    $before = Vault-Status
    if ($before.initialized -ne $true -or $before.sealed -ne $false) { throw 'Vault was not initialized and unsealed before the drill.' }
    & kubectl delete pod $Pod -n $Namespace --wait=false | Out-Null
    if ($LASTEXITCODE -ne 0) { throw "Unable to restart Vault pod $Pod." }
    & kubectl wait --for=condition=Ready "pod/$Pod" -n $Namespace --timeout=10m | Out-Null
    if ($LASTEXITCODE -ne 0) { throw "Vault pod $Pod did not become Ready after restart." }
    $after = Vault-Status
    if ($after.initialized -ne $true -or $after.sealed -ne $false) { throw 'Vault did not auto-unseal after pod restart.' }
    $status = 'pass'
    $verified = $true
    Write-Evidence
    Write-Output "Vault recovery drill PASS: $Pod restarted and remained initialized/unsealed."
}
catch {
    $failure = $_.Exception.Message -replace '(?i)(password|token|secret|sas|client[_-]?secret)[^;\r\n]*', '$1=[redacted]'
    Write-Evidence
    throw
}
