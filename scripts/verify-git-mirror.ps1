[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][ValidatePattern('^[0-9a-f]{40}$')][string]$ExpectedRevision,
    [string]$Kubeconfig = 'artifacts/kubeconfig-production.yaml',
    [string]$ApplicationName = 'his-hope-production',
    [switch]$RequireSynced,
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

$application = (Invoke-Kubectl @('get', 'application', $ApplicationName, '-n', 'argocd', '-o', 'json')) | ConvertFrom-Json
$targetRevision = $application.spec.source.targetRevision
if ([string]::IsNullOrWhiteSpace($targetRevision)) {
    throw "Argo CD application $ApplicationName has no source.targetRevision"
}
if ($targetRevision -notmatch '^[A-Za-z0-9][A-Za-z0-9._/-]*$') {
    throw "Argo CD application $ApplicationName has an unsupported targetRevision: $targetRevision"
}

$mirrorRevision = (Invoke-Kubectl @('-n', 'git-mirror', 'exec', 'deployment/gitea', '--', 'git', '--git-dir=/var/lib/gitea/git/repositories/gitops-admin/micro.git', 'rev-parse', "refs/heads/$targetRevision")).Trim()
$checks = @(
    [pscustomobject]@{ name = 'mirror-target-revision'; pass = $mirrorRevision -eq $ExpectedRevision; actual = $mirrorRevision },
    [pscustomobject]@{ name = 'argocd-source'; pass = $application.spec.source.repoURL -eq 'http://gitea.git-mirror.svc.cluster.local:3000/gitops-admin/micro.git'; actual = $application.spec.source.repoURL },
    [pscustomobject]@{ name = 'argocd-revision'; pass = $application.status.sync.revision -eq $ExpectedRevision; actual = $application.status.sync.revision }
)
if ($RequireSynced) {
    $checks += [pscustomobject]@{ name = 'argocd-synced'; pass = $application.status.sync.status -eq 'Synced'; actual = $application.status.sync.status }
}

$result = [pscustomobject]@{
    expectedRevision = $ExpectedRevision
    application = $ApplicationName
    targetRevision = $targetRevision
    checks = $checks
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
