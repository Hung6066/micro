[CmdletBinding()]
param([string]$EnvFile = "docker/production-identity.env")

$ErrorActionPreference = 'Stop'
if (-not (Test-Path -LiteralPath $EnvFile)) { throw "Missing $EnvFile" }
$keys = @('POSTGRES_ADMIN_PASSWORD_FILE','SPIRE_DATABASE_PASSWORD_FILE','POSTGRES_MIGRATOR_PASSWORD_FILE','VAULT_DB_ADMIN_PASSWORD_FILE','VAULT_BOOTSTRAP_TOKEN_FILE','VAULT_SNAPSHOT_TOKEN_FILE','VAULT_TEST_TOKEN_FILE','POSTGRES_CA_FILE','POSTGRES_CERT_FILE','POSTGRES_KEY_FILE','SPIRE_NODE_CA_FILE','SPIRE_SERVER_BUNDLE_FILE','SPIRE_AGENT_CERT_FILE','SPIRE_AGENT_KEY_FILE','SPIRE_OIDC_CA_FILE','VAULT_CA_FILE','VAULT_CERT_FILE','VAULT_KEY_FILE','OIDC_CERT_FILE','OIDC_KEY_FILE')
$values = @{}
Get-Content -LiteralPath $EnvFile | ForEach-Object { if ($_ -match '^\s*([A-Za-z_][A-Za-z0-9_]*)=(.*)$') { $values[$matches[1]] = $matches[2].Trim() } }
foreach ($key in $keys) {
    if (-not $values.ContainsKey($key) -or -not (Test-Path -LiteralPath $values[$key])) { throw "Secret injection is incomplete: $key" }
}
docker compose --env-file $EnvFile -f docker/docker-compose.identity-production.yml config --quiet
Write-Output 'PRODUCTION_IDENTITY_COMPOSE_CONFIG_PASS'
