[CmdletBinding()]
param(
    [string]$ProdOverlay = (Join-Path $PSScriptRoot '..\k8s\overlays\prod')
)

$ErrorActionPreference = 'Stop'
$overlayPath = (Resolve-Path -LiteralPath $ProdOverlay).Path
$files = @(Get-ChildItem -LiteralPath $overlayPath -Recurse -File -Include '*.yaml', '*.yml')
if ($files.Count -eq 0) {
    throw "No Kubernetes manifests found under $overlayPath"
}

$errors = [System.Collections.Generic.List[string]]::new()

if ($files | Select-String -Pattern '^\s*image:\s*[^\s]+:latest\s*$') {
    $errors.Add('Production overlay contains an image tagged latest.')
}

if ($files | Select-String -Pattern 'sha256:0{64}') {
    $errors.Add('Production overlay contains a zero placeholder image digest.')
}

if ($files | Select-String -Pattern '\$\{[A-Za-z_][A-Za-z0-9_]*\}') {
    $errors.Add('Production overlay contains an unresolved ${...} substitution.')
}

if (-not ($files | Select-String -Pattern '(?i)cosign|signature|ratify|externaldata')) {
    $errors.Add('Production overlay does not declare an image signature verification control.')
}

if ($errors.Count -gt 0) {
    $errors | ForEach-Object { Write-Error $_ }
    exit 1
}

Write-Host "Kubernetes production release validation passed: $overlayPath"
