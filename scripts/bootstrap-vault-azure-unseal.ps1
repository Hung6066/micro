[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$Context,
    [string]$EnvFile = 'D:\secure\his-hope\azure-production.env',
    [string]$ClientSecretFile = 'D:\secure\his-hope\azure_client_secret',
    [switch]$Apply
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

if (-not $Apply) { throw 'Dry-run only. Re-run with -Apply after verifying the production kube-context.' }
if (-not (Test-Path -LiteralPath $EnvFile)) { throw "Azure env file not found: $EnvFile" }
if (-not (Test-Path -LiteralPath $ClientSecretFile)) { throw "Azure client secret file not found: $ClientSecretFile" }

$values = @{}
foreach ($line in Get-Content -LiteralPath $EnvFile) {
    $trimmed = $line.Trim()
    if ([string]::IsNullOrWhiteSpace($trimmed) -or $trimmed.StartsWith('#')) { continue }
    if ($trimmed -notmatch '^([A-Z0-9_]+)=(.*)$') { throw 'Invalid Azure env file line format.' }
    $values[$Matches[1]] = $Matches[2]
}

foreach ($key in @('AZURE_TENANT_ID', 'AZURE_CLIENT_ID', 'VAULT_AZUREKEYVAULT_VAULT_NAME', 'VAULT_AZUREKEYVAULT_KEY_NAME')) {
    if (-not $values.ContainsKey($key) -or [string]::IsNullOrWhiteSpace($values[$key]) -or $values[$key] -match 'REPLACE_ME|<[^>]+>') {
        throw "Missing or placeholder Azure Key Vault value: $key"
    }
}

$clientSecret = (Get-Content -Raw -LiteralPath $ClientSecretFile).Trim()
if ([string]::IsNullOrWhiteSpace($clientSecret) -or $clientSecret -match 'REPLACE_ME|<[^>]+>') { throw 'Azure client secret file is empty or a placeholder.' }

$current = (& kubectl config current-context).Trim()
if ($LASTEXITCODE -ne 0 -or $current -ne $Context) { throw "Current kube-context '$current' does not match requested context '$Context'." }

function B64([string]$value) { [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($value)) }
$secretYaml = @"
apiVersion: v1
kind: Secret
metadata:
  name: vault-azure-unseal
  namespace: his-hope
  labels:
    app.kubernetes.io/name: vault-azure-unseal
    app.kubernetes.io/component: seal-credentials
    app.kubernetes.io/part-of: his-hope
    vault.his-hope.io/managed-by: bootstrap
type: Opaque
data:
  AZURE_TENANT_ID: $(B64 $values['AZURE_TENANT_ID'])
  AZURE_CLIENT_ID: $(B64 $values['AZURE_CLIENT_ID'])
  AZURE_CLIENT_SECRET: $(B64 $clientSecret)
  VAULT_AZUREKEYVAULT_VAULT_NAME: $(B64 $values['VAULT_AZUREKEYVAULT_VAULT_NAME'])
  VAULT_AZUREKEYVAULT_KEY_NAME: $(B64 $values['VAULT_AZUREKEYVAULT_KEY_NAME'])
"@
$secretYaml | kubectl --context $Context apply -f - | Out-Null
if ($LASTEXITCODE -ne 0) { throw 'Unable to apply vault-azure-unseal Secret.' }

kubectl --context $Context apply -f k8s/production-ha/vault/vault-production.yaml
if ($LASTEXITCODE -ne 0) { throw 'Unable to apply Vault production manifest.' }

Write-Output 'Vault Azure Key Vault seal configuration applied. Verify Vault pods are unsealed and healthy before initializing application secrets.'
