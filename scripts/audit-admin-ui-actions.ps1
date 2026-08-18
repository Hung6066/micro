[CmdletBinding()]
param(
    [switch]$FailOnViolation
)

$ErrorActionPreference = "Stop"
$adminRoot = Join-Path $PSScriptRoot "..\admin-app\src\app\features"
$workspaceRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$files = Get-ChildItem $adminRoot -Recurse -Filter *.ts
$violations = @()

foreach ($file in $files) {
    $content = Get-Content $file.FullName -Raw
    $patterns = @(
        @{ Pattern = 'mat-(raised|flat|stroked|icon)-button'; Rule = "Use hh-action-button instead of raw Material action buttons" },
        @{ Pattern = 'class\s*=\s*["'']hh-button'; Rule = "Use hh-action-button instead of hh-button CSS classes" },
        @{ Pattern = 'class\s*=\s*["'']hh-icon-button'; Rule = "Use hh-action-button with mode=icon-only" }
    )

    foreach ($rule in $patterns) {
        if ($content -match $rule.Pattern) {
            $violations += [pscustomobject]@{
                File = $file.FullName.Substring($workspaceRoot.Length).TrimStart("\")
                Rule = $rule.Rule
            }
        }
    }
}

$standardized = ($files | ForEach-Object {
        if ((Get-Content $_.FullName -Raw) -match 'hh-action-button') { $_ }
    }).Count

Write-Output "Admin UI action audit"
Write-Output "Standardized files: $standardized"
Write-Output "Violating files: $(($violations | Select-Object -ExpandProperty File -Unique).Count)"

$violations | Sort-Object File, Rule -Unique | Format-Table -AutoSize

if ($FailOnViolation -and $violations.Count -gt 0) {
    exit 1
}