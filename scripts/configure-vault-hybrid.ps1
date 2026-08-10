[CmdletBinding()]
param(
    [string]$VaultAddress = $(if ($env:VAULT_ADDR) { $env:VAULT_ADDR } else { 'https://vault.internal:8200' }),
    [string]$JwtMount = 'jwt-spiffe',
    [string]$JwtIssuer,
    [string]$JwtJwksUrl,
    [string]$DatabaseMount = 'database',
    [string]$DatabaseName = 'his-hope-postgres',
    [string]$DatabaseConnectionUrl,
    [string]$DatabaseAdminUser,
    [string]$DatabaseAdminPassword
)

$ErrorActionPreference = 'Stop'
if ([string]::IsNullOrWhiteSpace($env:VAULT_TOKEN)) { throw 'VAULT_TOKEN must be supplied through the process environment.' }
if ([string]::IsNullOrWhiteSpace($JwtJwksUrl)) { throw 'JwtJwksUrl is required.' }
if ([string]::IsNullOrWhiteSpace($DatabaseConnectionUrl)) { throw 'DatabaseConnectionUrl is required.' }
if ([string]::IsNullOrWhiteSpace($DatabaseAdminUser) -or [string]::IsNullOrWhiteSpace($DatabaseAdminPassword)) { throw 'Database management credentials must be supplied out-of-band.' }

$env:VAULT_ADDR = $VaultAddress
$env:VAULT_TOKEN = $env:VAULT_TOKEN

vault auth enable -path=$JwtMount jwt 2>$null
if ($LASTEXITCODE -ne 0) { Write-Verbose "JWT auth mount already exists: $JwtMount" }
vault write "auth/$JwtMount/config" jwks_url=$JwtJwksUrl bound_issuer=''

$services = @('identity-service','patient-service','clinical-service','appointment-service','lab-service','billing-service','pharmacy-service')
foreach ($service in $services) {
    $policy = @"
path "$DatabaseMount/creds/$service-db" {
  capabilities = ["read"]
}
"@
    $policy | vault policy write "$service-db" - | Out-Null
    $boundSubject = "spiffe://his-hope.local/ns/his-hope/sa/$service"
    vault write "auth/$JwtMount/role/$service" "role_type=jwt" "user_claim=sub" "bound_audiences=vault" "bound_subject=$boundSubject" "policies=$service-db" "ttl=15m" "max_ttl=1h" | Out-Null
}

vault secrets enable -path=$DatabaseMount database 2>$null
if ($LASTEXITCODE -ne 0) { Write-Verbose "Database secrets engine already exists: $DatabaseMount" }
vault write "$DatabaseMount/config/$DatabaseName" "plugin_name=postgresql-database-plugin" "allowed_roles=identity-service-db,patient-service-db,clinical-service-db,appointment-service-db,lab-service-db,billing-service-db,pharmacy-service-db" "connection_url=$DatabaseConnectionUrl" "username=$DatabaseAdminUser" "password=$DatabaseAdminPassword" | Out-Null

$creation = 'CREATE ROLE "{{name}}" WITH LOGIN PASSWORD ''{{password}}'' VALID UNTIL ''{{expiration}}''; GRANT USAGE ON SCHEMA public TO "{{name}}"; GRANT SELECT, INSERT, UPDATE, DELETE, REFERENCES ON ALL TABLES IN SCHEMA public TO "{{name}}"; GRANT USAGE, SELECT, UPDATE ON ALL SEQUENCES IN SCHEMA public TO "{{name}}";'
$revocation = 'REASSIGN OWNED BY "{{name}}" TO his_hope; DROP OWNED BY "{{name}}"; DROP ROLE IF EXISTS "{{name}}";'
foreach ($service in $services) {
    vault write "$DatabaseMount/roles/$service-db" "db_name=$DatabaseName" "creation_statements=$creation" "revocation_statements=$revocation" "default_ttl=15m" "max_ttl=1h" | Out-Null
}
Write-Output "Vault hybrid configuration completed for $($services.Count) services."
