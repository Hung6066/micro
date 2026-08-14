[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][ValidatePattern('^[0-9a-f]{40}$')][string]$ExpectedRevision,
    [string]$Kubeconfig = 'artifacts/kubeconfig-production.yaml',
    [string[]]$ApplicationName = @(
        'his-hope-data-plane',
        'his-hope-system-plane',
        'his-hope-production-ha',
        'his-hope-security-boundaries',
        'his-hope-staging',
        'his-hope-production-policies',
        'his-hope-signature-policy',
        'his-hope-observability-production',
        'his-hope-production'
    ),
    [switch]$RequireSynced,
    [string]$ExpectedRepoUrl = 'https://git-mirror.his-hope.local/gitops-admin/micro.git',
    [string]$OutputPath
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

if (-not (Test-Path -LiteralPath $Kubeconfig -PathType Leaf)) {
    throw "Kubeconfig was not found: $Kubeconfig"
}

function Invoke-Kubectl {
    param([string[]]$Arguments)
    $output = & kubectl --kubeconfig $Kubeconfig @Arguments 2>&1
    if ($LASTEXITCODE -ne 0) {
        throw ($output -join "`n")
    }
    return $output -join "`n"
}

$checks = [System.Collections.Generic.List[object]]::new()
$diagnostics = [System.Collections.Generic.List[object]]::new()
foreach ($name in $ApplicationName) {
    $application = (Invoke-Kubectl @('get', 'application', $name, '-n', 'argocd', '-o', 'json')) | ConvertFrom-Json
    $targetRevision = [string]$application.spec.source.targetRevision
    if ([string]::IsNullOrWhiteSpace($targetRevision)) {
        throw "Argo CD application $name has no source.targetRevision"
    }
    if ($targetRevision -notmatch '^[A-Za-z0-9][A-Za-z0-9._/-]*$') {
        throw "Argo CD application $name has an unsupported targetRevision: $targetRevision"
    }

    $mirrorRevision = (Invoke-Kubectl @('-n', 'git-mirror', 'exec', 'deployment/gitea', '--', 'git', '--git-dir=/var/lib/gitea/git/repositories/gitops-admin/micro.git', 'rev-parse', "refs/heads/$targetRevision")).Trim()
    $checks.Add([pscustomobject]@{ application = $name; name = 'mirror-target-revision'; pass = $mirrorRevision -eq $ExpectedRevision; actual = $mirrorRevision })
    $checks.Add([pscustomobject]@{ application = $name; name = 'argocd-source'; pass = $application.spec.source.repoURL -eq $ExpectedRepoUrl; actual = $application.spec.source.repoURL })
    $checks.Add([pscustomobject]@{ application = $name; name = 'argocd-revision'; pass = $application.status.sync.revision -eq $ExpectedRevision; actual = $application.status.sync.revision })
    if ($RequireSynced) {
        $checks.Add([pscustomobject]@{ application = $name; name = 'argocd-synced'; pass = $application.status.sync.status -eq 'Synced'; actual = $application.status.sync.status })
    }
    $operationMessage = if ($application.status.PSObject.Properties.Name -contains 'operationState') {
        [string]$application.status.operationState.message
    } else {
        ''
    }
    $conditions = if ($application.status.PSObject.Properties.Name -contains 'conditions') {
        @($application.status.conditions | ForEach-Object {
            [pscustomobject]@{ type = [string]$_.type; message = [string]$_.message }
        })
    } else {
        @()
    }
    $outOfSyncResources = if ($application.status.PSObject.Properties.Name -contains 'resources') {
        @($application.status.resources | Where-Object { $_.status -eq 'OutOfSync' } | ForEach-Object {
            $healthStatus = if ($_.PSObject.Properties.Name -contains 'health') { [string]$_.health.status } else { '' }
            $healthMessage = if ($_.PSObject.Properties.Name -contains 'health') { [string]$_.health.message } else { '' }
            [pscustomobject]@{
                group = [string]$_.group
                kind = [string]$_.kind
                namespace = [string]$_.namespace
                name = [string]$_.name
                health = $healthStatus
                message = $healthMessage
            }
        })
    } else {
        @()
    }
    $syncResultResources = if ($application.status.PSObject.Properties.Name -contains 'operationState' -and
        $application.status.operationState.PSObject.Properties.Name -contains 'syncResult' -and
        $application.status.operationState.syncResult.PSObject.Properties.Name -contains 'resources') {
        @($application.status.operationState.syncResult.resources | ForEach-Object {
            [pscustomobject]@{
                group = [string]$_.group
                kind = [string]$_.kind
                namespace = [string]$_.namespace
                name = [string]$_.name
                status = [string]$_.status
                message = [string]$_.message
                hookType = [string]$_.hookType
            }
        })
    } else {
        @()
    }
    $diagnostics.Add([pscustomobject]@{
        application = $name
        syncStatus = [string]$application.status.sync.status
        operationMessage = $operationMessage
        conditions = $conditions
        outOfSyncResources = $outOfSyncResources
        syncResultResources = $syncResultResources
    })
}

$result = [pscustomobject]@{
    expectedRevision = $ExpectedRevision
    applications = @($ApplicationName)
    checks = $checks
    diagnostics = $diagnostics
    status = if (($checks | Where-Object { -not $_.pass }).Count -eq 0) { 'pass' } else { 'fail' }
}
$json = $result | ConvertTo-Json -Depth 5
if ($OutputPath) {
    $directory = Split-Path -Parent $OutputPath
    if ($directory) { New-Item -ItemType Directory -Force -Path $directory | Out-Null }
    [IO.File]::WriteAllText([IO.Path]::GetFullPath($OutputPath), $json, [Text.UTF8Encoding]::new($false))
}
Write-Output $json
if ($result.status -ne 'pass') { exit 1 }
