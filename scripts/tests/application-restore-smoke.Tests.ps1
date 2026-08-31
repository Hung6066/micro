$ErrorActionPreference = 'Stop'

$repositoryRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$scriptPath = Join-Path $repositoryRoot 'scripts\test-application-restore-smoke.ps1'
$workflowPath = Join-Path $repositoryRoot '.github\workflows\application-restore-smoke.yml'
$script = Get-Content -LiteralPath $scriptPath -Raw
$workflow = Get-Content -LiteralPath $workflowPath -Raw

if ($script -match '\?\?') {
    throw 'Application restore smoke script must remain compatible with Windows PowerShell; null-coalescing syntax is not allowed.'
}
foreach ($required in @(
    'availableReplicas',
    'desiredReplicas',
    'deployment-readiness',
    'authenticated-api-smoke',
    'authorization-negative'
)) {
    if (-not $script.Contains($required)) {
        throw "Application restore smoke script is missing required contract: $required"
    }
}
if (-not $workflow.Contains('test-application-restore-smoke.ps1')) {
    throw 'Application restore workflow must invoke the restore smoke script.'
}

Write-Output 'Application restore smoke contract: PASS'
