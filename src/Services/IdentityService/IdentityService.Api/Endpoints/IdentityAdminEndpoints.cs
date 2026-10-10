using System.Security.Claims;
using System.Text.Json;
using His.Hope.Contracts.Identity;
using His.Hope.IdentityService.Api.Authorization;
using His.Hope.IdentityService.Api.Composition;
using His.Hope.IdentityService.Application.Conglomerate;
using His.Hope.IdentityService.Application.Interfaces;
using His.Hope.IdentityService.Application.Services;
using His.Hope.IdentityService.Domain.Entities;
using His.Hope.IdentityService.Infrastructure.Persistence;
using His.Hope.IdentityService.Infrastructure.Services;
using His.Hope.Infrastructure.Caching;
using His.Hope.SharedKernel.Authorization;
using His.Hope.SharedKernel.Protocol;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using OpenIddictEntityFrameworkCore = OpenIddict.EntityFrameworkCore.Models;

namespace His.Hope.IdentityService.Api.Endpoints;

public static class IdentityAdminEndpoints
{
    public static void MapIdentityAdminEndpoints(this WebApplication app)
    {
        var auth = app.MapGroup(IdentityApiRoutes.Auth).RequireCors();

        // Admin API endpoints (for frontend admin module)
        var admin = app.MapGroup("/api/v1/admin")
            .RequireAuthorization(AuthorizationConstants.Policies.HumanAdmin)
            .BlockEndUserPortal()
            .RestrictCustomerOperatorPaths();
        auth.MapIdentityPermissionEndpoints(admin);
        admin.MapUserEndpoints();
        admin.MapRoleEndpoints();
        admin.MapAccessGovernanceEndpoints();
        admin.MapAuthorizationChangeRequestEndpoints();
        admin.MapSupportElevationEndpoints();
        var iamControlPlane = app.MapGroup(IdentityApiRoutes.AdminIam)
            .RequireAuthorization(AuthorizationConstants.Policies.HumanSuperAdmin)
            .RequireOperatorPortal()
            .BlockEndUserPortal();
        iamControlPlane.MapIamControlPlaneEndpoints();
        app.MapAdminIncidentEndpoints();
        admin.MapSettingsEndpoints();
        admin.MapAuditLogEndpoints();

        // Canonical Identity Workbench aliases. The legacy /api/v1/admin routes above
        // remain available for older clients; new admin-app calls use /admin/iam so
        // governance and audit resources share one route vocabulary with IAM catalog
        // resources. Endpoint-level permissions are defined by the mapped handlers.
        var iamWorkbench = app.MapGroup(IdentityApiRoutes.IdentityWorkbench.Base)
            .RequireAuthorization(AuthorizationConstants.Policies.HumanSuperAdmin)
            .RequireOperatorPortal()
            .BlockEndUserPortal();
        iamWorkbench.MapAccessGovernanceEndpoints();
        iamWorkbench.MapAuthorizationChangeRequestEndpoints();
        iamWorkbench.MapAuditLogEndpoints();
        iamWorkbench.MapAdminIncidentEndpoints();
        iamWorkbench.MapIdentityWorkbenchDedicatedEndpoints();
        admin.MapGroup("/clients").MapClientEndpoints();
        ClientEndpoints.MapDynamicClientRegistration(app);
        admin.MapBulkImportEndpoints();
        admin.MapAdminTableEndpoints();
        admin.MapTableViewEndpoints();
        admin.MapTableAnalysisEndpoints();

        app.MapMobilePlatformEndpoints();
        app.MapGet("/api/v1/auth/identity-login.js", (HttpContext context) =>
        {
            var scriptPath = ResolveIdentityLoginScriptPath(app.Environment);
            if (!File.Exists(scriptPath))
                return Results.NotFound();

            context.Response.Headers.CacheControl = "no-store, no-cache, must-revalidate";
            context.Response.Headers.Pragma = "no-cache";
            return Results.File(scriptPath, "text/javascript; charset=utf-8");
        })
            .AllowAnonymous();
        app.MapPasskeyEndpoints();
        admin.MapGet("/me/switchable-tenants", async (
            HttpContext httpContext,
            UserManager<User> userManager,
            IdentityDbContext db,
            IConglomerateTenantRegistry registry,
            CancellationToken ct) =>
        {
            if (PortalClassGuard.EnsureOperatorPortal(httpContext.User) is { } portalError)
                return portalError;

            var memberships = IamTenantScopeResolver.GetMemberships(httpContext.User);
            if (memberships.Count == 0)
            {
                var userId = httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier)
                    ?? httpContext.User.FindFirstValue(His.Hope.SharedKernel.Protocol.HisHopeProtocolConstants.Claims.Subject);
                if (Guid.TryParse(userId, out var parsedUserId))
                {
                    var user = await userManager.FindByIdAsync(parsedUserId.ToString());
                    if (user is not null)
                    {
                        memberships = (await userManager.GetClaimsAsync(user))
                            .Where(claim => claim.Type == IamTenantScopeResolver.TenantMembershipClaimType)
                            .Select(claim => claim.Value)
                            .Distinct(StringComparer.OrdinalIgnoreCase)
                            .ToArray();
                    }
                }
            }

            if (memberships.Count == 0)
                return Results.Ok(new { tenants = Array.Empty<object>() });

            var switchableKeys = new HashSet<string>(memberships, StringComparer.OrdinalIgnoreCase);
            foreach (var membership in memberships)
            {
                foreach (var customerKey in registry.GetCustomerTenantsForOperator(membership))
                    switchableKeys.Add(customerKey);
            }

            var tenantScopes = await db.IamScopes.AsNoTracking()
                .Where(scope => scope.IsActive && scope.Kind == "tenant")
                .ToListAsync(ct);

            var tenants = tenantScopes
                .Where(scope => switchableKeys.Contains(scope.Key))
                .OrderBy(scope => registry.IsCustomerTenant(scope.Key))
                .ThenBy(scope => scope.DisplayName)
                .Select(scope => new
                {
                    key = scope.Key,
                    displayName = scope.DisplayName,
                    scopeId = scope.Id,
                    tenantClass = registry.GetTenantClass(scope.Key),
                    isCustomerSupport = registry.IsCustomerTenant(scope.Key)
                })
                .ToArray();

            return Results.Ok(new { tenants });
        }).RequireAuthorization();
        admin.MapGroup("/consents").RequireAuthorization(AuthorizationPolicyNames.Permissions.AdminUsersRead).MapGet("/", async (
            int page = PaginationDefaults.DefaultPage,
            int pageSize = PaginationDefaults.DefaultPageSize,
            string? search = null,
            string? clientId = null,
            string? sort = null,
            IdentityDbContext db = null!,
            HttpContext http = null!,
            CancellationToken ct = default) =>
        {
            if (page < PaginationDefaults.DefaultPage || pageSize is < 1 or > PaginationDefaults.MaxPageSize)
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["pageSize"] = [$"pageSize must be between 1 and {PaginationDefaults.MaxPageSize} and page must be at least {PaginationDefaults.DefaultPage}."] });
            if (search?.Length > 100 || clientId?.Length > 100 || sort?.Length > 100)
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["search"] = ["Search must be 100 characters or fewer."] });

            var tenantFilter = IamTenantHttpContext.RequireFilter(http);

            var query = db.ClientConsents.AsNoTracking()
                .TagWith("Identity.Consents.GetConsents")
                .Where(c => c.IsActive);
            if (tenantFilter.AllowedTenantKeys is { } consentTenantKeys)
            {
                // Keep the membership predicate rooted at UserClaims. Calling
                // WhereTenantMembership through a nested Users query produces
                // an EF Core NavigationTreeExpression that PostgreSQL cannot
                // translate for this consent projection.
                var normalizedTenantKeys = consentTenantKeys
                    .Select(key => key.ToLowerInvariant())
                    .ToArray();
                query = query.Where(consent => db.UserClaims.Any(claim =>
                    claim.UserId == consent.UserId &&
                    claim.ClaimType == IamTenantScopeResolver.TenantMembershipClaimType &&
                    claim.ClaimValue != null && normalizedTenantKeys.Contains(claim.ClaimValue.ToLower())));
            }
            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.Trim();
                query = query.Where(c => c.ClientId.Contains(term));
            }
            if (!string.IsNullOrWhiteSpace(clientId))
                query = query.Where(c => c.ClientId.Contains(clientId.Trim()));

            var sortParts = sort?.Split(':', 2, StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            var descending = sortParts?.Length > 1 && string.Equals(sortParts[1], "desc", StringComparison.OrdinalIgnoreCase);
            query = (sortParts?.FirstOrDefault()?.ToLowerInvariant(), descending) switch
            {
                ("clientid", false) => query.OrderBy(c => c.ClientId),
                ("clientid", true) => query.OrderByDescending(c => c.ClientId),
                ("created", false) => query.OrderBy(c => c.GrantedAt),
                _ => query.OrderByDescending(c => c.GrantedAt)
            };

            var totalCount = await query.CountAsync(ct);
            var consents = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(ct);

            var userIds = consents.Select(c => c.UserId).Distinct().ToArray();
            var users = await db.Users.Where(u => userIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.UserName ?? u.Email ?? u.Id.ToString(), ct);
            var items = consents.Select(c => new
            {
                id = c.Id,
                subject = users.GetValueOrDefault(c.UserId, c.UserId.ToString()),
                clientId = c.ClientId,
                scopes = JsonSerializer.Deserialize<List<string>>(c.Scopes) ?? new List<string>(),
                created = c.GrantedAt,
                expiresAt = c.ExpiresAt
            }).ToList();
            return Results.Ok(new PagedResult<object>(items, totalCount, page, pageSize));
        }).WithTenantReadScope(HisHopePermissions.Admin.UsersRead);

        admin.MapGet("/dashboard", async (
            IdentityDbContext db,
            IConglomerateTenantRegistry tenantRegistry,
            HttpContext http,
            CancellationToken ct) =>
        {
            var filter = IamTenantHttpContext.RequireFilter(http);

            var usersQuery = db.Users.AsNoTracking().WhereTenantMembership(db, filter.AllowedTenantKeys);
            var totalUsers = await usersQuery.CountAsync(ct);
            var activeUsers = await usersQuery.Where(userEntity => userEntity.IsActive).CountAsync(ct);

            var rolesQuery = db.Roles.AsNoTracking().AsQueryable();
            if (filter.AllowedTenantKeys is { Count: > 0 } allowedTenantKeys)
            {
                rolesQuery = rolesQuery.Where(role => db.UserRoles.Any(userRole =>
                    userRole.RoleId == role.Id &&
                    db.UserClaims.Any(claim =>
                        claim.UserId == userRole.UserId &&
                        claim.ClaimType == IamTenantScopeResolver.TenantMembershipClaimType &&
                        claim.ClaimValue != null && allowedTenantKeys.Contains(claim.ClaimValue))));
            }
            var totalRoles = await rolesQuery.CountAsync(ct);

            var clientsQuery = db.Set<OpenIddictEntityFrameworkCore.OpenIddictEntityFrameworkCoreApplication>().AsNoTracking();
            var allowedClientIds = IamTenantQueryExtensions.ResolveAllowedClientIds(tenantRegistry, filter);
            if (allowedClientIds is not null)
                clientsQuery = clientsQuery.Where(client => allowedClientIds.Contains(client.ClientId ?? string.Empty));
            var totalClients = await clientsQuery.CountAsync(ct);

            var consentsQuery = db.ClientConsents.AsNoTracking().Where(consent => consent.IsActive);
            if (allowedClientIds is not null)
                consentsQuery = consentsQuery.Where(consent => allowedClientIds.Contains(consent.ClientId));
            if (filter.AllowedTenantKeys is { Count: > 0 } tenantKeys)
            {
                consentsQuery = consentsQuery.Where(consent => db.UserClaims.Any(claim =>
                    claim.UserId == consent.UserId &&
                    claim.ClaimType == IamTenantScopeResolver.TenantMembershipClaimType &&
                    claim.ClaimValue != null && tenantKeys.Contains(claim.ClaimValue)));
            }
            var activeConsents = await consentsQuery.CountAsync(ct);

            return Results.Ok(new { totalUsers, activeUsers, totalRoles, totalClients, activeConsents });
        }).RequireAuthorization(AuthorizationPolicyNames.Permissions.AdminUsersRead)
            .WithTenantReadScope(HisHopePermissions.Admin.UsersRead);

        // Manual LDAP sync trigger
        admin.MapPost("/ldap/sync", async (LdapSyncService syncService, CancellationToken ct) =>
        {
            await syncService.SyncAsync(ct);
            return Results.Ok(new { message = "LDAP sync completed" });
        }).RequireAuthorization(AuthorizationPolicyNames.Permissions.AdminUsersWrite);

        // Key rotation (admin only)
        admin.MapPost("/security/rotate-signing-key", async (VaultKeyService keyService, CancellationToken ct) =>
        {
            await keyService.RotateKeyAsync(ct);
            return Results.Ok(new { message = "Signing key rotated successfully" });
        }).RequireAuthorization(AuthorizationPolicyNames.Permissions.AdminSettingsWrite);
    }

    private static string ResolveIdentityLoginScriptPath(IWebHostEnvironment environment)
    {
        var candidates = new[]
        {
            string.IsNullOrWhiteSpace(environment.WebRootPath) ? null : Path.Combine(environment.WebRootPath, "js", "identity-login.js"),
            Path.Combine(environment.ContentRootPath, "wwwroot", "js", "identity-login.js"),
            Path.Combine(AppContext.BaseDirectory, "wwwroot", "js", "identity-login.js")
        }.Where(path => !string.IsNullOrWhiteSpace(path)).Cast<string>().ToList();
        foreach (var candidate in candidates)
            if (File.Exists(candidate)) return candidate;
        foreach (var root in EnumerateSearchRoots(environment.ContentRootPath).Concat(EnumerateSearchRoots(Directory.GetCurrentDirectory())).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var sourceCandidate = Path.Combine(root, "src", "Services", "IdentityService", "IdentityService.Api", "wwwroot", "js", "identity-login.js");
            if (File.Exists(sourceCandidate)) return sourceCandidate;
        }
        return candidates[0];
    }

    private static IEnumerable<string> EnumerateSearchRoots(string? startPath)
    {
        if (string.IsNullOrWhiteSpace(startPath)) yield break;
        for (var current = new DirectoryInfo(startPath); current is not null; current = current.Parent) yield return current.FullName;
    }
}
