[CmdletBinding()]
param(
    [string]$RepositoryRoot = (Get-Location).Path
)

$ErrorActionPreference = 'Stop'
$prod = Join-Path $RepositoryRoot 'k8s/overlays/prod'
$rendered = kubectl kustomize $prod --load-restrictor LoadRestrictionsNone 2>&1
if ($LASTEXITCODE -ne 0) {
    throw "Unable to render production manifests. Run with the repository's approved Kustomize load policy and fix the render errors before deployment."
}

$forbidden = @('cG9zdGdyZXM=', 'cmVkaXM=', 'cmFiYml0bXE=', 'PLACEHOLDER', 'change-me')
foreach ($value in $forbidden) {
    if ($rendered -match [regex]::Escape($value)) {
        throw "Production manifest contains placeholder/default secret marker '$value'. Inject database/cache/messaging secrets from Vault or an approved secret manager."
    }
}

$forbiddenPatterns = @(
    'Vault__Token',
    'VAULT_TOKEN',
    'Vault__RoleId',
    'Vault__SecretId',
    'vault\.hashicorp\.com/role:\s*["'']?default'
    # Static accounts are allowed for frontend-only pods; backend deployment
    # identity is enforced by the production Kustomize patch.
)
foreach ($pattern in $forbiddenPatterns) {
    if ($rendered -match $pattern) {
        throw "Production manifest contains forbidden static-credential or disabled-workload-identity pattern '$pattern'."
    }
}

Write-Output 'Production secret gate passed: no known placeholder/default secret markers found.'
