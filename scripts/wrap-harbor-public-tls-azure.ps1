[CmdletBinding()]
param(
    [string]$SecureRoot = 'D:\secure\his-hope',
    [string]$AzureEnvFile = 'D:\secure\his-hope\azure-production.env'
)

$ErrorActionPreference = 'Stop'
foreach ($path in @($AzureEnvFile, (Join-Path $SecureRoot 'harbor_public_key.pem'), (Join-Path $SecureRoot 'harbor_public_chain.pem'))) {
    if (-not (Test-Path -LiteralPath $path)) { throw "Required secure file is missing: $path" }
}

$values = @{}
Get-Content -LiteralPath $AzureEnvFile | ForEach-Object {
    if ($_ -match '^\s*([^#=][^=]*)=(.*)$') { $values[$matches[1].Trim()] = $matches[2].Trim() }
}
$vault = $values['VAULT_AZUREKEYVAULT_VAULT_NAME']
if ([string]::IsNullOrWhiteSpace($vault) -or $vault -match 'REPLACE_ME') { throw 'Key Vault name is missing or still a placeholder' }

function Set-KeyVaultSecret([string]$Name, [string]$File) {
    & az keyvault secret set --vault-name $vault --name $Name --file $File --query id -o tsv --only-show-errors | Out-Null
    if ($LASTEXITCODE -ne 0) { throw "Key Vault secret write failed for $Name; require Key Vault Secrets Officer or secrets/set permission" }
}

Set-KeyVaultSecret 'harbor-public-key-pem' (Join-Path $SecureRoot 'harbor_public_key.pem')
Set-KeyVaultSecret 'harbor-public-chain-pem' (Join-Path $SecureRoot 'harbor_public_chain.pem')
Write-Output "Stored Harbor public TLS key and chain as Key Vault secrets in '$vault'. Secret values were not printed."
