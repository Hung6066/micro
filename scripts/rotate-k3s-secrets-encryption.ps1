[CmdletBinding()]
param(
    [ValidateSet('staging','production')][string]$Environment = 'staging',
    [string]$Inventory = 'ansible/enterprise-k3s/inventory/production.yml',
    [string]$Playbook = 'ansible/enterprise-k3s/playbooks/45-rotate-k3s-secrets-encryption.yml',
    [string]$SshKeyPath = '',
    [string]$VaultPasswordPath = '',
    [string]$SnapshotPath = '',
    [string]$OutputPath = 'artifacts/evidence/k3s-secrets-encryption-rotation.json',
    [switch]$Apply,
    [switch]$AllowProduction
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

if ($Environment -eq 'production' -and $Apply -and -not $AllowProduction) {
    throw 'Production encryption rotation requires -AllowProduction from the protected workflow.'
}
if (-not (Test-Path -LiteralPath $Inventory -PathType Leaf)) { throw "Inventory not found: $Inventory" }
if (-not (Test-Path -LiteralPath $Playbook -PathType Leaf)) { throw "Playbook not found: $Playbook" }

$output = [IO.Path]::GetFullPath($OutputPath)
$parent = Split-Path -Parent $output
if ($parent) { New-Item -ItemType Directory -Force -Path $parent | Out-Null }
$started = [DateTime]::UtcNow
$status = if ($Apply) { 'fail' } else { 'skipped' }
$failure = $null

if (-not $Apply) {
    [IO.File]::WriteAllText($output, (@{ status=$status; executedAtUtc=$started.ToString('o'); rotationVerified=$false; failure=$null } | ConvertTo-Json), [Text.UTF8Encoding]::new($false))
    Write-Output 'K3s secrets-encryption rotation DRY-RUN: no host or cluster mutation performed.'
    exit 0
}

if ([string]::IsNullOrWhiteSpace($SshKeyPath) -or -not (Test-Path -LiteralPath $SshKeyPath -PathType Leaf)) { throw 'SshKeyPath is required for apply.' }
if ([string]::IsNullOrWhiteSpace($VaultPasswordPath) -or -not (Test-Path -LiteralPath $VaultPasswordPath -PathType Leaf)) { throw 'VaultPasswordPath is required for apply.' }
if ([string]::IsNullOrWhiteSpace($SnapshotPath)) { throw 'SnapshotPath is required for apply.' }
$env:ANSIBLE_PRIVATE_KEY_FILE = (Resolve-Path -LiteralPath $SshKeyPath).Path
$env:ANSIBLE_VAULT_PASSWORD_FILE = (Resolve-Path -LiteralPath $VaultPasswordPath).Path
$env:K3S_SECRETS_ROTATION_APPROVED = 'true'
$args = @('-i', $Inventory, $Playbook, '-e', 'k3s_secrets_rotation_approved=true', '-e', "k3s_rotation_snapshot_path=$SnapshotPath", '-e', "ansible_ssh_private_key_file=$((Resolve-Path -LiteralPath $SshKeyPath).Path)")
try {
    & ansible-playbook @args
    if ($LASTEXITCODE -ne 0) { throw "ansible-playbook exited with $LASTEXITCODE" }
    $status = 'pass'
    [IO.File]::WriteAllText($output, (@{ status=$status; executedAtUtc=$started.ToString('o'); rotationVerified=$true; failure=$null } | ConvertTo-Json), [Text.UTF8Encoding]::new($false))
    Write-Output 'K3s secrets-encryption rotation PASS: keys rotated, existing Secrets re-encrypted, and API readiness verified.'
}
catch {
    $failure = $_.Exception.Message -replace '(?i)(password|token|secret|sas|client[_-]?secret)[^;\r\n]*', '$1=[redacted]'
    [IO.File]::WriteAllText($output, (@{ status='fail'; executedAtUtc=$started.ToString('o'); rotationVerified=$false; failure=$failure } | ConvertTo-Json), [Text.UTF8Encoding]::new($false))
    throw
}
