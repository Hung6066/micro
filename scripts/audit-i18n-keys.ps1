[CmdletBinding()]
param(
    [switch]$FailOnMissing
)

$ErrorActionPreference = "Stop"
$workspaceRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$featureRoot = Join-Path $workspaceRoot "admin-app\src\app"
$dictionaryPaths = @(
    (Join-Path $workspaceRoot "shared\frontend-foundation\i18n\src\dictionaries\en.ts"),
    (Join-Path $workspaceRoot "shared\frontend-foundation\i18n\src\dictionaries\vi-vn.ts")
)

$usedKeys = [ordered]@{}
$keyPattern = '["'']((?:admin|common|auth|table|errors|validation)\.[A-Za-z0-9_.-]+)["'']\s*\|\s*hhTranslate(?!\s*:)' 

Get-ChildItem $featureRoot -Recurse -Filter *.ts | ForEach-Object {
    $file = $_
    $content = Get-Content $file.FullName -Raw
    foreach ($match in [regex]::Matches($content, $keyPattern)) {
        $key = $match.Groups[1].Value
        if (-not $usedKeys.Contains($key)) {
            $usedKeys[$key] = [System.Collections.Generic.List[string]]::new()
        }
        $usedKeys[$key].Add($file.FullName.Substring($workspaceRoot.Length).TrimStart("\"))
    }
}

$dictionaryText = @{}
foreach ($path in $dictionaryPaths) {
    $dictionaryText[$path] = Get-Content $path -Raw
}

$missing = @()
foreach ($key in $usedKeys.Keys) {
    $property = ($key -split '\.')[-1]
    foreach ($path in $dictionaryPaths) {
        if ($dictionaryText[$path] -notmatch "(?m)^\s*$([regex]::Escape($property))\s*:") {
            $missing += [pscustomobject]@{
                Locale = [IO.Path]::GetFileNameWithoutExtension($path)
                Key    = $key
                Files  = ($usedKeys[$key] | Select-Object -Unique) -join ", "
            }
        }
    }
}

Write-Output "i18n key audit"
Write-Output "Referenced keys: $($usedKeys.Count)"
Write-Output "Missing translations: $($missing.Count)"
$missing | Sort-Object Key, Locale | Format-Table -AutoSize

if ($FailOnMissing -and $missing.Count -gt 0) {
    exit 1
}
