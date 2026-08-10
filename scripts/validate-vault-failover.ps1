[CmdletBinding()]
param([string]$ComposeEnvFile = "docker/production-identity.env")

$ErrorActionPreference = 'Stop'
$compose = @('--env-file', $ComposeEnvFile, '-f', 'docker/docker-compose.identity-production.yml')
docker compose @compose ps vault-1 vault-2 vault-3
docker compose @compose stop vault-1 | Out-Host
try {
    $status = docker compose @compose exec -T vault-2 sh -c 'VAULT_ADDR=https://vault-2:8200 VAULT_CACERT=/run/secrets/vault_ca.pem vault status -format=json'
    if ($LASTEXITCODE -ne 0 -or $status -notmatch '"initialized"\s*:\s*true') { throw 'Vault follower did not remain available after leader stop.' }
    Write-Output 'VAULT_RAFT_FAILOVER_PASS'
}
finally {
    docker compose @compose start vault-1 | Out-Host
}
