[CmdletBinding()]
param(
    [string]$Artifact = "artifacts/packages/his-hope-frontend-foundation-1.1.0.tgz"
)

$ErrorActionPreference = 'Stop'
if (-not (Test-Path -LiteralPath $Artifact)) { throw "Frontend artifact not found: $Artifact" }

$packageJson = tar -xOf $Artifact package/package.json | ConvertFrom-Json
if ($packageJson.name -ne '@his-hope/frontend-foundation') { throw "Unexpected package name: $($packageJson.name)" }
if ($packageJson.version -notmatch '^\d+\.\d+\.\d+$') { throw "Artifact version is not semver: $($packageJson.version)" }

foreach ($section in @('dependencies','devDependencies','peerDependencies','optionalDependencies')) {
    $values = $packageJson.$section
    if ($null -eq $values) { continue }
    foreach ($prop in $values.psobject.Properties) {
        if ($prop.Value -match '^file:') { throw "Artifact contains local dependency $($prop.Name): $($prop.Value)" }
    }
}

foreach ($entry in @($packageJson.main, $packageJson.module, $packageJson.types)) {
    if ($entry -and -not (tar -tf $Artifact | Select-String -SimpleMatch ("package/" + $entry.TrimStart('./')))) {
        throw "Artifact entry point missing: $entry"
    }
}

Write-Output "Frontend artifact passed: $($packageJson.name)@$($packageJson.version), no local dependencies, entry points present."
