$ErrorActionPreference = 'Stop'

$vault = Get-Content -LiteralPath (Join-Path $PSScriptRoot '..\bootstrap-vault-azure-unseal.ps1') -Raw
$cnpg = Get-Content -LiteralPath (Join-Path $PSScriptRoot '..\bootstrap-cnpg-azure-object-store.ps1') -Raw
$restorePath = Join-Path $PSScriptRoot '..\verify-production-backup-restore.ps1'
$restore = Get-Content -LiteralPath $restorePath -Raw
$cnpgWorkflow = Get-Content -LiteralPath (Join-Path $PSScriptRoot '..\..\.github\workflows\cnpg-azure-backup-bootstrap.yml') -Raw -ErrorAction SilentlyContinue

if ($vault -notmatch 'REPLACE_ME|<\[^>\]+>') {
    throw 'Vault bootstrap must reject placeholders.'
}
foreach ($required in @('AZURE_STORAGE_SAS_TOKEN','sr','AZURE_STORAGE_ENDPOINT')) {
    if ($cnpg -notmatch [regex]::Escape($required)) { throw "CNPG bootstrap is missing $required contract." }
}
if (-not (Test-Path -LiteralPath $restorePath)) { throw 'Restore verification entry point is missing.' }
foreach ($required in @('Invoke-RestMethod','EnumerationResults','TargetNamespace','test-cnpg-restore-drill.ps1','AllowProduction')) {
    if ($restore -notmatch [regex]::Escape($required)) { throw "Restore verifier is missing $required." }
}
if ($restore -match '(?i)Write-Host.*sas|Write-Output.*sas') { throw 'Restore verifier must not print SAS material.' }
if ($cnpgWorkflow -notmatch 'validate-azure-blob-access\.py') { throw 'CNPG workflow must validate Azure Blob access before apply.' }

Write-Output 'Azure production scripts contract: PASS'
