[CmdletBinding()]
param(
    [ValidateSet('staging', 'production')][string]$Environment = 'staging',
    [Parameter(Mandatory)][string]$Inventory,
    [Parameter(Mandatory)][string]$RebuildPlaybook,
    [Parameter(Mandatory)][string]$SnapshotPath,
    [Parameter(Mandatory)][string]$Kubeconfig,
    [Parameter(Mandatory)][string]$SshKeyPath,
    [Parameter(Mandatory)][string]$VaultPasswordPath,
    [double]$RpoMinutes = 0,
    [string]$OutputPath = 'artifacts/evidence/control-plane-rebuild-drill.json',
    [switch]$Apply,
    [switch]$AllowProduction,
    [switch]$WhatIf
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

if ($Environment -eq 'production' -and (-not $AllowProduction -or -not $Apply)) {
    throw 'Production control-plane rebuild is blocked unless both -Apply and -AllowProduction are supplied by the protected workflow.'
}
foreach ($path in @($Inventory, $RebuildPlaybook, $Kubeconfig, $SshKeyPath, $VaultPasswordPath)) {
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { throw "Required drill input is missing: $path" }
}
if ([string]::IsNullOrWhiteSpace($SnapshotPath) -or $SnapshotPath -match '(?i)(token|password|secret|sas)') { throw 'SnapshotPath must be a reviewed host path and must not contain credentials.' }
if ($RpoMinutes -lt 0 -or [double]::IsNaN($RpoMinutes) -or [double]::IsInfinity($RpoMinutes)) { throw 'RpoMinutes must be a finite non-negative number.' }

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
        rpoMinutes = $RpoMinutes
        rtoMinutes = [math]::Round($rto, 3)
        restoreVerified = $verified
        target = "$Environment/control-plane"
        recoveryMode = 'embedded-etcd-snapshot-cluster-reset'
        sourcePlaybook = [IO.Path]::GetFileName($RebuildPlaybook)
        failure = $failure
    }
    [IO.File]::WriteAllText($output, ($doc | ConvertTo-Json -Depth 6), [Text.UTF8Encoding]::new($false))
}

if ($WhatIf -or -not $Apply) {
    $status = 'skipped'
    Write-Evidence
    Write-Output 'DRILL DRY-RUN: the reviewed Ansible rebuild playbook would reset one control-plane member serially and verify all nodes.'
    exit 0
}

$env:KUBECONFIG = (Resolve-Path -LiteralPath $Kubeconfig).Path
$ansibleArgs = @(
    '-i', $Inventory,
    $RebuildPlaybook,
    '--private-key', $SshKeyPath,
    '--vault-password-file', $VaultPasswordPath,
    '-e', 'control_plane_rebuild_drill_approved=true',
    '-e', "control_plane_rebuild_environment=$Environment",
    '-e', "control_plane_snapshot_path=$SnapshotPath",
    '-e', "control_plane_allow_production=$([bool]$AllowProduction)"
)
try {
    & ansible-playbook @ansibleArgs *> $null
    if ($LASTEXITCODE -ne 0) { throw "Ansible control-plane rebuild playbook failed with exit code $LASTEXITCODE." }
    $readyz = & kubectl --kubeconfig $env:KUBECONFIG get --raw=/readyz --request-timeout=20s 2>$null
    if ($LASTEXITCODE -ne 0 -or $readyz -notmatch 'ok') { throw 'Kubernetes API did not report ready after the rebuild.' }
    $nodes = & kubectl --kubeconfig $env:KUBECONFIG get nodes --no-headers --request-timeout=20s 2>$null
    if ($LASTEXITCODE -ne 0) { throw 'Unable to inventory nodes after the rebuild.' }
    $notReady = @($nodes | Where-Object { $_ -notmatch '\sReady\s' })
    if ($notReady.Count -gt 0) { throw "Nodes remained not-ready after rebuild: $($notReady.Count)." }
    $status = 'pass'
    $verified = $true
    Write-Evidence
    Write-Output 'Control-plane rebuild drill PASS: API ready and all reported nodes Ready.'
}
catch {
    $failure = $_.Exception.Message -replace '(?i)(password|token|secret|sas|private[_-]?key)[^;\r\n]*', '$1=[redacted]'
    Write-Evidence
    throw
}
