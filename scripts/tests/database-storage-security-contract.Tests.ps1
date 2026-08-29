$ErrorActionPreference = 'Stop'

$validatorPath = Join-Path $PSScriptRoot '..\validate-database-storage-security-contract.ps1'
$validator = Get-Content -LiteralPath $validatorPath -Raw

foreach ($required in @(
    'Vault__RequireVault',
    'Vault__AllowStaticToken',
    'vaultSkipTLSVerify',
    'object.?lock|retention|WORM',
    'REPLACE_ME',
    'azureCredentials',
    'storageSasToken',
    'retentionPolicy',
    'local-path',
    'https://minio-'
)) {
    if ($validator -notmatch $required) {
        throw "Database/storage validator is missing fail-closed rule: $required"
    }
}

$output = & $validatorPath 2>&1
if ($LASTEXITCODE -ne 60) {
    throw "Expected the current repository to remain blocked until production provider evidence is supplied; exit=$LASTEXITCODE output=$($output -join ' ')"
}
if (($output -join "`n") -notmatch '"status"\s*:\s*"blocked"') {
    throw 'Validator did not emit blocked status for the current unverified production storage contract.'
}

Write-Output 'Database/storage security validator contract: PASS (unsafe current baseline correctly blocked)'
