[CmdletBinding()]
param(
    [switch] $Ci,
    [switch] $Staged
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
Set-Location $repoRoot

function Invoke-Checked([string] $Command, [string[]] $Arguments) {
    Write-Host "> $Command $($Arguments -join ' ')"
    & $Command @Arguments
    if ($LASTEXITCODE -ne 0) { throw "Policy gate failed: $Command $($Arguments -join ' ')" }
}

$changed = @()
if ($Staged) {
    $changed = @(git diff --cached --name-only)
} elseif (-not $Ci) {
    $changed = @(git diff --name-only HEAD)
}

# CI runs the full baseline. Local hooks run only gates relevant to staged files.
$frontendChanged = $Ci -or ($changed | Where-Object { $_ -match '^(shared/frontend-foundation|admin-app|dashboard-app|src/Frontend/his-hope-app|scripts/validate-design-tokens\.mjs|package\.json)' }).Count -gt 0
$backendChanged = $Ci -or ($changed | Where-Object { $_ -match '^(src/Services|src/Shared|src/ApiGateway|src/Bff|tests/|His\.Hope\.sln|scripts/validate-api-platform-conventions\.ps1)' }).Count -gt 0

if ($frontendChanged) {
    Invoke-Checked 'npm' @('run', 'validate:foundation')
    Invoke-Checked 'npm' @('run', 'lint:design-tokens')
    if ($Ci) {
        Invoke-Checked 'npm' @('run', 'build:shared')
        Invoke-Checked 'npm' @('--workspace', 'admin-app', 'run', 'build')
        Invoke-Checked 'npm' @('--workspace', 'dashboard-app', 'run', 'build')
        Invoke-Checked 'npm' @('--workspace', 'his-hope-app', 'run', 'build')
    }
}

if ($backendChanged) {
    Invoke-Checked 'powershell' @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', 'scripts/validate-api-platform-conventions.ps1')
    if ($Ci) {
        Invoke-Checked 'dotnet' @('restore', 'His.Hope.sln', '-warnAsError:NU1605', '-warnAsError:NU1901', '-warnAsError:NU1902', '-warnAsError:NU1903', '-warnAsError:NU1904')
        Invoke-Checked 'dotnet' @('build', 'His.Hope.sln', '--no-restore', '--warnaserror:NU1605', '--warnaserror:NU1901', '--warnaserror:NU1902', '--warnaserror:NU1903', '--warnaserror:NU1904')
    }
}

Write-Host 'His.Hope policy gate passed.'
