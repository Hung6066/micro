[CmdletBinding()]
param(
    [string]$AdminAppRoot = '',
    [switch]$RunBuild
)

$ErrorActionPreference = 'Stop'
$root = (Resolve-Path "$PSScriptRoot\..\..").Path
if ([string]::IsNullOrWhiteSpace($AdminAppRoot)) {
    $AdminAppRoot = Join-Path $root 'admin-app'
}
$AdminAppRoot = (Resolve-Path $AdminAppRoot).Path

function Require-File([string]$Path) {
    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        throw "ADMIN_IDENTITY_GATE_FAIL missing file: $Path"
    }
}

function Require-Text([string]$Path, [string]$Pattern, [string]$Description) {
    $content = Get-Content -LiteralPath $Path -Raw
    if ($content -notmatch $Pattern) {
        throw "ADMIN_IDENTITY_GATE_FAIL $Description ($Path)"
    }
}

$component = Join-Path $AdminAppRoot 'src\app\features\identity-capabilities\identity-capabilities-page.component.ts'
$routes = Join-Path $AdminAppRoot 'src\app\app.routes.ts'
$guard = Join-Path $AdminAppRoot 'src\app\core\guards\capability-permission.guard.ts'
$service = Join-Path $AdminAppRoot 'src\app\core\services\identity-capabilities.service.ts'
$productionEnvironment = Join-Path $AdminAppRoot 'src\environments\environment.prod.ts'
$en = Join-Path $root 'shared\frontend-foundation\src\i18n\dictionaries\en.ts'
$vi = Join-Path $root 'shared\frontend-foundation\src\i18n\dictionaries\vi-vn.ts'
$provisioningEndpoint = Join-Path $root 'src\Services\IdentityService\IdentityService.Api\Endpoints\DirectoryProvisioningEndpoints.cs'
$mtlsEndpoint = Join-Path $root 'src\Services\IdentityService\IdentityService.Api\Endpoints\MtlsEndpoints.cs'
$identityOperations = Join-Path $AdminAppRoot 'src\app\features\identity-operations\identity-operations-page.component.ts'
$adminApi = Join-Path $AdminAppRoot 'src\app\core\services\admin-api.service.ts'
$incidentEndpoint = Join-Path $root 'src\Services\IdentityService\IdentityService.Api\Endpoints\AdminIncidentEndpoints.cs'
$securitySignalEndpoint = Join-Path $root 'src\Services\IdentityService\IdentityService.Api\Endpoints\SecuritySignalAdminEndpoints.cs'

@($component, $routes, $guard, $service, $productionEnvironment, $en, $vi, $provisioningEndpoint, $mtlsEndpoint, $identityOperations, $adminApi, $incidentEndpoint, $securitySignalEndpoint) | ForEach-Object { Require-File $_ }

Require-Text $component "@his-hope/frontend-foundation" 'component must import shared foundation'
Require-Text $component "HisHopePageLayoutComponent" 'component must use foundation page layout'
Require-Text $component "HisHopePageHeaderComponent" 'component must use foundation page header'
Require-Text $component "hhTranslate" 'component must use the shared i18n pipe'
Require-Text $component "var\(--(space|surface|text|border|radius|font)-" 'component must use theme tokens'
Require-Text $component "Secrets, certificates and vendor credentials remain server-side" 'component must state the secret boundary'
Require-Text $routes 'identity-capabilities' 'identity capability route is missing'
Require-Text $routes 'capabilityPermissionGuard' 'identity capability route must be permission guarded'
Require-Text $guard 'admin\.settings\.read' 'guard must require the read permission'
Require-Text $service 'sanitizeError|normalizeError' 'API facade must normalize errors'
Require-Text $productionEnvironment 'RuntimeConfigService' 'production environment must use the shared runtime contract'
if ((Get-Content -LiteralPath $productionEnvironment -Raw) -match 'localhost') {
    throw "ADMIN_IDENTITY_GATE_FAIL production environment contains a localhost fallback."
}
Require-Text $en 'identityCapabilities' 'English identity capability dictionary key is missing'
Require-Text $vi 'identityCapabilities' 'Vietnamese identity capability dictionary key is missing'
Require-Text $provisioningEndpoint '(Permission:admin\.users\.read|AuthorizationPolicyNames\.Permissions\.AdminUsersRead)' 'provisioning read routes must use the read permission'
Require-Text $provisioningEndpoint '(Permission:admin\.users\.write|AuthorizationPolicyNames\.Permissions\.AdminUsersWrite)' 'provisioning mutation routes must use the write permission'
Require-Text $mtlsEndpoint '(Permission:admin\.clients\.read|AuthorizationPolicyNames\.Permissions\.AdminClientsRead)' 'mTLS read routes must use the client read permission'
Require-Text $mtlsEndpoint '(Permission:admin\.clients\.write|AuthorizationPolicyNames\.Permissions\.AdminClientsWrite)' 'mTLS mutation routes must use the client write permission'
Require-Text $identityOperations 'revokeAllSessions|resetCredentials|previewImport|reconcile|retrySsf' 'identity operations UI must expose incident, lifecycle and outbox workflows'
Require-Text $adminApi 'revokeAllAdminSessions|resetAdminCredentials|previewUserImport|reconcileProvisioning|retrySecuritySignal' 'admin API facade must expose identity operations contracts'
Require-Text $incidentEndpoint 'sessions/revoke-all' 'server must expose admin session revocation'
Require-Text $incidentEndpoint 'credentials/reset' 'server must expose admin credential reset'
Require-Text $securitySignalEndpoint 'outbox/{id:guid}/retry' 'server must expose SSF outbox replay'

$forbiddenSecrets = Select-String -Path $component -Pattern '(clientSecret|privateKey|signingKey|accessToken|refreshToken|serviceAccountKey)' -CaseSensitive:$false
if ($forbiddenSecrets) {
    throw "ADMIN_IDENTITY_GATE_FAIL UI references a vendor secret or private key field."
}

if ($RunBuild) {
    Push-Location $AdminAppRoot
    try {
        & npm run build
        if ($LASTEXITCODE -ne 0) { throw "ADMIN_IDENTITY_GATE_FAIL Angular build exited $LASTEXITCODE" }
    }
    finally { Pop-Location }
}

Write-Output "ADMIN_IDENTITY_CAPABILITIES_VALIDATED"
