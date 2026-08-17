[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$ImageName,
    [Parameter(Mandatory = $true)][ValidatePattern('^sha256:[0-9a-f]{64}$')][string]$Digest,
    [string]$Path = 'k8s/overlays/prod/image-digests/kustomization.yaml',
    [string]$ReleaseSha = '',
    [string]$MetadataPath = 'k8s/overlays/prod/release-metadata.yaml'
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
if ($ImageName -notmatch '^his-hope/[a-z0-9][a-z0-9-]*$') {
    throw "Only application images under his-hope/<name> may be promoted: $ImageName"
}
if (-not [string]::IsNullOrWhiteSpace($ReleaseSha) -and $ReleaseSha -notmatch '^[0-9a-f]{40}$') {
    throw "ReleaseSha must be a 40-character commit SHA."
}
$file = (Resolve-Path -LiteralPath $Path).Path
$lines = [System.IO.File]::ReadAllLines($file)
$found = $false
$inImage = $false
for ($i = 0; $i -lt $lines.Length; $i++) {
    if ($lines[$i] -match '^\s*- name:\s*(?<name>[^\s]+)\s*$') {
        $inImage = $Matches['name'] -eq $ImageName
        if ($inImage) { $found = $true }
        continue
    }
    if ($inImage -and $lines[$i] -match '^\s*digest:\s*sha256:[0-9a-f]{64}\s*$') {
        $indent = $lines[$i].Substring(0, $lines[$i].IndexOf('digest:'))
        $lines[$i] = "${indent}digest: $Digest"
        $inImage = $false
        break
    }
    if ($inImage -and -not [string]::IsNullOrWhiteSpace($ReleaseSha) -and $lines[$i] -match '^(\s*)newTag:\s*[^\s]+\s*$') {
        $indent = $Matches[1]
        $lines[$i] = "${indent}newTag: $ReleaseSha"
    }
}
if (-not $found) { throw "Image name not found in ${Path}: $ImageName" }
[System.IO.File]::WriteAllLines($file, $lines, [System.Text.UTF8Encoding]::new($false))
Write-Output "Updated digest for $ImageName in $Path."

if (-not [string]::IsNullOrWhiteSpace($ReleaseSha)) {
    $metadata = (Resolve-Path -LiteralPath $MetadataPath).Path
    $metadataLines = [System.IO.File]::ReadAllLines($metadata)
    $releaseDigest = (Get-FileHash -Algorithm SHA256 -LiteralPath $file).Hash.ToLowerInvariant()
    $shaFound = $false
    $digestFound = $false
    for ($i = 0; $i -lt $metadataLines.Length; $i++) {
        if ($metadataLines[$i] -match '^\s*HIS_HOPE_RELEASE_SHA:\s*') {
            $metadataLines[$i] = "  HIS_HOPE_RELEASE_SHA: $ReleaseSha"
            $shaFound = $true
        }
        if ($metadataLines[$i] -match '^\s*HIS_HOPE_RELEASE_DIGEST:\s*') {
            $metadataLines[$i] = "  HIS_HOPE_RELEASE_DIGEST: sha256:$releaseDigest"
            $digestFound = $true
        }
    }
    if (-not $shaFound -or -not $digestFound) { throw "Release metadata keys are missing from $MetadataPath." }
    [System.IO.File]::WriteAllLines($metadata, $metadataLines, [System.Text.UTF8Encoding]::new($false))
    Write-Output "Updated release metadata for $ReleaseSha (sha256:$releaseDigest)."
}
