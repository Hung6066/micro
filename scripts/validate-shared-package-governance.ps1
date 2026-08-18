$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSScriptRoot
$catalogPath = Join-Path $repoRoot 'config/shared-package-catalog.json'
if (-not (Test-Path -LiteralPath $catalogPath)) { throw "Package catalog not found: $catalogPath" }

$catalog = Get-Content -LiteralPath $catalogPath -Raw | ConvertFrom-Json
if (-not $catalog.packages -or $catalog.packages.Count -eq 0) { throw 'Package catalog must define at least one package.' }

$allowedEcosystems = @('npm', 'nuget')
$allowedChannels = @('canary', 'beta', 'stable')
$seenIds = @{}

foreach ($package in $catalog.packages) {
    foreach ($required in @('id', 'ecosystem', 'manifest', 'owner', 'channel')) {
        if ([string]::IsNullOrWhiteSpace([string]$package.$required)) {
            throw "Package catalog entry is missing '$required'."
        }
    }

    if ($seenIds.ContainsKey($package.id)) { throw "Package catalog contains duplicate id '$($package.id)'." }
    $seenIds[$package.id] = $true

    if ($package.ecosystem -notin $allowedEcosystems) { throw "Unsupported ecosystem '$($package.ecosystem)' for '$($package.id)'." }
    if ($package.channel -notin $allowedChannels) { throw "Unsupported release channel '$($package.channel)' for '$($package.id)'." }

    $manifestPath = Join-Path $repoRoot $package.manifest
    if (-not (Test-Path -LiteralPath $manifestPath -PathType Leaf)) { throw "Manifest not found for '$($package.id)': $($package.manifest)" }

    if ($package.ecosystem -eq 'npm') {
        $manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
        if ($manifest.name -ne $package.id) { throw "npm package id mismatch in '$($package.manifest)'; expected '$($package.id)', found '$($manifest.name)'." }
        if ([string]::IsNullOrWhiteSpace([string]$manifest.version) -or $manifest.version -notmatch '^\d+\.\d+\.\d+([-.+][0-9A-Za-z.-]+)?$') { throw "npm package '$($package.id)' must declare a SemVer version." }
        if ($manifest.private -eq $true) { throw "Shared npm package '$($package.id)' must be publishable, not private." }
        $changelogPath = Join-Path $repoRoot $package.changelog
        if (-not (Test-Path -LiteralPath $changelogPath -PathType Leaf)) { throw "npm package '$($package.id)' must include its declared changelog '$($package.changelog)'." }
        continue
    }

    [xml]$project = Get-Content -LiteralPath $manifestPath -Raw
    $properties = @($project.Project.PropertyGroup)
    $packageId = @($properties | ForEach-Object { $_.PackageId } | Where-Object { $_ } | Select-Object -First 1)
    if ($packageId -ne $package.id) { throw "NuGet package id mismatch in '$($package.manifest)'; expected '$($package.id)', found '$packageId'." }
    $isPackable = @($properties | ForEach-Object { $_.IsPackable } | Where-Object { $_ } | Select-Object -First 1)
    if ($isPackable -ne 'true') { throw "NuGet package '$($package.id)' must set IsPackable=true." }
}

Write-Host "Shared package governance passed: $($catalog.packages.Count) catalog entries."