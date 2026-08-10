[CmdletBinding()]
param(
    [string]$Path = 'k8s/gitops/bootstrap',
    [string]$OutputPath
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$rendered = & kubectl kustomize $Path --load-restrictor LoadRestrictionsNone 2>&1
if ($LASTEXITCODE -ne 0) { throw "Argo CD bootstrap render failed: $Path" }
$text = $rendered -join "`n"
$checks = [System.Collections.Generic.List[object]]::new()
function Add-Check([string]$Name, [ValidateSet('pass','fail')][string]$Status, [string]$Detail) { $checks.Add([pscustomobject]@{name=$Name;status=$Status;detail=$Detail}) }

if ($text -match 'kind:\s*AppProject' -and $text -match 'name:\s*his-hope') { Add-Check 'project-boundary' 'pass' 'His.Hope AppProject is rendered.' }
else { Add-Check 'project-boundary' 'fail' 'His.Hope AppProject is missing.' }

if ($text -match '(?ms)namespaceResourceWhitelist:\s*\r?\n\s*-\s*group:\s*["'']?\*["'']?\s*\r?\n\s*kind:\s*["'']?\*["'']?') {
    Add-Check 'project-least-privilege' 'fail' 'AppProject namespaceResourceWhitelist contains a wildcard.'
} else {
    $requiredKinds = @('Deployment', 'StatefulSet', 'DaemonSet', 'Service', 'NetworkPolicy', 'Role', 'RoleBinding', 'SecretProviderClass')
    $missingKinds = @($requiredKinds | Where-Object { $text -notmatch "(?m)^\s*kind:\s*$([regex]::Escape($_))\s*$" })
    if ($missingKinds.Count -eq 0) { Add-Check 'project-least-privilege' 'pass' 'AppProject uses an explicit namespaced kind allow-list.' }
    else { Add-Check 'project-least-privilege' 'fail' "AppProject allow-list is missing: $($missingKinds -join ', ')." }
}

$applicationCount = ([regex]::Matches($text, '(?m)^kind:\s*Application\s*$')).Count
$retryCount = ([regex]::Matches($text, '(?m)^\s*retry:\s*\r?$')).Count
if ($applicationCount -gt 0 -and $retryCount -eq $applicationCount) { Add-Check 'application-retry' 'pass' "$applicationCount Applications have retry policy." }
else { Add-Check 'application-retry' 'fail' "Applications=$applicationCount; retry policies=$retryCount." }
$sharedResourceCount = ([regex]::Matches($text, '(?m)^\s*-\s*FailOnSharedResource=true\s*$')).Count
if ($applicationCount -gt 0 -and $sharedResourceCount -eq $applicationCount) { Add-Check 'shared-resource-guard' 'pass' "$applicationCount Applications fail when a resource is owned by another Application." }
else { Add-Check 'shared-resource-guard' 'fail' "Applications=$applicationCount; FailOnSharedResource options=$sharedResourceCount." }

foreach ($key in @(
        'resource.customizations.health.apps_Deployment',
        'resource.customizations.health.batch_Job',
        'resource.customizations.health.v1_Service',
        'resource.customizations.health.policy.linkerd.io_Server',
        'resource.customizations.health.monitoring.coreos.com_PrometheusRule',
        'resource.customizations.health.monitoring.coreos.com_ServiceMonitor',
        'resource.customizations.health.secrets-store.csi.x-k8s.io_SecretProviderClass')) {
    if ($text -match [regex]::Escape($key)) { Add-Check "health-$key" 'pass' "$key configured." }
    else { Add-Check "health-$key" 'fail' "$key is missing." }
}

$production = @($text -split '(?m)^---\s*$' | Where-Object { $_ -match '(?m)^\s*name:\s*his-hope-production\s*$' } | Select-Object -First 1)
if ($production.Count -eq 0) { Add-Check 'production-manual-sync' 'fail' 'Production Application is missing.' }
elseif ($production[0] -match '(?m)^\s*automated:\s*$') {
    $approved = $production[0] -match '(?m)^\s*his-hope\.io/auto-sync-approved:\s*["'']?true["'']?\s*$'
    $revision = [regex]::Match($production[0], '(?m)^\s*targetRevision:\s*(?<revision>[^\s]+)\s*$').Groups['revision'].Value
    if ($approved -and $revision -and $revision -ne 'main') {
        Add-Check 'production-branch-auto-sync' 'pass' "Production auto-sync is explicitly approved for reviewed branch '$revision'."
    } else {
        Add-Check 'production-branch-auto-sync' 'fail' 'Automated production sync requires auto-sync-approved=true and a non-main reviewed branch.'
    }
} else { Add-Check 'production-branch-auto-sync' 'pass' 'Production Application is not automated; manual sync remains fail-closed.' }

$failed = @($checks | Where-Object status -eq 'fail')
$status = if ($failed.Count -gt 0) { 'fail' } else { 'pass' }
$result = [pscustomobject]@{status=$status; checks=@($checks); generatedAtUtc=[DateTime]::UtcNow.ToString('o')}
$json = $result | ConvertTo-Json -Depth 8
if ($OutputPath) {
    $dir=Split-Path -Parent $OutputPath
    if($dir){New-Item -ItemType Directory -Force -Path $dir|Out-Null}
    [IO.File]::WriteAllText([IO.Path]::GetFullPath($OutputPath),$json,[Text.UTF8Encoding]::new($false))
}
Write-Output $json
if($status -eq 'fail'){exit 30}
exit 0
