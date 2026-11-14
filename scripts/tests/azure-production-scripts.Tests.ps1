
$ErrorActionPreference = 'Stop'

$vault = Get-Content -LiteralPath (Join-Path $PSScriptRoot '..\bootstrap-vault-azure-unseal.ps1') -Raw
$cnpg = Get-Content -LiteralPath (Join-Path $PSScriptRoot '..\bootstrap-cnpg-azure-object-store.ps1') -Raw
$restorePath = Join-Path $PSScriptRoot '..\verify-production-backup-restore.ps1'
$restore = Get-Content -LiteralPath $restorePath -Raw
$retentionPath = Join-Path $PSScriptRoot '..\validate-azure-blob-retention.py'
$retention = Get-Content -LiteralPath $retentionPath -Raw
$immutabilityPath = Join-Path $PSScriptRoot '..\configure-azure-blob-immutability.ps1'
$immutability = Get-Content -LiteralPath $immutabilityPath -Raw
$cnpgWorkflow = Get-Content -LiteralPath (Join-Path $PSScriptRoot '..\..\.github\workflows\cnpg-azure-backup-bootstrap.yml') -Raw -ErrorAction SilentlyContinue

if ($vault -notmatch 'REPLACE_ME|<\[^>\]+>') {
    throw 'Vault bootstrap must reject placeholders.'
}
foreach ($required in @('AZURE_STORAGE_SAS_TOKEN','sr','AZURE_STORAGE_ENDPOINT')) {
    if ($cnpg -notmatch [regex]::Escape($required)) { throw "CNPG bootstrap is missing $required contract." }
}
if (-not (Test-Path -LiteralPath $restorePath)) { throw 'Restore verification entry point is missing.' }
if (-not (Test-Path -LiteralPath $retentionPath)) { throw 'Azure immutable-retention verifier is missing.' }
if (-not (Test-Path -LiteralPath $immutabilityPath)) { throw 'Azure immutable-retention configurator is missing.' }
foreach ($required in @('AllowProduction', 'LOCK-WORM', 'DRY-RUN', 'immutability-policy', 'lock', 'TLS1_2', 'Microsoft.Keyvault', 'infrastructure')) {
    if ($immutability -notmatch [regex]::Escape($required)) { throw "Azure immutable-retention configurator is missing $required." }
}
foreach ($required in @('enableHttpsTrafficOnly', 'allowBlobPublicAccess', 'immutableStorageWithVersioningEnabled')) {
    if ($immutability -notmatch [regex]::Escape($required)) { throw "Azure immutable-retention configurator is missing account/container guard $required." }
}
foreach ($required in @('PolicyMode', 'Locked', 'ImmutabilityPeriodSinceCreationInDays', 'minimum_days')) {
    if ($retention -notmatch [regex]::Escape($required)) { throw "Azure immutable-retention verifier is missing $required." }
}
if ($cnpg -notmatch 'validate-azure-blob-retention\.py' -or $cnpg -notmatch 'minimum-days 30') {
    throw 'CNPG bootstrap must enforce Azure immutable retention before apply.'
}
foreach ($required in @('Invoke-RestMethod','EnumerationResults','TargetNamespace','test-cnpg-restore-drill.ps1','AllowProduction')) {
    if ($restore -notmatch [regex]::Escape($required)) { throw "Restore verifier is missing $required." }
}
if ($restore -match '(?i)Write-Host.*sas|Write-Output.*sas') { throw 'Restore verifier must not print SAS material.' }
if ($cnpgWorkflow -notmatch 'validate-azure-blob-access\.py') { throw 'CNPG workflow must validate Azure Blob access before apply.' }

Write-Output 'Azure production scripts contract: PASS'
