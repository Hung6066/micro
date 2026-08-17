[CmdletBinding()]
param([string]$OutputDirectory = 'D:\secure\his-hope')

$ErrorActionPreference = 'Stop'
if (-not (Get-Command openssl -ErrorAction SilentlyContinue)) {
    throw 'OpenSSL is required. Install it with: winget install ShiningLight.OpenSSL.Light'
}

New-Item -ItemType Directory -Force -Path $OutputDirectory | Out-Null

function New-RandomFile([string]$Name) {
    $bytes = New-Object byte[] 32
    [Security.Cryptography.RandomNumberGenerator]::Fill($bytes)
    [Convert]::ToBase64String($bytes) | Set-Content -NoNewline (Join-Path $OutputDirectory $Name)
}

function Invoke-OpenSsl([string[]]$Arguments) {
    & openssl @Arguments
    if ($LASTEXITCODE -ne 0) { throw "OpenSSL failed: $($Arguments -join ' ')" }
}

$caKey = Join-Path $OutputDirectory 'his_hope_ca.key'
$caCert = Join-Path $OutputDirectory 'his_hope_ca.pem'
Invoke-OpenSsl @('req','-x509','-newkey','rsa:4096','-nodes','-sha256','-days','3650','-keyout',$caKey,'-out',$caCert,'-subj','/CN=His.Hope Local CA')

function New-Certificate([string]$Name, [string[]]$DnsNames, [string]$CaKey, [string]$CaCert) {
    $key = Join-Path $OutputDirectory "$Name.key.pem"
    $csr = Join-Path $OutputDirectory "$Name.csr.pem"
    $cert = Join-Path $OutputDirectory "$Name.pem"
    $serial = Join-Path $OutputDirectory "$Name.srl"
    $ext = Join-Path $OutputDirectory "$Name.ext"
    $index = 0
    $san = ($DnsNames | ForEach-Object { $index++; "DNS:$($_)" }) -join ','
    @("basicConstraints=CA:FALSE", "keyUsage=digitalSignature,keyEncipherment", "extendedKeyUsage=serverAuth,clientAuth", "subjectAltName=$san") | Set-Content $ext
    Invoke-OpenSsl @('req','-new','-newkey','rsa:2048','-nodes','-keyout',$key,'-out',$csr,'-subj',"/CN=$($DnsNames[0])")
    Invoke-OpenSsl @('x509','-req','-sha256','-days','825','-in',$csr,'-CA',$CaCert,'-CAkey',$CaKey,'-CAcreateserial','-CAserial',$serial,'-out',$cert,'-extfile',$ext)
    Remove-Item $csr,$ext,$serial -Force -ErrorAction SilentlyContinue
}

New-Certificate 'postgres_cert' @('postgres','localhost') $caKey $caCert
New-Certificate 'vault_cert' @('vault-1','vault-2','vault-3','localhost') $caKey $caCert
New-Certificate 'oidc_cert' @('oidc.his-hope.local','localhost') $caKey $caCert
Copy-Item (Join-Path $OutputDirectory 'postgres_cert.key.pem') (Join-Path $OutputDirectory 'postgres_key.pem') -Force
Copy-Item (Join-Path $OutputDirectory 'vault_cert.key.pem') (Join-Path $OutputDirectory 'vault_key.pem') -Force
Copy-Item (Join-Path $OutputDirectory 'oidc_cert.key.pem') (Join-Path $OutputDirectory 'oidc_key.pem') -Force

$spireCaKey = Join-Path $OutputDirectory 'spire_node_ca.key'
$spireCa = Join-Path $OutputDirectory 'spire_node_ca.pem'
Invoke-OpenSsl @('req','-x509','-newkey','rsa:4096','-nodes','-sha256','-days','3650','-keyout',$spireCaKey,'-out',$spireCa,'-subj','/CN=His.Hope SPIRE Node CA')
New-Certificate 'spire_agent_cert' @('docker-local-agent') $spireCaKey $spireCa
Copy-Item (Join-Path $OutputDirectory 'spire_agent_cert.key.pem') (Join-Path $OutputDirectory 'spire_agent_key.pem') -Force

Copy-Item $caCert (Join-Path $OutputDirectory 'postgres_ca.pem') -Force
Copy-Item $caCert (Join-Path $OutputDirectory 'vault_ca.pem') -Force
Copy-Item $caCert (Join-Path $OutputDirectory 'spire_oidc_ca.pem') -Force
# SPIRE server bundle is replaced by the server-issued bundle after bootstrap.
Copy-Item $caCert (Join-Path $OutputDirectory 'spire_server_bundle.pem') -Force

@('postgres_admin_password','spire_database_password','postgres_migrator_password','vault_db_admin_password','vault_bootstrap_token','vault_snapshot_token','vault_rotation_test_token') | ForEach-Object { New-RandomFile $_ }

Write-Output "Local identity files created under $OutputDirectory"
Write-Output 'Do not use the generated Vault token files until they are replaced with real Vault-issued tokens.'
Write-Output 'Keep *.key.pem, passwords and tokens outside Git.'
