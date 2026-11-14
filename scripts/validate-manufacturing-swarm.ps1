[CmdletBinding()]
param(
    [string]$StackFile = 'docker/swarm/manufacturing-stack.yml',
    [string]$EnvironmentFile = 'docker/swarm/manufacturing.env.example',
    [switch]$AllowMutableImages,
    [switch]$Live
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
function Resolve-RepoPath([string]$Path) {
    if ([IO.Path]::IsPathRooted($Path)) { return (Resolve-Path -LiteralPath $Path).Path }
    return (Resolve-Path (Join-Path $root $Path)).Path
}

$stack = Resolve-RepoPath $StackFile
$envFile = Resolve-RepoPath $EnvironmentFile

function Fail([string]$Message) { throw "MANUFACTURING_SWARM_FAIL $Message" }
function Require([bool]$Condition, [string]$Message) { if (-not $Condition) { Fail $Message } }

$text = Get-Content -Raw -LiteralPath $stack
Require ($text -match '(?m)^\s+manufacturingservice:\s*$') 'missing manufacturing API service'
Require ($text -match '(?m)^\s+manufacturing-worker:\s*$') 'missing dedicated worker service'
Require ($text -match '(?m)^\s+entrypoint:\s+\["/usr/local/bin/swarm-entrypoint.sh"\]') 'all services must execute the secret-aware entrypoint'
Require ($text -match '(?m)^\s+manufacturing:\s*$') 'missing overlay network'
Require ($text -notmatch '(?m)^\s+build:') 'Swarm stack must not contain build directives'
Require ($text -notmatch '(?m)^\s+container_name:') 'Swarm stack must not contain container_name'
Require ($text -match 'external:\s+true') 'stateful credentials must be external Docker secrets'
Require ($text -match 'failure_action:\s+rollback') 'rolling rollback policy is required'
Require (([regex]::Matches($text, '(?m)^\s+healthcheck:\s*$')).Count -ge 5) 'healthchecks are required for every service; run /health/ready probes separately'
Require ($text -match 'Outbox__Enabled:\s+"false"') 'API must not run duplicate outbox workers'
Require ($text -match 'Consumers__CommerceOrdersEnabled:\s+"true"') 'worker must own commerce consumer'
Require ($text -match 'Manufacturing__Automation__Enabled:\s+"true"') 'worker must own lifecycle automation'
Require ($text -match 'Vault__AuthMethod:\s+spiffe-jwt') 'Vault workload identity auth is required'
Require ($text -match 'Vault__SpiffeJwtTokenFile:\s+/run/secrets/manufacturing_vault_jwt') 'Vault JWT secret mount is required'
Require ($text -match 'manufacturing_vault_jwt:\s*\r?\n\s+external:\s+true') 'Vault JWT must be an external Swarm secret'

$envValues = @{}
foreach ($line in Get-Content -LiteralPath $envFile) {
    if ([string]::IsNullOrWhiteSpace($line) -or $line.TrimStart().StartsWith('#')) { continue }
    $idx = $line.IndexOf('=')
    if ($idx -gt 0) { $envValues[$line.Substring(0, $idx).Trim()] = $line.Substring($idx + 1).Trim() }
}
foreach ($key in @('IDENTITY_IMAGE','COMMERCE_IMAGE','CONTENT_IMAGE','MANUFACTURING_IMAGE','MANUFACTURING_DB_HOST','MANUFACTURING_REDIS_URL','MANUFACTURING_RABBIT_HOST','MANUFACTURING_OIDC_ISSUER','MANUFACTURING_JWT_AUTHORITY','MANUFACTURING_JWT_METADATA_ADDRESS','MANUFACTURING_JWT_ISSUER','MANUFACTURING_VAULT_ADDRESS','MANUFACTURING_VAULT_AUTH_MOUNT','MANUFACTURING_VAULT_AUDIENCE','MANUFACTURING_VAULT_JWT_SECRET')) {
    Require ($envValues.ContainsKey($key) -and -not [string]::IsNullOrWhiteSpace($envValues[$key])) "missing $key"
}
foreach ($key in @('IDENTITY_IMAGE','COMMERCE_IMAGE','CONTENT_IMAGE','MANUFACTURING_IMAGE')) {
    if (-not $AllowMutableImages) {
        Require ($envValues[$key] -match '@sha256:[0-9a-f]{64}$') "$key must be immutable digest"
    }
}
Require ($envValues['MANUFACTURING_OIDC_ISSUER'] -match '^https://') 'OIDC issuer must use HTTPS'
Require ($envValues['MANUFACTURING_JWT_AUTHORITY'] -match '^https://') 'JWT authority must use HTTPS'
Require ($envValues['MANUFACTURING_JWT_METADATA_ADDRESS'] -match '^https://') 'JWT metadata address must use HTTPS'
Require ($envValues['MANUFACTURING_JWT_ISSUER'] -match '^https://') 'JWT issuer must use HTTPS'
Require ($envValues['MANUFACTURING_VAULT_ADDRESS'] -match '^https://') 'Vault address must use HTTPS'

Write-Output "MANUFACTURING_SWARM_STATIC_PASS stack=$stack env=$envFile"

if ($Live) {
    foreach ($entry in $envValues.GetEnumerator()) {
        Set-Item "Env:$($entry.Key)" $entry.Value
    }
    $version = & rtk docker version --format '{{.Server.Version}}' 2>&1
    if ($LASTEXITCODE -ne 0) { Write-Output 'MANUFACTURING_SWARM_ENVIRONMENT_BLOCKED docker daemon unavailable'; exit 2 }
    $nodeState = & rtk docker info --format '{{.Swarm.LocalNodeState}}' 2>&1
    if ($LASTEXITCODE -ne 0 -or "$nodeState".Trim() -ne 'active') { Write-Output "MANUFACTURING_SWARM_ENVIRONMENT_BLOCKED swarm_state=$nodeState"; exit 2 }
    & rtk docker stack config -c $stack
    if ($LASTEXITCODE -ne 0) { Fail 'docker stack config rejected the stack' }
    Write-Output 'MANUFACTURING_SWARM_LIVE_CONFIG_PASS'
}
