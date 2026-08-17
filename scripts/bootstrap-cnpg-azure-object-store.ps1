[CmdletBinding()]
param(
    [string]$Context,
    [string]$EnvFile = 'D:\secure\his-hope\azure-production.env',
    [string]$Overlay = 'k8s/overlays/prod-spire-azure-shared-storage',
    [ValidateSet('production')][string]$Environment = 'production',
    [switch]$Apply,
    [switch]$AllowProduction
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

if ($Apply -and -not $AllowProduction) {
    throw 'Production Azure CNPG apply is blocked by default; rerun with -AllowProduction after change approval.'
}

if ([string]::IsNullOrWhiteSpace($Context)) {
    throw 'The production kube-context is required: -Context <context-name>.'
}
if (-not (Test-Path -LiteralPath $EnvFile)) { throw "Azure env file not found: $EnvFile" }

$values = @{}
foreach ($line in Get-Content -LiteralPath $EnvFile) {
    $trimmed = $line.Trim()
    if ([string]::IsNullOrWhiteSpace($trimmed) -or $trimmed.StartsWith('#')) { continue }
    if ($trimmed -notmatch '^([A-Z0-9_]+)=(.*)$') { throw 'Invalid Azure env file line format.' }
    $values[$Matches[1]] = $Matches[2]
}

foreach ($key in @('AZURE_STORAGE_ACCOUNT', 'AZURE_STORAGE_CONTAINER', 'AZURE_STORAGE_ENDPOINT', 'AZURE_STORAGE_SAS_TOKEN', 'AZURE_BACKUP_PREFIX')) {
    if (-not $values.ContainsKey($key) -or [string]::IsNullOrWhiteSpace($values[$key]) -or $values[$key] -match 'REPLACE_ME|<[^>]+>') {
        throw "Missing or placeholder Azure value: $key"
    }
}

$endpoint = $values['AZURE_STORAGE_ENDPOINT'].TrimEnd('/')
$account = $values['AZURE_STORAGE_ACCOUNT'].Trim()
$container = $values['AZURE_STORAGE_CONTAINER'].Trim()
$sas = $values['AZURE_STORAGE_SAS_TOKEN'].Trim().TrimStart('?')
$prefix = $values['AZURE_BACKUP_PREFIX'].Trim().Trim('/')
$uri = [uri]$endpoint
if ($uri.Scheme -ne 'https' -or $uri.Host -ne "$account.blob.core.windows.net" -or $uri.AbsolutePath -ne '/' -or -not [string]::IsNullOrEmpty($uri.Query)) {
    throw 'AZURE_STORAGE_ENDPOINT must be https://<account>.blob.core.windows.net with no path or query.'
}

$sasParams = @{}
foreach ($part in ($sas -split '&')) {
    if ($part -match '^([^=]+)=(.*)$') { $sasParams[$Matches[1]] = $Matches[2] }
}
$permissions = [uri]::UnescapeDataString([string]$sasParams['sp'])
foreach ($requiredPermission in @('r', 'a', 'c', 'w', 'l')) {
    if (-not $permissions.Contains($requiredPermission)) { throw "SAS is missing required permission: $requiredPermission" }
}
if ($sasParams['sr'] -ne 'c') { throw 'SAS must be scoped to a container (sr=c).' }

$current = (& kubectl config current-context).Trim()
if ($LASTEXITCODE -ne 0) { throw 'Unable to read current kube-context.' }
if ($current -ne $Context) { throw "Current kube-context '$current' does not match requested production context '$Context'." }

$secretYaml = @"
apiVersion: v1
kind: Secret
metadata:
  name: spire-postgres-azure-backup-credentials
  namespace: spire
  labels:
    app.kubernetes.io/name: spire-postgres-azure-backup-credentials
    app.kubernetes.io/component: backup-credentials
    app.kubernetes.io/part-of: his-hope
    cnpg.io/reload: ""
type: Opaque
data:
  AZURE_STORAGE_ACCOUNT: $([Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($account)))
  AZURE_STORAGE_SAS_TOKEN: $([Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($sas)))
"@
$destination = "$endpoint/$container/$prefix"
$rendered = (& kubectl kustomize --load-restrictor LoadRestrictionsNone $Overlay) -join "`n"
if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($rendered)) { throw 'Unable to render Azure CNPG overlay.' }
$rendered = $rendered.Replace('https://REPLACE_ME.blob.core.windows.net/REPLACE_ME/his-hope/production/spire-postgres-v2', $destination)
$rendered = $rendered.Replace('https://REPLACE_ME.blob.core.windows.net/REPLACE_ME/his-hope/production/spire-postgres', $destination)
if ($rendered.Contains('REPLACE_ME')) { throw 'Rendered Azure overlay still contains a placeholder.' }

if (-not $Apply) {
    Write-Output "DRY-RUN: Azure CNPG destination and SAS contract validated for context '$Context'; no Secret or ObjectStore was applied."
    exit 0
}

$secretYaml | kubectl --context $Context apply -f - | Out-Null
if ($LASTEXITCODE -ne 0) { throw 'Unable to apply Azure backup Secret.' }

$rendered | kubectl --context $Context apply -f -
if ($LASTEXITCODE -ne 0) { throw 'Unable to apply Azure CNPG overlay.' }

$objectStore = & kubectl --context $Context get objectstore spire-postgres-azure-store -n spire -o jsonpath='{.metadata.name}'
if ($LASTEXITCODE -ne 0 -or $objectStore -ne 'spire-postgres-azure-store') { throw 'Azure ObjectStore was not created.' }

Write-Output 'Azure CNPG ObjectStore applied and Ready. Run scripts/validate-cnpg-backup-platform.ps1 -RunBackup against the same context before enabling restore gates.'
