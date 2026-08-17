[CmdletBinding()]
param([switch]$RequireTools)
$ErrorActionPreference = 'Stop'
$root = (Resolve-Path "$PSScriptRoot\..\..").Path
function Invoke-AdapterScript([string]$Path, [string[]]$Arguments) {
    $output = & pwsh -NoProfile -File $Path @Arguments 2>&1
    $output | ForEach-Object { Write-Output $_ }
    if ($LASTEXITCODE -ne 0) { throw "Adapter failed: $Path (exit $LASTEXITCODE)" }
}

Invoke-AdapterScript (Join-Path $root 'scripts\config\validate-admin-identity-capabilities.ps1') @()
Invoke-AdapterScript (Join-Path $root 'scripts\config\validate-identity-workbench-12-parts.ps1') @()
Invoke-AdapterScript (Join-Path $root 'scripts\config\validate-identity-workbench-naming.ps1') @()
Invoke-AdapterScript (Join-Path $root 'scripts\config\validate-identity-live-prerequisites.ps1') @()

Invoke-AdapterScript (Join-Path $root 'scripts\config\validate-runtime-contract.ps1') @('-EnvironmentFile', (Join-Path $root 'config\environments\development.env.example'), '-Runtime', 'docker', '-Strict')
Invoke-AdapterScript (Join-Path $root 'scripts\config\validate-vm-runtime.ps1') @('-EnvironmentFile', (Join-Path $root 'deploy\vm\runtime.env.example'))

$kubectl = Get-Command kubectl -ErrorAction SilentlyContinue
if ($kubectl) {
    Invoke-AdapterScript (Join-Path $root 'scripts\config\validate-kustomize-runtime.ps1') @('-Overlay', 'dev')
} elseif ($RequireTools) {
    throw 'kubectl is required but unavailable.'
} else {
    Write-Output 'ENVIRONMENT_BLOCKED kubectl unavailable; Kustomize validation skipped.'
}

$docker = Get-Command docker -ErrorAction SilentlyContinue
if ($docker) {
    Invoke-AdapterScript (Join-Path $root 'scripts\config\validate-compose-stack.ps1') @('-ComposeFile', (Join-Path $root 'docker\docker-compose.yml'), '-EnvironmentFile', (Join-Path $root 'config\environments\development.env.example'))
} elseif ($RequireTools) {
    throw 'Docker is required but unavailable.'
} else {
    Write-Output 'ENVIRONMENT_BLOCKED docker unavailable; Compose validation skipped.'
}
Write-Output 'ALL_RUNTIME_ADAPTERS_VALIDATED'
