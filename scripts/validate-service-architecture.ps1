[CmdletBinding()]
param(
    [string]$Root
)

$ErrorActionPreference = 'Stop'

if ([string]::IsNullOrWhiteSpace($Root)) {
    $Root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
}

function Get-ProjectLayer([string]$Path) {
    $name = [System.IO.Path]::GetFileNameWithoutExtension($Path)
    foreach ($layer in @('Api', 'Infrastructure', 'Application', 'Domain')) {
        if ($name.EndsWith(".$layer", [System.StringComparison]::OrdinalIgnoreCase)) {
            return $layer
        }
    }
    return $null
}

function Get-ReferencedLayer([string]$Include) {
    $name = [System.IO.Path]::GetFileNameWithoutExtension($Include)
    foreach ($layer in @('Api', 'Infrastructure', 'Application', 'Domain')) {
        if ($name.EndsWith(".$layer", [System.StringComparison]::OrdinalIgnoreCase)) {
            return $layer
        }
    }
    return $null
}

$violations = [System.Collections.Generic.List[string]]::new()
$projects = Get-ChildItem -LiteralPath (Join-Path $Root 'src/Services') -Recurse -Filter '*.csproj' -File

foreach ($project in $projects) {
    $sourceLayer = Get-ProjectLayer $project.FullName
    if (-not $sourceLayer) { continue }

    [xml]$xml = Get-Content -LiteralPath $project.FullName -Raw
    $references = @($xml.Project.ItemGroup.ProjectReference | ForEach-Object { [string]$_.Include })
    foreach ($reference in $references) {
        $targetLayer = Get-ReferencedLayer $reference
        if (-not $targetLayer) { continue }

        $allowed = switch ($sourceLayer) {
            'Domain' { @('Domain') }
            'Application' { @('Domain', 'Application') }
            'Infrastructure' { @('Domain', 'Application', 'Infrastructure') }
            'Api' { @('Domain', 'Application', 'Infrastructure', 'Api') }
        }

        if ($allowed -notcontains $targetLayer) {
            $violations.Add("$($project.FullName): $sourceLayer -> $targetLayer ($reference)")
        }
    }
}

if ($violations.Count -gt 0) {
    Write-Error ("Service architecture dependency violations:`n" + ($violations -join "`n"))
    exit 1
}

Write-Host "Service architecture gate passed: $($projects.Count) service projects preserve Domain -> Application -> Infrastructure -> Api direction."
