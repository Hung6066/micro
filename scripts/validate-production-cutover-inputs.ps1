[CmdletBinding()]
param(
    [string]$Kubeconfig = 'artifacts/kubeconfig-production.yaml',
    [string]$SecureRoot = 'D:\secure\his-hope',
    [string]$AzureEnvFile,
    [string]$SshKeyPath,
    [string]$AnsibleVaultPasswordPath,
    [string]$OutputPath = 'artifacts/evidence/production-cutover-inputs.json',
    [switch]$RequireOperatorCredentials
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$checks = [System.Collections.Generic.List[object]]::new()
function Add-Check([string]$Name, [ValidateSet('pass','blocked','fail')][string]$Status, [string]$Detail) {
    $checks.Add([pscustomobject]@{ name = $Name; status = $Status; detail = $Detail })
}

function Resolve-SafePath([string]$PathValue) {
    if ([string]::IsNullOrWhiteSpace($PathValue)) { return $null }
    try { return [IO.Path]::GetFullPath($PathValue) } catch { return $null }
}

$kubePath = Resolve-SafePath $Kubeconfig
$securePath = Resolve-SafePath $SecureRoot
$azurePath = if ($AzureEnvFile) { Resolve-SafePath $AzureEnvFile } else { Join-Path $securePath 'azure-production.env' }

if ($kubePath -and (Test-Path -LiteralPath $kubePath -PathType Leaf)) {
    try {
        $null = & kubectl --kubeconfig $kubePath config view --minify -o json 2>$null
        if ($LASTEXITCODE -eq 0) { Add-Check 'kubeconfig' 'pass' 'Kubeconfig exists and kubectl can parse the current context.' }
        else { Add-Check 'kubeconfig' 'blocked' 'Kubeconfig exists but kubectl could not parse it.' }
    } catch { Add-Check 'kubeconfig' 'blocked' 'Kubeconfig parse failed; details intentionally redacted.' }
} else {
    Add-Check 'kubeconfig' 'blocked' 'Production kubeconfig file is missing.'
}

if (-not $securePath -or -not (Test-Path -LiteralPath $securePath -PathType Container)) {
    Add-Check 'secure-root' 'blocked' 'Secure root directory is missing.'
} else {
    Add-Check 'secure-root' 'pass' 'Secure root directory exists; values are never emitted.'
}

if ($azurePath -and (Test-Path -LiteralPath $azurePath -PathType Leaf)) {
    $keys = @{}
    foreach ($line in Get-Content -LiteralPath $azurePath -ErrorAction Stop) {
        if ($line -match '^\s*([A-Z][A-Z0-9_]+)\s*=') { $keys[$Matches[1]] = $true }
    }
    $required = @('AZURE_STORAGE_ACCOUNT','AZURE_STORAGE_CONTAINER','AZURE_STORAGE_ENDPOINT','AZURE_STORAGE_SAS_TOKEN')
    $missing = @($required | Where-Object { -not $keys.ContainsKey($_) })
    if ($missing.Count -eq 0) { Add-Check 'azure-env' 'pass' "Azure env contains the required key set ($($required.Count) keys); values redacted." }
    else { Add-Check 'azure-env' 'blocked' "Azure env is missing required key names: $($missing -join ', '). Values were not read into output." }
} else {
    Add-Check 'azure-env' 'blocked' 'azure-production.env is missing.'
}

foreach ($name in @('his_hope_ca.pem','vault_pki_ca_chain.pem')) {
    $path = Join-Path $securePath $name
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
        Add-Check "certificate-$name" 'blocked' "$name is missing."
        continue
    }
    try {
        $text = Get-Content -LiteralPath $path -Raw
        $count = ([regex]::Matches($text, '-----BEGIN CERTIFICATE-----')).Count
        if ($count -gt 0) { Add-Check "certificate-$name" 'pass' "$name contains $count PEM certificate block(s); private key material was not accessed." }
        else { Add-Check "certificate-$name" 'fail' "$name does not contain a PEM certificate block." }
    } catch { Add-Check "certificate-$name" 'fail' "$name could not be parsed; details intentionally redacted." }
}

if ($RequireOperatorCredentials) {
    $sshPath = if ($SshKeyPath) { Resolve-SafePath $SshKeyPath } else { Join-Path $env:USERPROFILE '.ssh\id_deploy' }
    $vaultPath = if ($AnsibleVaultPasswordPath) { Resolve-SafePath $AnsibleVaultPasswordPath } else { Join-Path $securePath 'ansible-vault-password' }
    if ($sshPath -and (Test-Path -LiteralPath $sshPath -PathType Leaf)) {
        Add-Check 'operator-ssh-key' 'pass' 'Explicit private SSH key exists; key content was not read.'
    } else {
        Add-Check 'operator-ssh-key' 'blocked' 'Private SSH key is missing; pass -SshKeyPath explicitly or provide the default id_deploy key.'
    }
    if ($vaultPath -and (Test-Path -LiteralPath $vaultPath -PathType Leaf)) {
        Add-Check 'operator-vault-password' 'pass' 'Explicit Ansible Vault password file exists; content was not read.'
    } else {
        Add-Check 'operator-vault-password' 'blocked' 'Ansible Vault password file is missing; pass -AnsibleVaultPasswordPath explicitly.'
    }
}

$status = if (@($checks | Where-Object status -eq 'fail').Count -gt 0) { 'fail' } elseif (@($checks | Where-Object status -eq 'blocked').Count -gt 0) { 'blocked' } else { 'pass' }
$evidence = [pscustomobject]@{ status = $status; checks = @($checks); generatedAtUtc = [DateTime]::UtcNow.ToString('o') }
$json = $evidence | ConvertTo-Json -Depth 6
if ($OutputPath) {
    $full = [IO.Path]::GetFullPath($OutputPath)
    $parent = Split-Path -Parent $full
    if ($parent) { New-Item -ItemType Directory -Path $parent -Force | Out-Null }
    [IO.File]::WriteAllText($full, $json, [Text.UTF8Encoding]::new($false))
}
Write-Output $json
if ($status -eq 'fail' -or $status -eq 'blocked') { exit 30 }
exit 0
