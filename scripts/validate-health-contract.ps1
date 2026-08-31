[CmdletBinding()]
param(
    [string]$Root
)

$ErrorActionPreference = 'Stop'
$Root = if ([string]::IsNullOrWhiteSpace($Root)) {
    (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
} else {
    (Resolve-Path $Root).Path
}

$targets = @()
$targets += Get-ChildItem (Join-Path $Root 'src/Services') -Recurse -Filter 'Program.cs' -File |
    Where-Object { $_.FullName -match '\.Api[\\/]Program\.cs$' } |
    ForEach-Object { [pscustomobject]@{ Path = $_.FullName; Kind = 'service API' } }
$targets += Get-ChildItem (Join-Path $Root 'src/Bff') -Recurse -Filter 'Program.cs' -File |
    ForEach-Object { [pscustomobject]@{ Path = $_.FullName; Kind = 'BFF' } }
$gateway = Join-Path $Root 'src/ApiGateway/Program.cs'
if (Test-Path -LiteralPath $gateway -PathType Leaf) {
    $targets += [pscustomobject]@{ Path = $gateway; Kind = 'API Gateway' }
}

if ($targets.Count -eq 0) { throw 'No API, BFF or Gateway Program.cs files were found.' }

$failures = [System.Collections.Generic.List[string]]::new()
$bffHealthImplementation = Join-Path $Root 'src/Bff/His.Hope.Bff.Core/DependencyInjection.cs'
if (-not (Test-Path -LiteralPath $bffHealthImplementation -PathType Leaf) -or
    (Get-Content -LiteralPath $bffHealthImplementation -Raw) -notmatch 'MapHisHopeHealthEndpoints\(') {
    $failures.Add('His.Hope.Bff.Core.MapBffHealth must delegate to MapHisHopeHealthEndpoints.')
}
foreach ($target in $targets) {
    $text = Get-Content -LiteralPath $target.Path -Raw
    $relative = $target.Path.Substring($Root.Length + 1)
    if ($text -notmatch 'MapHisHopeHealthEndpoints\(' -and $text -notmatch 'MapBffHealth\(') {
        $failures.Add("$relative ($($target.Kind)) does not map the shared health contract.")
    }
}

if ($failures.Count -gt 0) {
    $failures | ForEach-Object { Write-Error $_ }
    throw "Health contract failed for $($failures.Count) target(s)."
}

Write-Host "Health contract passed for $($targets.Count) API/BFF/Gateway targets."
