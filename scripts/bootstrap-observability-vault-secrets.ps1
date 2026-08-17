[CmdletBinding()]
param(
    [string]$SecretFile = 'D:\secure\his-hope\observability-alertmanager.env',
    [string]$VaultAddress = 'https://127.0.0.1:18200',
    [string]$VaultTokenFile = 'D:\secure\his-hope\vault_bootstrap_token',
    [string]$GrafanaClientId,
    [string]$GrafanaClientSecret,
    [string]$ObjectStoreEndpoint = 'http://minio.backup.svc.cluster.local:9000',
    [string]$ObjectStoreBucket = 'his-hope-observability',
    [string]$ObjectStoreRegion = 'us-east-1',
    [string]$ObjectStoreAccessKeyId,
    [string]$ObjectStoreSecretAccessKey
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

if (-not (Test-Path -LiteralPath $SecretFile)) { throw "Secret file not found: $SecretFile" }
if (-not (Test-Path -LiteralPath $VaultTokenFile)) { throw "Vault token file not found: $VaultTokenFile" }

$values = @{}
Get-Content -LiteralPath $SecretFile | ForEach-Object {
    if ($_ -match '^([^#=][^=]*)=(.*)$') { $values[$matches[1].Trim()] = $matches[2].Trim() }
}
foreach ($required in @('SMTP_HOST','SMTP_PORT','SMTP_USERNAME','SMTP_PASSWORD','SMTP_FROM','SMTP_TO','DISCORD_WEBHOOK_URL')) {
    if ([string]::IsNullOrWhiteSpace($values[$required])) { throw "Missing $required in $SecretFile" }
}

if ([string]::IsNullOrWhiteSpace($GrafanaClientId) -or [string]::IsNullOrWhiteSpace($GrafanaClientSecret)) {
    throw 'Grafana OIDC client id/secret must be supplied from the Identity Service client registration.'
}
if ([string]::IsNullOrWhiteSpace($ObjectStoreAccessKeyId) -or [string]::IsNullOrWhiteSpace($ObjectStoreSecretAccessKey)) {
    throw 'Object-store access key and secret must be supplied from a least-privilege MinIO user.'
}

$env:VAULT_ADDR = $VaultAddress
$env:VAULT_SKIP_VERIFY = 'true'
$tokenText = (Get-Content -Raw -LiteralPath $VaultTokenFile).Trim()
if ($tokenText.StartsWith('{')) {
    $rootTokenMatch = [regex]::Match($tokenText, '"root_token"\s*:\s*"([^"]+)"')
    if (-not $rootTokenMatch.Success) { throw 'Vault token wrapper does not contain root_token.' }
    $tokenText = $rootTokenMatch.Groups[1].Value
}
if ([string]::IsNullOrWhiteSpace($tokenText) -or $tokenText -match '\p{C}') {
    throw 'Vault token file must contain a plain token or a valid root_token wrapper.'
}
$env:VAULT_TOKEN = $tokenText
try {
    $common = @('--format=json')
    & vault kv put @common secret/his-hope/observability/grafana-oidc `
        client_id=$GrafanaClientId client_secret=$GrafanaClientSecret | Out-Null
    if ($LASTEXITCODE -ne 0) { throw 'Vault write failed for grafana-oidc.' }

    & vault kv put @common secret/his-hope/observability/alertmanager `
        smtp_host=$values.SMTP_HOST smtp_port=$values.SMTP_PORT `
        smtp_username=$values.SMTP_USERNAME smtp_password=$values.SMTP_PASSWORD `
        smtp_from=$values.SMTP_FROM smtp_to=$values.SMTP_TO `
        discord_webhook_url=$values.DISCORD_WEBHOOK_URL | Out-Null
    if ($LASTEXITCODE -ne 0) { throw 'Vault write failed for alertmanager.' }

    & vault kv put @common secret/his-hope/observability/object-store `
        endpoint=$ObjectStoreEndpoint bucket=$ObjectStoreBucket region=$ObjectStoreRegion `
        access_key_id=$ObjectStoreAccessKeyId secret_access_key=$ObjectStoreSecretAccessKey | Out-Null
    if ($LASTEXITCODE -ne 0) { throw 'Vault write failed for object-store.' }

    Write-Output 'PASS: observability Vault paths written without printing secret values.'
}
finally {
    Remove-Item Env:VAULT_TOKEN -ErrorAction SilentlyContinue
    Remove-Item Env:VAULT_ADDR -ErrorAction SilentlyContinue
    Remove-Item Env:VAULT_SKIP_VERIFY -ErrorAction SilentlyContinue
}
