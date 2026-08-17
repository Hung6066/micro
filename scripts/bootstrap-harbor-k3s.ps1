[CmdletBinding()]
param(
    [string]$SecureRoot = 'D:\secure\his-hope',
    [string]$Namespace = 'harbor',
    [string]$ChartVersion = '1.19.2'
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$values = Join-Path $repoRoot 'k8s\harbor\harbor-values.yaml'
$caCert = Join-Path $SecureRoot 'his_hope_ca.pem'
$tlsCert = Join-Path $SecureRoot 'harbor_cert.pem'
$tlsKey = Join-Path $SecureRoot 'harbor_key.pem'
$adminPassword = Join-Path $SecureRoot 'harbor_admin_password'
$secretKeyFile = Join-Path $SecureRoot 'harbor_secret_key'

function Invoke-Kubectl([string[]]$KubectlArgs) {
    & kubectl @KubectlArgs
    if ($LASTEXITCODE -ne 0) { throw "kubectl failed: $($KubectlArgs -join ' ')" }
}

function Apply-KubectlGeneratedYaml([string[]]$KubectlArgs) {
    $temp = Join-Path ([System.IO.Path]::GetTempPath()) (([guid]::NewGuid()).ToString() + '.yaml')
    try {
        & kubectl @KubectlArgs | Set-Content -Path $temp -Encoding utf8
        if ($LASTEXITCODE -ne 0) { throw "kubectl generation failed: $($KubectlArgs -join ' ')" }
        & kubectl apply -f $temp
        if ($LASTEXITCODE -ne 0) { throw "kubectl apply failed: $($KubectlArgs -join ' ')" }
    }
    finally {
        Remove-Item -LiteralPath $temp -Force -ErrorAction SilentlyContinue
    }
}

if (-not (Test-Path $values)) { throw "Missing Harbor values: $values" }
foreach ($path in @($caCert, $tlsCert, $tlsKey)) {
    if (-not (Test-Path $path)) { throw "Missing runtime certificate file: $path. Run scripts\generate-harbor-cert.py first." }
}
New-Item -ItemType Directory -Force -Path $SecureRoot | Out-Null

if (-not (Test-Path $adminPassword)) {
    $bytes = [byte[]]::new(32)
    $rng = [System.Security.Cryptography.RandomNumberGenerator]::Create()
    $rng.GetBytes($bytes)
    $rng.Dispose()
    [Convert]::ToBase64String($bytes) | Set-Content -NoNewline -Encoding ascii $adminPassword
}
if (-not (Test-Path $secretKeyFile)) {
    $alphabet = 'abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789'
    $bytes = [byte[]]::new(16)
    $rng = [System.Security.Cryptography.RandomNumberGenerator]::Create()
    $rng.GetBytes($bytes)
    $rng.Dispose()
    $key = -join ($bytes | ForEach-Object { $alphabet[$_ % $alphabet.Length] })
    $key | Set-Content -NoNewline -Encoding ascii $secretKeyFile
}
$secretKey = (Get-Content -Raw $secretKeyFile).Trim()
if ($secretKey.Length -ne 16) { throw 'harbor_secret_key must contain exactly 16 characters' }

Apply-KubectlGeneratedYaml @('create', 'namespace', $Namespace, '--dry-run=client', '-o', 'yaml')
Apply-KubectlGeneratedYaml @('-n', $Namespace, 'create', 'secret', 'tls', 'harbor-tls', '--cert', $tlsCert, '--key', $tlsKey, '--dry-run=client', '-o', 'yaml')
Apply-KubectlGeneratedYaml @('-n', $Namespace, 'create', 'secret', 'generic', 'harbor-ca', "--from-file=ca.crt=$caCert", '--dry-run=client', '-o', 'yaml')
Apply-KubectlGeneratedYaml @('-n', $Namespace, 'create', 'secret', 'generic', 'harbor-admin', "--from-file=HARBOR_ADMIN_PASSWORD=$adminPassword", '--dry-run=client', '-o', 'yaml')

helm repo add harbor https://helm.goharbor.io --force-update | Out-Null
helm repo update harbor | Out-Null
helm upgrade --install harbor harbor/harbor --namespace $Namespace --version $ChartVersion --values $values --set existingSecretAdminPassword=harbor-admin --set existingSecretAdminPasswordKey=HARBOR_ADMIN_PASSWORD --set secretKey=$secretKey --wait --timeout 15m
if ($LASTEXITCODE -ne 0) { throw 'Harbor Helm deployment failed' }

kubectl -n $Namespace wait --for=condition=Ready pod --all --timeout=15m
if ($LASTEXITCODE -ne 0) { throw 'Harbor pods did not become ready' }
Write-Output 'Harbor deployment PASS: namespace, TLS, persistent PVCs, and ready pods.'
