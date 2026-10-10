[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$ImageRefDirectory,
    [Parameter(Mandatory = $true)][ValidatePattern('^[0-9a-f]{40}$')][string]$ReleaseSha,
    [string]$Path = 'k8s/overlays/prod/image-digests/kustomization.yaml',
    [string]$MetadataPath = 'k8s/overlays/prod/release-metadata.yaml'
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$imageDirectory = (Resolve-Path -LiteralPath $ImageRefDirectory).Path
$imageReferences = @(
    Get-ChildItem -LiteralPath $imageDirectory -Recurse -File -Filter '*.image-ref.txt' |
        ForEach-Object { Get-Content -LiteralPath $_.FullName -Raw } |
        ForEach-Object { $_.Trim() } |
        Where-Object { $_ } |
        Sort-Object -Unique
)
if ($imageReferences.Count -eq 0) {
    throw "No immutable image reference artifacts found under $imageDirectory."
}
if ($imageReferences.Count -ne 22) {
    throw "Production promotion requires exactly 22 application image references; found $($imageReferences.Count)."
}

$digests = @{}
foreach ($reference in $imageReferences) {
    if ($reference -notmatch '^harbor\.myduchospital\.com:443/(?<name>his-hope/[a-z0-9][a-z0-9-]*)@(?<digest>sha256:[0-9a-f]{64})$') {
        throw "Release artifact is not an approved Harbor digest reference: $reference"
    }
    $name = $Matches['name']
    if ($digests.ContainsKey($name)) {
        throw "Duplicate release artifact for $name."
    }
    $digests[$name] = $Matches['digest']
}

$manifestPath = [IO.Path]::GetFullPath($Path)
$manifestText = Get-Content -LiteralPath $manifestPath -Raw
foreach ($name in $digests.Keys) {
    if ($manifestText -notmatch "(?m)^\s*- name:\s*$([regex]::Escape($name))\s*$") {
        throw "Release artifact $name is not an approved production image mapping."
    }
}

$updater = Join-Path $repositoryRoot 'scripts/update-gitops-digest.ps1'
foreach ($name in ($digests.Keys | Sort-Object)) {
    & $updater -ImageName $name -Digest $digests[$name] -Path $manifestPath -ReleaseSha $ReleaseSha -MetadataPath $MetadataPath
    if ($LASTEXITCODE -ne 0) {
        throw "Failed to promote digest for $name."
    }
}

$versionPaths = @(
    'k8s/overlays/prod/kustomization.yaml',
    'k8s/overlays/prod/traefik-production.yaml',
    'k8s/overlays/prod-spire-azure/kustomization.yaml'
)
foreach ($relativePath in $versionPaths) {
    $versionPath = Join-Path $repositoryRoot $relativePath
    if (-not (Test-Path -LiteralPath $versionPath -PathType Leaf)) {
        throw "Production release metadata path is missing: $relativePath"
    }
    $versionText = Get-Content -LiteralPath $versionPath -Raw
    $updatedVersionText = [regex]::Replace(
        $versionText,
        '(?m)^(\s*app\.kubernetes\.io/version:\s*)[0-9a-f]{40}\s*$',
        { param($match) "$($match.Groups[1].Value)$ReleaseSha" }
    )
    if ($updatedVersionText -eq $versionText) {
        throw "Production release metadata does not contain an immutable app.kubernetes.io/version label: $relativePath"
    }
    [IO.File]::WriteAllText($versionPath, $updatedVersionText, [Text.UTF8Encoding]::new($false))
}

Write-Output "Updated $($digests.Count) approved production image digest(s) for release $ReleaseSha."
