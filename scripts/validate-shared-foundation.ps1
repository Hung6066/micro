$ErrorActionPreference = 'Stop'

Write-Host 'Validating shared foundation catalog and public exports...'
$catalog = Get-Content 'shared/frontend-foundation/docs/component-catalog.json' -Raw | ConvertFrom-Json
if ($catalog.package -ne '@his-hope/frontend-foundation') { throw 'Invalid catalog package.' }
if ($catalog.components.Count -lt 6) { throw 'Catalog must describe the core shared components.' }

$uiPublicApi = Get-Content 'shared/frontend-foundation/ui/src/public-api.ts' -Raw
$i18nPublicApi = Get-Content 'shared/frontend-foundation/i18n/src/public-api.ts' -Raw
foreach ($component in $catalog.components) {
  $selector = $component.selector.Split(' / ')[0]
  $sourceName = Split-Path $component.source -Leaf
  if ($selector -and $sourceName -and $sourceName -notin @('his-hope-status-badge.component.ts', 'his-hope-skeleton.component.ts')) {
    $sourcePath = Join-Path 'shared/frontend-foundation' $component.source
    if (-not (Test-Path $sourcePath)) { throw "Catalog source does not exist: $sourcePath" }
  }
}
if ($uiPublicApi -notmatch 'export \* from "\./his-hope-data-table\.component"') { throw 'DataTable is not exported publicly.' }
if ($i18nPublicApi -notmatch 'export \* from "\./his-hope-language-switcher\.component"') { throw 'Language switcher is not exported publicly.' }

Write-Host "Catalog validation passed: $($catalog.components.Count) component entries."
