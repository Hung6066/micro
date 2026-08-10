$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSScriptRoot
$coreProject = Join-Path $repoRoot 'src/Shared/Core/His.Hope.Core/His.Hope.Core.csproj'
$contractsProject = Join-Path $repoRoot 'src/Shared/Contracts/His.Hope.Contracts/His.Hope.Contracts.csproj'

function Read-Project([string] $path) {
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
        throw "Required project file does not exist: $path"
    }

    return [xml](Get-Content -LiteralPath $path -Raw)
}

function Get-IncludeValues([System.Xml.XmlElement] $project, [string] $elementName) {
    $elements = @($project.Project.ItemGroup.$elementName)
    return @($elements | ForEach-Object { $_.Include } | Where-Object { $_ })
}

function Assert-LeafPackage([string] $name, [string] $path, [string] $forbiddenNamespace) {
    $project = Read-Project $path
    $propertyGroups = @($project.Project.PropertyGroup)
    $targetFramework = @($propertyGroups | ForEach-Object { $_.TargetFramework } | Where-Object { $_ } | Select-Object -First 1)
    if ($targetFramework -ne 'net8.0') {
        throw "$name must target net8.0; found '$targetFramework'."
    }

    $references = @(
        (Get-IncludeValues $project.Project 'ProjectReference')
        (Get-IncludeValues $project.Project 'PackageReference')
    ) | Where-Object { $_ }
    if ($references.Count -gt 0) {
        throw "$name must remain dependency-free; found: $($references -join ', ')"
    }

    $projectDirectory = Split-Path -Parent $path
    $sourceFiles = @(Get-ChildItem -LiteralPath $projectDirectory -Recurse -File | Where-Object { $_.Extension -eq '.cs' })
    foreach ($sourceFile in $sourceFiles) {
        $source = Get-Content -LiteralPath $sourceFile.FullName -Raw
        if ($source -match [regex]::Escape($forbiddenNamespace)) {
            throw "$name source must not reference ${forbiddenNamespace}: $($sourceFile.FullName)"
        }
    }
}

Write-Host 'Validating shared platform package boundaries...'
Assert-LeafPackage 'His.Hope.Core' $coreProject 'His.Hope.Contracts'
Assert-LeafPackage 'His.Hope.Contracts' $contractsProject 'His.Hope.Core'
Write-Host 'Shared platform package boundaries passed.'
