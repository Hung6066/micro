[CmdletBinding()]
param(
    [string]$RepositoryRoot = (Get-Location).Path
)

$ErrorActionPreference = 'Stop'

function Assert-Contains([string]$Path, [string]$Pattern, [string]$Description) {
    $content = Get-Content -LiteralPath (Join-Path $RepositoryRoot $Path) -Raw
    if ($content -notmatch [regex]::Escape($Pattern)) {
        throw "Authorization contract failed: $Description ($Path)"
    }
}

Assert-Contains 'src/Services/IdentityService/IdentityService.Api/Endpoints/BulkImportEndpoints.cs' '.RequireAuthorization(AuthorizationPolicyNames.Permissions.AdminUsersWrite)' 'bulk writes require admin.users.write via canonical policy names'
Assert-Contains 'src/Services/IdentityService/IdentityService.Api/Endpoints/BulkImportEndpoints.cs' '.RequireAuthorization(AuthorizationPolicyNames.Permissions.AdminUsersRead)' 'bulk preview requires admin.users.read via canonical policy names'
Assert-Contains 'src/Shared/Authorization/His.Hope.Authorization/Handlers/PermissionHandler.cs' 'token has no permissions claim' 'canonical handler fails closed without permission claims'
Assert-Contains 'src/Shared/Infrastructure/His.Hope.Infrastructure/Security/Authorization/Handlers/PermissionHandler.cs' 'token has no matching permission claim' 'legacy handler fails closed without permission claims'
Assert-Contains 'src/Services/IdentityService/IdentityService.Infrastructure/Persistence/IdentityDbContext.cs' 'entity.ToTable("user_facilities")' 'facility membership is persisted'
Assert-Contains 'src/Services/IdentityService/IdentityService.Application/OpenIddict/OpenIddictHandlers.cs' 'facility_ids' 'tokens expose all active facility memberships'
Assert-Contains 'src/Shared/SharedKernel/Src/His.Hope.SharedKernel/Authorization/HisHopePermissions.cs' 'facility.cross' 'cross-facility access is a registered permission'
Assert-Contains 'src/Services/IdentityService/IdentityService.Infrastructure/Facility/FacilityResolutionMiddleware.cs' 'facility.cross' 'facility middleware uses the canonical cross-facility permission'
Assert-Contains 'src/Services/IdentityService/IdentityService.Api/Endpoints/AccessGovernanceEndpoints.cs' 'AuthorizationPolicyBundles' 'published policy bundles use a durable registry'
Assert-Contains 'src/Services/IdentityService/IdentityService.Api/Endpoints/AccessGovernanceEndpoints.cs' 'published_policy_bundle_not_found' 'bundle reads fail closed when no durable release exists'
Assert-Contains 'src/Services/IdentityService/IdentityService.Api/Endpoints/AccessGovernanceEndpoints.cs' 'idempotent = true' 'bundle publication is idempotent by content hash'
Assert-Contains 'src/Services/IdentityService/IdentityService.Api/Endpoints/RoleEndpoints.cs' 'StepUpAuthenticationGuard.RequireFreshMfa(http)' 'role publish and rollback require fresh step-up MFA'
Assert-Contains 'src/Services/IdentityService/IdentityService.Api/Endpoints/IamControlPlaneEndpoints.cs' 'RequireFreshMfaForMutationFilter' 'IAM mutations require fresh step-up MFA'
Assert-Contains 'src/Services/IdentityService/IdentityService.Api/Endpoints/ClientEndpoints.cs' 'RequireFreshMfaForMutationFilter' 'OIDC client mutations require fresh step-up MFA'
Assert-Contains '.github/workflows/platform-quality-gates.yml' 'id-token: write' 'policy artifact signing has OIDC identity-token permission'
Assert-Contains '.github/workflows/platform-quality-gates.yml' 'cosign sign-blob --yes' 'policy catalog is signed in the main CI release path'
Assert-Contains '.github/workflows/platform-quality-gates.yml' 'cosign verify-blob' 'policy catalog signature is verified before artifact delivery'
Assert-Contains '.github/workflows/platform-quality-gates.yml' 'authorization-policy-catalog.sig' 'policy signature is delivered with the catalog evidence'
Assert-Contains 'src/Shared/Infrastructure/His.Hope.Infrastructure/Caching/AuthorizationCacheKeyPartitioner.cs' 'facility_ids' 'cache keys are partitioned by authorization scope'
Assert-Contains 'src/Services/PatientService/PatientService.Infrastructure/Persistence/PatientDbContext.cs' 'HasQueryFilter' 'patient reads are filtered at the database query boundary'
Assert-Contains 'src/Services/AppointmentService/AppointmentService.Infrastructure/Persistence/AppointmentDbContext.cs' 'HasQueryFilter' 'appointment reads are filtered at the database query boundary'
Assert-Contains 'src/Services/ClinicalService/ClinicalService.Infrastructure/Persistence/ClinicalDbContext.cs' 'HasQueryFilter' 'clinical reads are filtered at the database query boundary'
Assert-Contains 'src/Services/LabService/LabService.Infrastructure/Persistence/LabDbContext.cs' 'HasQueryFilter' 'lab reads are filtered at the database query boundary'
Assert-Contains 'src/Services/BillingService/BillingService.Infrastructure/Persistence/BillingDbContext.cs' 'HasQueryFilter' 'billing reads are filtered at the database query boundary'
Assert-Contains 'src/Services/PharmacyService/PharmacyService.Infrastructure/Persistence/PharmacyDbContext.cs' 'HasQueryFilter' 'pharmacy reads are filtered at the database query boundary'
Assert-Contains 'src/Services/PatientService/PatientService.Infrastructure/Projections/PatientReadDbContext.cs' 'HasQueryFilter' 'patient read projections are filtered at the database query boundary'
Assert-Contains 'src/Services/PatientService/PatientService.Infrastructure/Projections/PatientProjection.cs' 'FacilityId' 'patient read projections carry facility scope'

Get-ChildItem -LiteralPath (Join-Path $RepositoryRoot 'src/Services') -Recurse -Filter '*AddFacilityScope.cs' |
    ForEach-Object {
        $migration = Get-Content -LiteralPath $_.FullName -Raw
        $downMarker = $migration.IndexOf('protected override void Down', [StringComparison]::Ordinal)
        $up = if ($downMarker -ge 0) { $migration.Substring(0, $downMarker) } else { $migration }
        if ($up -match 'DropColumn|DropTable|DropIndex|AlterColumn') {
            throw "Authorization contract failed: destructive operation in facility migration ($($_.FullName))"
        }
        if ($up -notmatch 'AddColumn') {
            throw "Authorization contract failed: facility migration has no additive column ($($_.FullName))"
        }
    }

Write-Output 'Authorization contract passed.'
