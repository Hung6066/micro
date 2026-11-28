[CmdletBinding()]
param(
    [string]$Root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path,
    [string]$Output = 'artifacts/evidence/tenant-context-seams.json'
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$roots = @(
    @{ name = 'application'; path = 'src/Services'; filter = '*Application*' },
    @{ name = 'persistence'; path = 'src/Services'; filter = '*Infrastructure*' },
    @{ name = 'api'; path = 'src/Services'; filter = '*.Api' }
)
$rows = [System.Collections.Generic.List[object]]::new()
foreach ($scope in $roots) {
    $base = Join-Path $Root $scope.path
    if (-not (Test-Path -LiteralPath $base)) { continue }
    $projects = Get-ChildItem -LiteralPath $base -Directory -Recurse | Where-Object Name -like $scope.filter
    foreach ($project in $projects) {
        $files = Get-ChildItem -LiteralPath $project.FullName -Filter '*.cs' -File -Recurse |
            Where-Object FullName -notmatch '[\\/](bin|obj)[\\/]'
        $count = 0
        foreach ($file in $files) {
            $count += @((Select-String -LiteralPath $file.FullName -Pattern '\btenantKey\b' -AllMatches -ErrorAction SilentlyContinue)).Count
        }
        if ($count -gt 0) {
            $rows.Add([pscustomobject]@{ scope = $scope.name; project = $project.Name; occurrences = $count })
        }
    }
}

$edgeViolations = [System.Collections.Generic.List[string]]::new()
$edgeRoots = @('admin-app','dashboard-app','internal-operator-app','manufacturing-buyer-app','src/Frontend/his-hope-app','shared/frontend-foundation')
foreach ($relative in $edgeRoots) {
    $path = Join-Path $Root $relative
    if (-not (Test-Path -LiteralPath $path)) { continue }
    $matches = Get-ChildItem -LiteralPath $path -Recurse -File -Include '*.ts','*.html','*.tsx','*.jsx' |
        Where-Object FullName -notmatch '(node_modules|dist|\.spec\.|\.test\.)' |
        Select-String -Pattern 'tenantKey=' -SimpleMatch -ErrorAction SilentlyContinue
    foreach ($match in $matches) { $edgeViolations.Add("$($match.Path):$($match.LineNumber)") }
}

$artifact = [pscustomobject]@{
    generatedAtUtc = [DateTimeOffset]::UtcNow.ToString('O')
    status = if ($edgeViolations.Count -eq 0) { 'pass' } else { 'fail' }
    edgeLegacySelectorOccurrences = $edgeViolations.Count
    edgeLegacySelectorLocations = @($edgeViolations)
    internalSeams = @($rows | Sort-Object scope, occurrences -Descending)
    policy = 'Internal tenantKey is allowed only for persistence partition predicates, event envelope compatibility, or explicit cross-tenant safety checks; new HTTP/DTO/query selectors are forbidden.'
}
$outputPath = Join-Path $Root $Output
New-Item -ItemType Directory -Force -Path (Split-Path -Parent $outputPath) | Out-Null
$artifact | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $outputPath -Encoding UTF8
if ($edgeViolations.Count -gt 0) { throw "Tenant context edge audit failed: $($edgeViolations.Count) legacy selector occurrences." }
Write-Host "Tenant context seam audit passed: $($rows.Count) internal seam entries; edge selectors absent."
