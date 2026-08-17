[CmdletBinding()]
param(
    [string]$RepositoryRoot = ""
)

$ErrorActionPreference = "Stop"
$scriptRoot = if ([string]::IsNullOrWhiteSpace($PSScriptRoot)) { (Get-Location).Path } else { $PSScriptRoot }
if ([string]::IsNullOrWhiteSpace($RepositoryRoot)) {
    $RepositoryRoot = (Resolve-Path (Join-Path $scriptRoot "../..")).Path
}
$servicesRoot = Join-Path $RepositoryRoot "src/Services"
$policyNamesPath = Join-Path $RepositoryRoot "src/Shared/SharedKernel/Src/His.Hope.SharedKernel/Authorization/AuthorizationPolicyNames.cs"
$permissionsPath = Join-Path $RepositoryRoot "src/Shared/SharedKernel/Src/His.Hope.SharedKernel/Authorization/HisHopePermissions.cs"

if (-not (Test-Path -LiteralPath $policyNamesPath) -or -not (Test-Path -LiteralPath $permissionsPath)) {
    throw "Shared authorization vocabulary files are missing."
}

$rawPermissionLiterals = @(rg -n --glob "*.cs" '"Permission:[a-z0-9.-]+"' $servicesRoot 2>$null)
if ($rawPermissionLiterals.Count -gt 0) {
    $rawPermissionLiterals | Write-Error
    throw "Services contain raw permission policy literals. Use AuthorizationPolicyNames.Permissions.*."
}

$rawPrincipalLiterals = @(Get-ChildItem -LiteralPath $servicesRoot -Recurse -Filter *.cs |
    # EF migration/designer files persist database column names and historical
    # snapshots by design. They are schema metadata, not service vocabulary
    # literals used to make authorization decisions, so keep the check focused
    # on maintainable application/domain source.
    Where-Object { $_.FullName -notmatch '[\\/]Migrations[\\/]' } |
    Select-String -SimpleMatch -Pattern '"principal_type"', '"HumanAdmin"')
if ($rawPrincipalLiterals.Count -gt 0) {
    $rawPrincipalLiterals | Write-Error
    throw "Services contain raw principal/policy vocabulary literals."
}

$permissionSource = Get-Content -Raw -LiteralPath $permissionsPath
$policySource = Get-Content -Raw -LiteralPath $policyNamesPath
$policyCodes = [regex]::Matches($policySource, 'Permission:([a-z0-9.-]+)') |
    ForEach-Object { $_.Groups[1].Value } |
    Sort-Object -Unique
$missing = @($policyCodes | Where-Object { $permissionSource -notmatch [regex]::Escape(('= "' + $_ + '"')) })
if ($missing.Count -gt 0) {
    throw "Policy names missing from HisHopePermissions catalog: $($missing -join ', ')"
}

"SHARED_AUTHORIZATION_VOCABULARY_PASS policies=$($policyCodes.Count)"
