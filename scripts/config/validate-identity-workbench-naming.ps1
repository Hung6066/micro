[CmdletBinding()]
param([string]$RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path)

$ErrorActionPreference = 'Stop'
$route = Join-Path $RepoRoot 'src\Shared\Contracts\His.Hope.Contracts\Identity\IdentityApiRoutes.cs'
$db = Join-Path $RepoRoot 'src\Services\IdentityService\IdentityService.Infrastructure\Persistence\IdentityDbContext.cs'
$catalog = Join-Path $RepoRoot 'src\Services\IdentityService\IdentityService.Infrastructure\Persistence\IdentityWorkbenchTableNames.cs'
$ts = Join-Path $RepoRoot 'admin-app\src\app\core\contracts\identity-workbench.naming.ts'
$menu = Join-Path $RepoRoot 'admin-app\src\app\app.component.ts'
$featureRoot = Join-Path $RepoRoot 'admin-app\src\app\features'
$required = @('overview','scopes','services','permission-sets','assignments','workload-roles','groups','boundaries','resource-policies','api-audiences','trusted-issuers')

foreach ($file in @($route,$db,$catalog,$ts,$menu,$featureRoot)) { if (-not (Test-Path -LiteralPath $file)) { throw "missing_file:$file" } }
$routeText = Get-Content -Raw -LiteralPath $route
$tsText = Get-Content -Raw -LiteralPath $ts
$dbText = Get-Content -Raw -LiteralPath $db
$catalogText = Get-Content -Raw -LiteralPath $catalog
$menuText = Get-Content -Raw -LiteralPath $menu
$featureText = (Get-ChildItem -LiteralPath $featureRoot -Filter '*.ts' -Recurse | ForEach-Object { Get-Content -Raw -LiteralPath $_.FullName }) -join "`n"

foreach ($name in $required) {
  $camel = (($name -split '-') | ForEach-Object { $_.Substring(0,1).ToUpperInvariant() + $_.Substring(1) }) -join ''
  $routeConstant = if ($name -eq 'api-audiences') { 'ApiAudiences' } elseif ($name -eq 'trusted-issuers') { 'TrustedIssuers' } elseif ($name -eq 'overview') { 'Overview' } else { (($name -split '-') | ForEach-Object { $_.Substring(0,1).ToUpperInvariant() + $_.Substring(1) }) -join '' }
  if ($routeText -notmatch "public const string $routeConstant\s*=") { throw "missing_backend_resource:$name" }
  if ($tsText -notmatch "\b$name\b") { throw "missing_frontend_resource:$name" }
  $menuRoute = @{
    scopes = '/iam/scopes'; services = '/iam/services';
    'permission-sets' = '/iam/permission-sets'; assignments = '/iam/assignments';
    'workload-roles' = '/iam/workload-roles'; groups = '/iam/groups';
    boundaries = '/iam/boundaries'; 'resource-policies' = '/iam/resource-policies';
    'api-audiences' = '/iam/api-audiences'; 'trusted-issuers' = '/iam/trusted-issuers';
    overview = '/iam/overview'
  }[$name]
  if ($menuRoute) {
    if ($menuText -notmatch [regex]::Escape($menuRoute)) { throw "missing_menu_route:$name" }
  }
}

if ($dbText -match 'ToTable\("iam_') { throw 'db_mapping_bypasses_table_catalog' }
if ($catalogText -notmatch 'iam_[a-z0-9_]+') { throw 'empty_table_catalog' }
if ($tsText -notmatch "IDENTITY_WORKBENCH_ACTIONS") { throw 'missing_action_catalog' }
if ($menuText -match 'id:\s*''[^'']+\s+[^'']') { throw 'menu_id_must_be_kebab_case' }
foreach ($routeComponent in @('IamOverviewPageComponent','UsersPageComponent','IamWorkloadSessionsPageComponent','IamRevocationsPageComponent','IamEffectiveAccessPageComponent','IamPolicySimulatorPageComponent','IamAccessDiffPageComponent','IamUnusedPermissionsPageComponent','IamAuditIntegrationsPageComponent')) {
  if ($featureText -notmatch "export class $routeComponent") { throw "missing_workbench_component:$routeComponent" }
}

Write-Output "IDENTITY_WORKBENCH_NAMING_VALIDATED resources=$($required.Count)"
