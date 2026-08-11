$ErrorActionPreference = 'Stop'

$repositoryRoot = Resolve-Path (Join-Path $PSScriptRoot '..\..')
$workflowPath = Join-Path $repositoryRoot '.github\workflows\container-release.yml'
$workflow = Get-Content -LiteralPath $workflowPath -Raw

if ($workflow -notmatch '(?ms)scan-type:\s*fs.*?scan-ref:\s*\..*?skip-dirs:\s*k8s,docker/spire') {
    throw 'The filesystem Trivy preflight must exclude k8s and docker/spire. Rendered production manifests are checked by dedicated release/admission validators; docker/spire contains non-release Compose helpers. Release images remain scanned after build.'
}

Write-Host 'Container release Trivy scope PASS: raw Kubernetes manifests and non-release SPIRE Compose helpers are excluded from the filesystem preflight.'
