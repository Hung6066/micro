using System.Security.Claims;
using System.Text.Json;
using His.Hope.Authorization;
using His.Hope.IdentityService.Domain.Entities;
using His.Hope.IdentityService.Infrastructure.Persistence;
using His.Hope.IdentityService.Api.Authorization;
using His.Hope.SharedKernel.Authorization;
using His.Hope.Contracts.Identity;
using His.Hope.Infrastructure.Security;
using His.Hope.Infrastructure.Audit;
using His.Hope.IdentityService.Application.Interfaces;
using His.Hope.IdentityService.Infrastructure.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using OpenIddict.Abstractions;

namespace His.Hope.IdentityService.Api.Endpoints;

/// <summary>Minimal server-side IAM control plane for scopes, services, permission sets and assignments.</summary>
public static class IamControlPlaneEndpoints
{
    public static void MapIamControlPlaneEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app is RouteGroupBuilder routeGroup
            ? routeGroup
            : app.MapGroup(IdentityApiRoutes.AdminIam).RequireAuthorization(AuthorizationConstants.Policies.HumanAdmin);

        // Identity Workbench owns the canonical identity/application surfaces.
        // The legacy /admin/users and /admin/clients routes remain available for
        // older clients, while new callers use these aliases under /admin/iam.
        group.MapUserEndpoints();
        group.MapGroup("/clients").MapClientEndpoints();

        group.MapGet("/external-identities", async (
            IConfiguration config,
            ExternalIdentityProviderRuntime runtime,
            HttpContext http,
            CancellationToken ct) =>
        {
            _ = IamTenantHttpContext.RequireFilter(http);

            var providers = new List<object>();
            if (!string.IsNullOrWhiteSpace(config["Authentication:Google:ClientId"]))
                providers.Add(new { provider = "Google", displayName = "Google", icon = "google", protocol = "oidc" });
            if (!string.IsNullOrWhiteSpace(config["Authentication:Microsoft:ClientId"]))
                providers.Add(new { provider = "Microsoft", displayName = "Microsoft", icon = "microsoft", protocol = "oidc" });
            if (!string.IsNullOrWhiteSpace(config["Authentication:Entra:ClientId"]) && Uri.TryCreate(config["Authentication:Entra:Authority"], UriKind.Absolute, out _))
                providers.Add(new { provider = "Entra", displayName = "Microsoft Entra ID", icon = "microsoft", protocol = "oidc" });
            foreach (var source in config.GetSection("Authentication:ExternalSources").GetChildren())
            {
                var name = source["Name"];
                if (!string.IsNullOrWhiteSpace(name) && Uri.TryCreate(source["Authority"], UriKind.Absolute, out var authority) && authority.Scheme == Uri.UriSchemeHttps)
                    providers.Add(new { provider = name, displayName = source["DisplayName"] ?? name, icon = "openid", protocol = "oidc" });
            }
            var saml = await runtime.GetSamlAsync(ct);
            if (saml.Enabled && !string.IsNullOrWhiteSpace(saml.IdpMetadata))
                providers.Add(new { provider = "Saml", displayName = "SAML SSO", icon = "business", protocol = "saml" });
            return Results.Ok(new { schemaVersion = "iam-external-identities.v1", evaluatedAt = DateTime.UtcNow, providers });
        }).RequireAuthorization(AuthorizationPolicyNames.Permissions.AdminRolesRead)
            .WithTenantReadScope(HisHopePermissions.Admin.RolesRead);

        group.MapGet("/service-principals", async (IdentityDbContext db, HttpContext http, CancellationToken ct) =>
        {
            var filter = IamTenantHttpContext.RequireFilter(http);

            var query = db.IamWorkloadRoles.AsNoTracking();
            if (filter.AllowedScopeIds is not null)
                query = query.Where(item => filter.AllowedScopeIds.Contains(item.ScopeId));

            return Results.Ok(await query.OrderBy(x => x.Key).Select(x => new
            {
                id = x.Id,
                key = x.Key,
                displayName = x.DisplayName,
                principalType = "workload",
                audience = x.Audience,
                scopeId = x.ScopeId,
                isActive = x.IsActive,
                lifecycleStatus = x.IsActive ? "active" : "inactive"
            }).ToListAsync(ct));
        })
            .RequireAuthorization(AuthorizationPolicyNames.Permissions.AdminRolesRead)
            .WithTenantReadScope(HisHopePermissions.Admin.RolesRead);

        group.MapGet("/overview", async (IdentityDbContext db, HttpContext http, CancellationToken ct) =>
        {
            var filter = IamTenantHttpContext.RequireFilter(http);

            var now = DateTime.UtcNow;
            var dayAgo = now.AddDays(-1);
            var scopeIds = filter.AllowedScopeIds;

            return Results.Ok(new
            {
                schemaVersion = "iam-overview.v1",
                evaluatedAt = now,
                scopes = await CountScopedAsync(db.IamScopes.Where(item => item.IsActive), scopeIds, item => item.Id, ct),
                services = await db.IamServiceDefinitions.CountAsync(item => item.IsActive, ct),
                publishedPermissionSets = await CountScopedAsync(
                    db.IamPermissionSets.Where(item => item.LifecycleStatus == AuthorizationConstants.LifecycleStatuses.Published),
                    scopeIds,
                    item => item.ScopeId,
                    ct),
                activeAssignments = await CountScopedAsync(
                    db.IamPermissionSetAssignments.Where(item =>
                        item.Status == AuthorizationConstants.LifecycleStatuses.Active &&
                        (item.ExpiresAt == null || item.ExpiresAt > now)),
                    scopeIds,
                    item => item.ScopeId,
                    ct),
                groups = await CountScopedAsync(db.IamGroups.Where(item => item.IsActive), scopeIds, item => item.ScopeId, ct),
                workloadRoles = await CountScopedAsync(db.IamWorkloadRoles.Where(item => item.IsActive), scopeIds, item => item.ScopeId, ct),
                pendingAccessRequests = await CountGovernanceSubjectsAsync(db.AccessRequests.Where(item => item.Status == "pending"), scopeIds, db, item => item.SubjectUserId, ct),
                pendingAccessReviews = await CountGovernanceSubjectsAsync(db.AccessReviews.Where(item => item.Status == "pending"), scopeIds, db, item => item.SubjectUserId, ct),
                pendingBreakGlass = await CountGovernanceSubjectsAsync(db.BreakGlassRequests.Where(item => item.Status == "pending"), scopeIds, db, item => item.SubjectUserId, ct),
                auditEventsLast24Hours = await db.AuditLogs.CountAsync(item => item.Timestamp >= dayAgo, ct)
            });
        }).RequireAuthorization(AuthorizationPolicyNames.Permissions.AdminRolesRead)
            .WithTenantReadScope(HisHopePermissions.Admin.RolesRead);

        group.MapGet("/scopes", async (IdentityDbContext db, HttpContext http, CancellationToken ct) =>
        {
            var filter = IamTenantHttpContext.RequireFilter(http);

            var query = db.IamScopes.AsNoTracking();
            if (filter.AllowedScopeIds is not null)
                query = query.Where(item => filter.AllowedScopeIds.Contains(item.Id));

            return Results.Ok(await query.OrderBy(x => x.Kind).ThenBy(x => x.Key).ToListAsync(ct));
        })
            .RequireAuthorization(AuthorizationPolicyNames.Permissions.AdminRolesRead)
            .WithTenantReadScope(HisHopePermissions.Admin.RolesRead);

        group.MapPost("/scopes", async (ScopeRequest request, IdentityDbContext db, HttpContext http, CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(request.Key) || string.IsNullOrWhiteSpace(request.DisplayName) ||
                !AuthorizationConstants.ScopeKinds.All.Contains(request.Kind))
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["scope"] = ["Key, displayName and kind are required; kind must be organization, tenant, account or environment."] });
            var filter = IamTenantHttpContext.RequireFilter(http);
            // A new root scope has no parent to authorize against, so only
            // operators without a tenant restriction may create one.
            if (request.ParentId is null && filter.AllowedScopeIds is not null) return Results.Forbid();
            if (request.ParentId is Guid newParentId && IamTenantAccessGuard.EnsureScopeAccess(newParentId, filter) is { } parentScopeError)
                return parentScopeError;
            if (request.ParentId.HasValue)
            {
                var parent = await db.IamScopes.AsNoTracking().SingleOrDefaultAsync(x => x.Id == request.ParentId.Value && x.IsActive, ct);
                if (parent is null) return Results.NotFound("parent_scope_not_found");
                if (!IsValidParentKind(parent.Kind, request.Kind))
                    return Results.ValidationProblem(new Dictionary<string, string[]> { ["parentId"] = ["Scope hierarchy must be organization -> tenant -> account -> environment."] });
            }
            var normalizedKey = request.Key.Trim().ToLowerInvariant();
            if (await db.IamScopes.AnyAsync(x => x.Key == normalizedKey && x.Kind == request.Kind, ct))
                return Results.Conflict("scope_key_exists");
            var item = new IamScope { Key = normalizedKey, DisplayName = request.DisplayName.Trim(), Kind = request.Kind, ParentId = request.ParentId };
            db.IamScopes.Add(item); await db.SaveChangesAsync(ct);
            await AdminAudit.LogAuthorizationChangeAsync(db, http, "IAM_SCOPE_CREATE", "IamScope", item.Id.ToString(), "IAM scope created.", null, JsonSerializer.Serialize(new { item.Key, item.DisplayName, item.Kind, item.ParentId }), ct);
            return Results.Created(IdentityApiRoutes.IamScope(item.Id), item);
        }).RequireAuthorization(AuthorizationPolicyNames.Permissions.AdminRolesWrite)
            .WithTenantMutationScope();

        group.MapPut("/scopes/{id:guid}", async (Guid id, ScopeRequest request, IdentityDbContext db, HttpContext http, CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(request.Key) || string.IsNullOrWhiteSpace(request.DisplayName) || !AuthorizationConstants.ScopeKinds.All.Contains(request.Kind))
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["scope"] = ["Key, displayName and kind are required; kind must be organization, tenant, account or environment."] });
            var filter = IamTenantHttpContext.RequireFilter(http);
            if (IamTenantAccessGuard.EnsureScopeAccess(id, filter) is { } scopeError) return scopeError;
            if (request.ParentId is Guid targetParentId && IamTenantAccessGuard.EnsureScopeAccess(targetParentId, filter) is { } parentScopeError)
                return parentScopeError;
            var item = await db.IamScopes.FirstOrDefaultAsync(x => x.Id == id, ct);
            if (item is null) return Results.NotFound("scope_not_found");
            if (request.ParentId == id) return Results.ValidationProblem(new Dictionary<string, string[]> { ["parentId"] = ["A scope cannot be its own parent."] });
            if (request.ParentId is Guid cycleParentId && await IsScopeWithinAsync(db, cycleParentId, id, ct))
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["parentId"] = ["A scope cannot be moved below one of its descendants."] });
            var before = JsonSerializer.Serialize(new { item.Key, item.DisplayName, item.Kind, item.ParentId });
            if (request.ParentId.HasValue)
            {
                var parent = await db.IamScopes.AsNoTracking().SingleOrDefaultAsync(x => x.Id == request.ParentId.Value && x.IsActive, ct);
                if (parent is null) return Results.NotFound("parent_scope_not_found");
                if (!IsValidParentKind(parent.Kind, request.Kind)) return Results.ValidationProblem(new Dictionary<string, string[]> { ["parentId"] = ["Scope hierarchy must be organization -> tenant -> account -> environment."] });
            }
            var children = await db.IamScopes.AsNoTracking().Where(x => x.ParentId == id && x.IsActive).Select(x => x.Kind).ToListAsync(ct);
            if (children.Any(childKind => !IsValidParentKind(request.Kind, childKind)))
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["kind"] = ["The scope kind cannot change while active children would violate the hierarchy."] });
            var normalizedKey = request.Key.Trim().ToLowerInvariant();
            if (await db.IamScopes.AnyAsync(x => x.Id != id && x.Key == normalizedKey && x.Kind == request.Kind, ct)) return Results.Conflict("scope_key_exists");
            item.Key = normalizedKey; item.DisplayName = request.DisplayName.Trim(); item.Kind = request.Kind; item.ParentId = request.ParentId;
            await db.SaveChangesAsync(ct);
            await AdminAudit.LogAuthorizationChangeAsync(db, http, "IAM_SCOPE_UPDATE", "IamScope", item.Id.ToString(), "IAM scope updated.", before, JsonSerializer.Serialize(new { item.Key, item.DisplayName, item.Kind, item.ParentId }), ct);
            return Results.Ok(item);
        }).RequireAuthorization(AuthorizationPolicyNames.Permissions.AdminRolesWrite)
            .WithTenantMutationScope();

        group.MapPost("/scopes/{id:guid}/deactivate", async (Guid id, IdentityDbContext db, HttpContext http, CancellationToken ct) =>
        {
            var filter = IamTenantHttpContext.RequireFilter(http);
            if (IamTenantAccessGuard.EnsureScopeAccess(id, filter) is { } scopeError) return scopeError;
            var item = await db.IamScopes.FirstOrDefaultAsync(x => x.Id == id, ct);
            if (item is null) return Results.NotFound("scope_not_found");
            if (await db.IamScopes.AnyAsync(x => x.ParentId == id && x.IsActive, ct)) return Results.Conflict("scope_has_active_children");
            item.IsActive = false;
            await db.SaveChangesAsync(ct);
            await AdminAudit.LogAuthorizationChangeAsync(db, http, "IAM_SCOPE_DEACTIVATE", "IamScope", id.ToString(), "IAM scope deactivated.", "active", "inactive", ct);
            return Results.Ok(item);
        }).RequireAuthorization(AuthorizationPolicyNames.Permissions.AdminRolesWrite)
            .WithTenantMutationScope();

        group.MapPost("/scopes/{id:guid}/activate", async (Guid id, IdentityDbContext db, HttpContext http, CancellationToken ct) =>
        {
            var filter = IamTenantHttpContext.RequireFilter(http);
            if (IamTenantAccessGuard.EnsureScopeAccess(id, filter) is { } scopeError) return scopeError;
            var item = await db.IamScopes.FirstOrDefaultAsync(x => x.Id == id, ct);
            if (item is null) return Results.NotFound("scope_not_found");
            if (item.ParentId is Guid parentId && !await db.IamScopes.AnyAsync(x => x.Id == parentId && x.IsActive, ct))
                return Results.Conflict("parent_scope_inactive");
            item.IsActive = true;
            await db.SaveChangesAsync(ct);
            await AdminAudit.LogAuthorizationChangeAsync(db, http, "IAM_SCOPE_ACTIVATE", "IamScope", id.ToString(), "IAM scope activated.", "inactive", "active", ct);
            return Results.Ok(item);
        }).RequireAuthorization(AuthorizationPolicyNames.Permissions.AdminRolesWrite)
            .WithTenantMutationScope();

        group.MapGet("/services", async (IdentityDbContext db, HttpContext http, CancellationToken ct) =>
        {
            // Service definitions are platform-level metadata with no scope
            // column, so the tenant guard only validates operator access.
            _ = IamTenantHttpContext.RequireFilter(http);

            return Results.Ok(await db.IamServiceDefinitions.AsNoTracking().OrderBy(x => x.Key).ToListAsync(ct));
        }).RequireAuthorization(AuthorizationPolicyNames.Permissions.AdminRolesRead)
            .WithTenantReadScope(HisHopePermissions.Admin.RolesRead);

        // API audiences are a first-class read model over workload roles. Keeping
        // this projection separate from role management gives admin clients a
        // stable Applications contract without duplicating audience state.
        group.MapGet("/api-audiences", async (IdentityDbContext db, HttpContext http, CancellationToken ct) =>
        {
            var filter = IamTenantHttpContext.RequireFilter(http);

            var audienceQuery = db.IamWorkloadRoles.AsNoTracking();
            if (filter.AllowedScopeIds is not null)
                audienceQuery = audienceQuery.Where(x => filter.AllowedScopeIds.Contains(x.ScopeId));

            var audiences = await audienceQuery
                .OrderBy(x => x.Audience)
                .Select(x => new
                {
                    id = x.Id,
                    key = x.Key,
                    displayName = x.DisplayName,
                    audience = x.Audience,
                    scopeId = x.ScopeId,
                    isActive = x.IsActive,
                    maxSessionSeconds = x.MaxSessionSeconds,
                    // Workload roles currently persist an active flag rather
                    // than a separate lifecycle column. Keep the public
                    // audience contract stable without querying a nonexistent
                    // database column.
                    lifecycleStatus = x.IsActive ? "active" : "inactive"
                })
                .ToListAsync(ct);
            return Results.Ok(new { schemaVersion = "iam-api-audiences.v1", evaluatedAt = DateTime.UtcNow, audiences });
        }).RequireAuthorization(AuthorizationPolicyNames.Permissions.AdminRolesRead)
            .WithTenantReadScope(HisHopePermissions.Admin.RolesRead);

        // Trusted issuers are deliberately configuration-backed: issuer metadata
        // and client secrets remain owned by the Identity runtime, while admin-app
        // receives only safe issuer/protocol/status fields.
        group.MapGet("/trusted-issuers", async (
            IConfiguration configuration,
            HttpContext http,
            CancellationToken ct) =>
        {
            _ = IamTenantHttpContext.RequireFilter(http);

            var issuers = new List<object>();
            AddIssuer(issuers, "google", "Google", configuration["Authentication:Google:Authority"], "oidc", configuration["Authentication:Google:ClientId"]);
            AddIssuer(issuers, "microsoft", "Microsoft", configuration["Authentication:Microsoft:Authority"], "oidc", configuration["Authentication:Microsoft:ClientId"]);
            AddIssuer(issuers, "entra", "Microsoft Entra ID", configuration["Authentication:Entra:Authority"], "oidc", configuration["Authentication:Entra:ClientId"]);
            foreach (var source in configuration.GetSection("Authentication:ExternalSources").GetChildren())
                AddIssuer(issuers, source["Name"], source["DisplayName"] ?? source["Name"], source["Authority"], "oidc", source["ClientId"]);
            return Results.Ok(new { schemaVersion = "iam-trusted-issuers.v1", evaluatedAt = DateTime.UtcNow, issuers });
        }).RequireAuthorization(AuthorizationPolicyNames.Permissions.AdminRolesRead)
            .WithTenantReadScope(HisHopePermissions.Admin.RolesRead);

        group.MapPost("/services", async (ServiceRequest request, IdentityDbContext db, CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(request.Key) || string.IsNullOrWhiteSpace(request.PermissionPrefix))
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["service"] = ["key and permissionPrefix are required."] });
            var key = request.Key.Trim().ToLowerInvariant();
            if (await db.IamServiceDefinitions.AnyAsync(x => x.Key == key, ct)) return Results.Conflict("service_key_exists");
            var item = new IamServiceDefinition { Key = key, DisplayName = request.DisplayName.Trim(), PermissionPrefix = request.PermissionPrefix.Trim(), Owner = request.Owner.Trim() };
            db.IamServiceDefinitions.Add(item); await db.SaveChangesAsync(ct);
            return Results.Created($"{IdentityApiRoutes.AdminIam}/services/{item.Id:D}", item);
        }).RequireAuthorization(AuthorizationPolicyNames.Permissions.AdminRolesWrite);

        group.MapPut("/services/{id:guid}", async (Guid id, ServiceRequest request, IdentityDbContext db, HttpContext http, CancellationToken ct) =>
        {
            var item = await db.IamServiceDefinitions.FirstOrDefaultAsync(x => x.Id == id, ct);
            if (item is null) return Results.NotFound("service_not_found");
            if (string.IsNullOrWhiteSpace(request.Key) || string.IsNullOrWhiteSpace(request.PermissionPrefix))
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["service"] = ["key and permissionPrefix are required."] });
            var key = request.Key.Trim().ToLowerInvariant();
            if (await db.IamServiceDefinitions.AnyAsync(x => x.Id != id && x.Key == key, ct)) return Results.Conflict("service_key_exists");
            var before = JsonSerializer.Serialize(new { item.Key, item.DisplayName, item.PermissionPrefix, item.Owner });
            item.Key = key; item.DisplayName = request.DisplayName.Trim(); item.PermissionPrefix = request.PermissionPrefix.Trim(); item.Owner = request.Owner.Trim();
            await db.SaveChangesAsync(ct);
            await AdminAudit.LogAuthorizationChangeAsync(db, http, "IAM_SERVICE_UPDATE", "IamServiceDefinition", id.ToString(), "IAM service definition updated.", before, JsonSerializer.Serialize(new { item.Key, item.DisplayName, item.PermissionPrefix, item.Owner }), ct);
            return Results.Ok(item);
        }).RequireAuthorization(AuthorizationPolicyNames.Permissions.AdminRolesWrite);

        group.MapPost("/services/{id:guid}/deactivate", async (Guid id, IdentityDbContext db, HttpContext http, CancellationToken ct) =>
        {
            var item = await db.IamServiceDefinitions.FirstOrDefaultAsync(x => x.Id == id, ct);
            if (item is null) return Results.NotFound("service_not_found");
            item.IsActive = false;
            await db.SaveChangesAsync(ct);
            await AdminAudit.LogAuthorizationChangeAsync(db, http, "IAM_SERVICE_DEACTIVATE", "IamServiceDefinition", id.ToString(), "IAM service deactivated.", "active", "inactive", ct);
            return Results.Ok(item);
        }).RequireAuthorization(AuthorizationPolicyNames.Permissions.AdminRolesWrite);

        group.MapPost("/services/{id:guid}/activate", async (Guid id, IdentityDbContext db, HttpContext http, CancellationToken ct) =>
        {
            var item = await db.IamServiceDefinitions.FirstOrDefaultAsync(x => x.Id == id, ct);
            if (item is null) return Results.NotFound("service_not_found");
            item.IsActive = true;
            await db.SaveChangesAsync(ct);
            await AdminAudit.LogAuthorizationChangeAsync(db, http, "IAM_SERVICE_ACTIVATE", "IamServiceDefinition", id.ToString(), "IAM service activated.", "inactive", "active", ct);
            return Results.Ok(item);
        }).RequireAuthorization(AuthorizationPolicyNames.Permissions.AdminRolesWrite);

        group.MapGet("/permission-sets", async (IdentityDbContext db, HttpContext http, CancellationToken ct) =>
        {
            var filter = IamTenantHttpContext.RequireFilter(http);

            var query = db.IamPermissionSets.AsNoTracking();
            // Permission-set definitions are environment-scoped for writes and
            // assignments, but the catalog itself must be discoverable by HQ
            // super-admins so they can administer customer environments. The
            // mutation and assignment endpoints below still enforce scope access.
            if (filter.AllowedScopeIds is not null && !filter.IsGroupHqOperator)
                query = query.Where(x => filter.AllowedScopeIds.Contains(x.ScopeId));
            return Results.Ok(await query.OrderBy(x => x.Key).ToListAsync(ct));
        }).RequireAuthorization(AuthorizationPolicyNames.Permissions.AdminRolesRead)
            .WithTenantReadScope(HisHopePermissions.Admin.RolesRead);

        group.MapGet("/assignments", async (Guid? principalId, IdentityDbContext db, HttpContext http, CancellationToken ct) =>
        {
            var filter = IamTenantHttpContext.RequireFilter(http);

            var query = db.IamPermissionSetAssignments.AsNoTracking();
            // HQ administrators need a cross-tenant view to audit and manage
            // assignments. Creating, changing, and revoking assignments still
            // goes through the mutation scope guard below.
            if (filter.AllowedScopeIds is not null && !filter.IsGroupHqOperator)
                query = query.Where(x => filter.AllowedScopeIds.Contains(x.ScopeId));
            if (principalId.HasValue) query = query.Where(x => x.PrincipalId == principalId.Value);
            return Results.Ok(await query.OrderByDescending(x => x.CreatedAt).Take(500).ToListAsync(ct));
        }).RequireAuthorization(AuthorizationPolicyNames.Permissions.AdminRolesRead)
            .WithTenantReadScope(HisHopePermissions.Admin.RolesRead);

        group.MapPost("/permission-sets", async (PermissionSetRequest request, IdentityDbContext db, HttpContext http, CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(request.Key) || request.ScopeId == Guid.Empty || request.Permissions is null)
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["permissionSet"] = ["Key, scopeId and permissions are required."] });
            var filter = IamTenantHttpContext.RequireFilter(http);
            if (IamTenantAccessGuard.EnsureScopeAccess(request.ScopeId, filter) is { } scopeError) return scopeError;
            if (!await db.IamScopes.AnyAsync(x => x.Id == request.ScopeId && x.IsActive, ct)) return Results.NotFound("scope_not_found");
            var invalid = request.Permissions.Where(x => string.IsNullOrWhiteSpace(x) || x.Length > 200 || !HisHopePermissions.All.Contains(x.Trim().ToLowerInvariant())).ToArray();
            if (invalid.Length > 0) return Results.ValidationProblem(new Dictionary<string, string[]> { ["permissions"] = ["Permission codes must be non-empty, <= 200 characters and registered in the canonical server catalog."] });
            var permissions = request.Permissions.Select(x => x.Trim().ToLowerInvariant()).Distinct(StringComparer.Ordinal).Order().ToArray();
            var item = new IamPermissionSet { Key = request.Key.Trim(), DisplayName = request.DisplayName.Trim(), ScopeId = request.ScopeId, PermissionsJson = JsonSerializer.Serialize(permissions), CreatedBy = http.User.FindFirstValue("sub") };
            db.IamPermissionSets.Add(item); await db.SaveChangesAsync(ct);
            return Results.Created(IdentityApiRoutes.IamPermissionSet(item.Id), item);
        }).RequireAuthorization(AuthorizationPolicyNames.Permissions.AdminRolesWrite)
            .WithTenantMutationScope();

        group.MapPut("/permission-sets/{id:guid}", async (Guid id, PermissionSetRequest request, IdentityDbContext db, HttpContext http, CancellationToken ct) =>
        {
            var filter = IamTenantHttpContext.RequireFilter(http);
            var item = await db.IamPermissionSets.FirstOrDefaultAsync(x => x.Id == id, ct);
            if (item is null) return Results.NotFound("permission_set_not_found");
            if (IamTenantAccessGuard.EnsureScopeAccess(item.ScopeId, filter) is { } currentScopeError) return currentScopeError;
            if (string.IsNullOrWhiteSpace(request.Key) || request.ScopeId == Guid.Empty || request.Permissions is null)
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["permissionSet"] = ["Key, scopeId and permissions are required."] });
            if (IamTenantAccessGuard.EnsureScopeAccess(request.ScopeId, filter) is { } targetScopeError) return targetScopeError;
            if (!await db.IamScopes.AnyAsync(x => x.Id == request.ScopeId && x.IsActive, ct)) return Results.NotFound("scope_not_found");
            var invalid = request.Permissions.Where(x => string.IsNullOrWhiteSpace(x) || x.Length > 200 || !HisHopePermissions.All.Contains(x.Trim().ToLowerInvariant())).ToArray();
            if (invalid.Length > 0) return Results.ValidationProblem(new Dictionary<string, string[]> { ["permissions"] = ["Permission codes must be registered in the canonical server catalog."] });
            var before = item.PermissionsJson;
            item.Key = request.Key.Trim();
            item.DisplayName = request.DisplayName.Trim();
            item.ScopeId = request.ScopeId;
            item.PermissionsJson = JsonSerializer.Serialize(request.Permissions.Select(x => x.Trim().ToLowerInvariant()).Distinct(StringComparer.Ordinal).Order());
            item.Version++;
            item.LifecycleStatus = AuthorizationConstants.LifecycleStatuses.Draft;
            item.PublishedAt = null;
            await db.SaveChangesAsync(ct);
            await AdminAudit.LogAuthorizationChangeAsync(db, http, "IAM_PERMISSION_SET_UPDATE", "IamPermissionSet", id.ToString(), "Permission set updated and returned to draft.", before, item.PermissionsJson, ct);
            return Results.Ok(item);
        }).RequireAuthorization(AuthorizationPolicyNames.Permissions.AdminRolesWrite)
            .WithTenantMutationScope();

        group.MapPost("/permission-sets/{id:guid}/assignments", async (Guid id, AssignmentRequest request, IdentityDbContext db, HttpContext http, CancellationToken ct) =>
        {
            var filter = IamTenantHttpContext.RequireFilter(http);
            var set = await db.IamPermissionSets.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct);
            if (set is null) return Results.NotFound("permission_set_not_found");
            if (IamTenantAccessGuard.EnsureScopeAccess(set.ScopeId, filter) is { } setScopeError) return setScopeError;
            if (set.LifecycleStatus != AuthorizationConstants.LifecycleStatuses.Published)
                return Results.Conflict("permission_set_must_be_published");
            if (request.PrincipalId == Guid.Empty || request.ScopeId == Guid.Empty || !AuthorizationConstants.PrincipalTypes.All.Contains(request.PrincipalType))
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["assignment"] = ["principalId, scopeId and principalType (human|group|workload) are required."] });
            if (IamTenantAccessGuard.EnsureScopeAccess(request.ScopeId, filter) is { } scopeError) return scopeError;
            if (request.PrincipalType == AuthorizationConstants.PrincipalTypes.Human &&
                await IamTenantAccessGuard.EnsureUserAccessAsync(db, request.PrincipalId, filter, ct) is { } principalError)
                return principalError;
            if (!await db.IamScopes.AnyAsync(x => x.Id == request.ScopeId && x.IsActive, ct)) return Results.NotFound("scope_not_found");
            if (!await IsScopeWithinAsync(db, request.ScopeId, set.ScopeId, ct))
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["scopeId"] = ["Assignment scope must be the permission-set scope or one of its descendants."] });
            var principalExists = request.PrincipalType switch
            {
                AuthorizationConstants.PrincipalTypes.Human => await db.Users.AnyAsync(x => x.Id == request.PrincipalId && x.IsActive, ct),
                AuthorizationConstants.PrincipalTypes.Group => await db.IamGroups.AnyAsync(x => x.Id == request.PrincipalId && x.IsActive, ct),
                AuthorizationConstants.PrincipalTypes.Workload => await db.IamWorkloadRoles.AnyAsync(x => x.Id == request.PrincipalId && x.IsActive, ct),
                _ => false
            };
            if (!principalExists) return Results.NotFound("principal_not_found");
            var item = new IamPermissionSetAssignment { PermissionSetId = id, PrincipalId = request.PrincipalId, PrincipalType = request.PrincipalType, ScopeId = request.ScopeId, ExpiresAt = request.ExpiresAt, CreatedBy = http.User.FindFirstValue("sub") };
            db.IamPermissionSetAssignments.Add(item); await db.SaveChangesAsync(ct);
            await AdminAudit.LogAuthorizationChangeAsync(db, http, "IAM_ASSIGNMENT_CREATE", "IamPermissionSetAssignment", item.Id.ToString(), "Permission set assignment created.", null, JsonSerializer.Serialize(new { item.PermissionSetId, item.PrincipalId, item.PrincipalType, item.ScopeId, item.ExpiresAt }), ct);
            return Results.Created(IdentityApiRoutes.IamAssignment(item.Id), item);
        }).RequireAuthorization(AuthorizationPolicyNames.Permissions.AdminRolesWrite)
            .WithTenantMutationScope();

        group.MapPost("/permission-sets/{id:guid}/publish", async (Guid id, IdentityDbContext db, HttpContext http, CancellationToken ct) =>
        {
            var filter = IamTenantHttpContext.RequireFilter(http);
            var item = await db.IamPermissionSets.FirstOrDefaultAsync(x => x.Id == id, ct);
            if (item is null) return Results.NotFound("permission_set_not_found");
            if (IamTenantAccessGuard.EnsureScopeAccess(item.ScopeId, filter) is { } scopeError) return scopeError;
            item.Version++;
            item.LifecycleStatus = AuthorizationConstants.LifecycleStatuses.Published;
            item.PublishedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
            await AdminAudit.LogAuthorizationChangeAsync(db, http, "IAM_PERMISSION_SET_PUBLISH", "IamPermissionSet", id.ToString(), "Permission set published.", null, item.PermissionsJson, ct);
            return Results.Ok(item);
        }).RequireAuthorization(AuthorizationPolicyNames.Permissions.AdminRolesWrite)
            .WithTenantMutationScope();

        group.MapPost("/assignments/{id:guid}/revoke", async (Guid id, IdentityDbContext db, HttpContext http, CancellationToken ct) =>
        {
            var filter = IamTenantHttpContext.RequireFilter(http);
            var item = await db.IamPermissionSetAssignments.FirstOrDefaultAsync(x => x.Id == id, ct);
            if (item is null) return Results.NotFound("assignment_not_found");
            if (IamTenantAccessGuard.EnsureScopeAccess(item.ScopeId, filter) is { } scopeError) return scopeError;
            item.Status = "revoked";
            await db.SaveChangesAsync(ct);
            await AdminAudit.LogAuthorizationChangeAsync(db, http, "IAM_ASSIGNMENT_REVOKE", "IamPermissionSetAssignment", id.ToString(), "Permission set assignment revoked.", "active", "revoked", ct);
            return Results.Ok(item);
        }).RequireAuthorization(AuthorizationPolicyNames.Permissions.AdminRolesWrite)
            .WithTenantMutationScope();

        group.MapGet("/principals/{principalId:guid}/effective-access", async (Guid principalId, IdentityDbContext db, HttpContext http, CancellationToken ct) =>
        {
            var filter = IamTenantHttpContext.RequireFilter(http);
            var evaluationScopeId = IamTenantHttpContext.ParseScopeId(http.Request);

            var humanExists = await db.Users.AsNoTracking().AnyAsync(x => x.Id == principalId && x.IsActive, ct);
            var workloadRole = await db.IamWorkloadRoles.AsNoTracking()
                .SingleOrDefaultAsync(x => x.Id == principalId && x.IsActive, ct);
            if (humanExists)
            {
                if (await IamTenantAccessGuard.EnsureUserAccessAsync(db, principalId, filter, ct) is { } principalError)
                    return principalError;
            }
            else if (workloadRole is not null && IamTenantAccessGuard.EnsureScopeAccess(workloadRole.ScopeId, filter) is { } workloadError)
                return workloadError;
            var groupIds = await db.IamGroupMemberships.AsNoTracking()
                .Where(x => x.UserId == principalId)
                .Join(db.IamGroups.Where(x => x.IsActive), membership => membership.GroupId, group => group.Id, (_, group) => group.Id)
                .ToArrayAsync(ct);
            var query = db.IamPermissionSetAssignments.AsNoTracking().Where(x => x.Status == "active" && (x.ExpiresAt == null || x.ExpiresAt > DateTime.UtcNow) && ((x.PrincipalId == principalId && x.PrincipalType == AuthorizationConstants.PrincipalTypes.Human) || (x.PrincipalType == AuthorizationConstants.PrincipalTypes.Group && groupIds.Contains(x.PrincipalId))));
            if (workloadRole is not null)
                query = query.Where(x => (x.PrincipalId == principalId && x.PrincipalType == AuthorizationConstants.PrincipalTypes.Workload) || (x.PrincipalId == principalId && x.PrincipalType == AuthorizationConstants.PrincipalTypes.Human) || (x.PrincipalType == AuthorizationConstants.PrincipalTypes.Group && groupIds.Contains(x.PrincipalId)));
            Guid[] evaluatedScopes = [];
            if (evaluationScopeId.HasValue)
            {
                evaluatedScopes = await GetScopeLineageAsync(db, evaluationScopeId.Value, ct);
                if (evaluatedScopes.Length == 0) return Results.NotFound("scope_not_found");
                query = query.Where(x => evaluatedScopes.Contains(x.ScopeId));
            }
            var sets = await query.Join(db.IamPermissionSets.Where(x => x.LifecycleStatus == AuthorizationConstants.LifecycleStatuses.Published), a => a.PermissionSetId, s => s.Id, (a, s) => s.PermissionsJson).ToListAsync(ct);
            var permissions = sets.SelectMany(x => JsonSerializer.Deserialize<string[]>(x) ?? []).ToList();
            if (workloadRole is not null)
                permissions.AddRange(JsonSerializer.Deserialize<string[]>(workloadRole.PermissionsJson) ?? []);
            return Results.Ok(new
            {
                principalId,
                principalType = workloadRole is not null && !humanExists ? AuthorizationConstants.PrincipalTypes.Workload : AuthorizationConstants.PrincipalTypes.Human,
                scopeId = evaluationScopeId,
                evaluatedScopeIds = evaluatedScopes,
                groupIds,
                workloadRoleId = workloadRole?.Id,
                permissions = permissions.Distinct(StringComparer.Ordinal).Order().ToArray(),
                source = workloadRole is not null ? "iam_permission_set_assignments+iam_workload_role" : "iam_permission_set_assignments",
                evaluatedAt = DateTime.UtcNow
            });
        }).RequireAuthorization(AuthorizationPolicyNames.Permissions.AdminRolesRead)
            .WithTenantReadScope(HisHopePermissions.Admin.RolesRead);

        group.MapGet("/workload-roles", async (IdentityDbContext db, HttpContext http, CancellationToken ct) =>
        {
            var filter = IamTenantHttpContext.RequireFilter(http);

            var query = db.IamWorkloadRoles.AsNoTracking();
            if (filter.AllowedScopeIds is not null)
                query = query.Where(x => filter.AllowedScopeIds.Contains(x.ScopeId));

            return Results.Ok(await query.OrderBy(x => x.Key).ToListAsync(ct));
        }).RequireAuthorization(AuthorizationPolicyNames.Permissions.AdminRolesRead)
            .WithTenantReadScope(HisHopePermissions.Admin.RolesRead);

        group.MapPost("/workload-roles/{id:guid}/revoke-sessions", async (
            Guid id, IdentityDbContext db, ITokenBlacklistService blacklist, HttpContext http, CancellationToken ct) =>
        {
            var filter = IamTenantHttpContext.RequireFilter(http);
            var role = await db.IamWorkloadRoles.AsNoTracking()
                .SingleOrDefaultAsync(item => item.Id == id, ct);
            if (role is null) return Results.NotFound("workload_role_not_found");
            if (IamTenantAccessGuard.EnsureScopeAccess(role.ScopeId, filter) is { } scopeError) return scopeError;

            // Workload access tokens use the OAuth client id as `sub`, so the
            // shared revocation timestamp immediately invalidates all tokens
            // issued to this service principal across resource services.
            await blacklist.RevokeAllUserTokensAsync(role.Audience, ct);
            await AdminAudit.LogAuthorizationChangeAsync(
                db,
                http,
                "IAM_WORKLOAD_SESSION_REVOKE_ALL",
                "IamWorkloadRole",
                role.Id.ToString("D"),
                "All workload sessions and access tokens were revoked.",
                "active",
                "revoked",
                ct);
            return Results.Ok(new { revoked = true, role.Id, role.Audience, revokedAt = DateTime.UtcNow, route = IdentityApiRoutes.IamWorkloadRoleRevokeSessions(role.Id) });
        }).RequireAuthorization(AuthorizationPolicyNames.Permissions.AdminRolesWrite)
            .WithTenantMutationScope();

        group.MapPost("/workload-roles/{id:guid}/rotate-credential", async (
            Guid id,
            IdentityDbContext db,
            IOpenIddictApplicationManager appManager,
            VaultClientSecretStore vaultStore,
            HttpContext http,
            CancellationToken ct) =>
        {
            var filter = IamTenantHttpContext.RequireFilter(http);
            var role = await db.IamWorkloadRoles.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
            if (role is null || !role.IsActive || string.IsNullOrWhiteSpace(role.Audience))
                return Results.NotFound();
            if (IamTenantAccessGuard.EnsureScopeAccess(role.ScopeId, filter) is { } scopeError) return scopeError;

            var application = await appManager.FindByClientIdAsync(role.Audience, ct);
            if (application is null || await appManager.GetClientTypeAsync(application, ct) != OpenIddictConstants.ClientTypes.Confidential)
                return Results.NotFound();

            var secret = vaultStore.GenerateSecret(role.Audience);
            await vaultStore.StoreSecretAsync(role.Audience, secret, ct);
            await appManager.UpdateAsync(application, secret, ct);
            await AdminAudit.LogAuthorizationChangeAsync(
                db,
                http,
                "IAM_WORKLOAD_CREDENTIAL_ROTATE",
                "IamWorkloadRole",
                role.Id.ToString("D"),
                "Workload credential rotated; secret material is returned once.",
                null,
                JsonSerializer.Serialize(new { role.Key, role.Audience }),
                ct);

            return Results.Ok(new
            {
                schemaVersion = "iam-workload-credential-rotate.v1",
                workloadRoleId = role.Id,
                clientId = role.Audience,
                clientAuthMethod = "client_secret_basic",
                secret,
                warning = "Store this secret securely. It will not be shown again."
            });
        }).RequireAuthorization(AuthorizationPolicyNames.Permissions.AdminRolesWrite)
            .WithTenantMutationScope();

        group.MapGet("/workload-roles/{id:guid}/sessions", async (
            Guid id, IdentityDbContext db, IWorkloadSessionStore sessions, HttpContext http, CancellationToken ct) =>
        {
            var filter = IamTenantHttpContext.RequireFilter(http);
            var role = await db.IamWorkloadRoles.AsNoTracking()
                .SingleOrDefaultAsync(item => item.Id == id, ct);
            if (role is null) return Results.NotFound("workload_role_not_found");
            if (IamTenantAccessGuard.EnsureScopeAccess(role.ScopeId, filter) is { } scopeError) return scopeError;
            return Results.Ok(new { role.Id, role.Audience, sessions = await sessions.ListAsync(role.Audience, ct), route = IdentityApiRoutes.IamWorkloadRoleSessions(role.Id) });
        }).RequireAuthorization(AuthorizationPolicyNames.Permissions.AdminRolesRead)
            .WithTenantReadScope(HisHopePermissions.Admin.RolesRead);

        group.MapDelete("/workload-roles/{id:guid}/sessions/{sessionId}", async (
            Guid id, string sessionId, IdentityDbContext db, IWorkloadSessionStore sessions, HttpContext http, CancellationToken ct) =>
        {
            var filter = IamTenantHttpContext.RequireFilter(http);
            var role = await db.IamWorkloadRoles.AsNoTracking()
                .SingleOrDefaultAsync(item => item.Id == id, ct);
            if (role is null) return Results.NotFound("workload_role_not_found");
            if (IamTenantAccessGuard.EnsureScopeAccess(role.ScopeId, filter) is { } scopeError) return scopeError;
            var revoked = await sessions.RevokeAsync(role.Audience, sessionId, ct);
            if (revoked)
                await AdminAudit.LogAuthorizationChangeAsync(db, http, "IAM_WORKLOAD_SESSION_REVOKE", "IamWorkloadRole", role.Id.ToString("D"), "A workload session was revoked.", sessionId, "revoked", ct);
            return revoked ? Results.NoContent() : Results.NotFound("workload_session_not_found");
        }).RequireAuthorization(AuthorizationPolicyNames.Permissions.AdminRolesWrite)
            .WithTenantMutationScope();

        group.MapPost("/workload-roles", async (WorkloadRoleRequest request, IdentityDbContext db, HttpContext http, CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(request.Key) || string.IsNullOrWhiteSpace(request.Audience) || request.ScopeId == Guid.Empty)
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["workloadRole"] = ["key, audience and scopeId are required."] });
            if (request.MaxSessionSeconds is < 60 or > 3600)
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["maxSessionSeconds"] = ["Must be between 60 and 3600 seconds."] });
            var filter = IamTenantHttpContext.RequireFilter(http);
            if (IamTenantAccessGuard.EnsureScopeAccess(request.ScopeId, filter) is { } scopeError) return scopeError;
            if (!await db.IamScopes.AnyAsync(x => x.Id == request.ScopeId && x.IsActive, ct)) return Results.NotFound("scope_not_found");
            if (!TryValidateTrust(request.TrustPolicyJson, out var trustError))
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["trustPolicyJson"] = [trustError!] });
            var permissions = request.Permissions?.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x.Trim().ToLowerInvariant()).Distinct(StringComparer.Ordinal).Order().ToArray() ?? [];
            if (permissions.Any(x => !HisHopePermissions.All.Contains(x)))
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["permissions"] = ["Workload role permissions must reference the canonical server permission catalog."] });
            var item = new IamWorkloadRole
            {
                Key = request.Key.Trim(), DisplayName = request.DisplayName.Trim(), ScopeId = request.ScopeId,
                Audience = request.Audience.Trim(), TrustPolicyJson = request.TrustPolicyJson, PermissionsJson = JsonSerializer.Serialize(permissions),
                MaxSessionSeconds = request.MaxSessionSeconds
            };
            db.IamWorkloadRoles.Add(item); await db.SaveChangesAsync(ct);
            return Results.Created(IdentityApiRoutes.IamWorkloadRole(item.Id), item);
        }).RequireAuthorization(AuthorizationPolicyNames.Permissions.AdminRolesWrite)
            .WithTenantMutationScope();

        group.MapPut("/workload-roles/{id:guid}", async (Guid id, WorkloadRoleRequest request, IdentityDbContext db, HttpContext http, CancellationToken ct) =>
        {
            var filter = IamTenantHttpContext.RequireFilter(http);
            var item = await db.IamWorkloadRoles.FirstOrDefaultAsync(x => x.Id == id, ct);
            if (item is null) return Results.NotFound("workload_role_not_found");
            if (IamTenantAccessGuard.EnsureScopeAccess(item.ScopeId, filter) is { } currentScopeError) return currentScopeError;
            if (string.IsNullOrWhiteSpace(request.Key) || string.IsNullOrWhiteSpace(request.Audience) || request.ScopeId == Guid.Empty)
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["workloadRole"] = ["key, audience and scopeId are required."] });
            if (request.MaxSessionSeconds is < 60 or > 3600) return Results.ValidationProblem(new Dictionary<string, string[]> { ["maxSessionSeconds"] = ["Must be between 60 and 3600 seconds."] });
            if (IamTenantAccessGuard.EnsureScopeAccess(request.ScopeId, filter) is { } targetScopeError) return targetScopeError;
            if (!await db.IamScopes.AnyAsync(x => x.Id == request.ScopeId && x.IsActive, ct)) return Results.NotFound("scope_not_found");
            if (!TryValidateTrust(request.TrustPolicyJson, out var trustError)) return Results.ValidationProblem(new Dictionary<string, string[]> { ["trustPolicyJson"] = [trustError!] });
            var permissions = request.Permissions?.Select(x => x.Trim().ToLowerInvariant()).Where(x => x.Length > 0).Distinct(StringComparer.Ordinal).Order().ToArray() ?? [];
            if (permissions.Any(x => !HisHopePermissions.All.Contains(x))) return Results.ValidationProblem(new Dictionary<string, string[]> { ["permissions"] = ["Workload role permissions must reference the canonical server permission catalog."] });
            var key = request.Key.Trim();
            if (await db.IamWorkloadRoles.AnyAsync(x => x.Id != id && x.ScopeId == request.ScopeId && x.Key == key, ct)) return Results.Conflict("workload_role_key_exists");
            var before = item.PermissionsJson;
            item.Key = key; item.DisplayName = request.DisplayName.Trim(); item.ScopeId = request.ScopeId; item.Audience = request.Audience.Trim(); item.TrustPolicyJson = request.TrustPolicyJson; item.PermissionsJson = JsonSerializer.Serialize(permissions); item.MaxSessionSeconds = request.MaxSessionSeconds;
            await db.SaveChangesAsync(ct);
            await AdminAudit.LogAuthorizationChangeAsync(db, http, "IAM_WORKLOAD_ROLE_UPDATE", "IamWorkloadRole", id.ToString(), "Workload role updated.", before, item.PermissionsJson, ct);
            return Results.Ok(item);
        }).RequireAuthorization(AuthorizationPolicyNames.Permissions.AdminRolesWrite)
            .WithTenantMutationScope();

        group.MapPost("/workload-roles/{id:guid}/deactivate", async (Guid id, IdentityDbContext db, ITokenBlacklistService blacklist, HttpContext http, CancellationToken ct) =>
        {
            var filter = IamTenantHttpContext.RequireFilter(http);
            var item = await db.IamWorkloadRoles.FirstOrDefaultAsync(x => x.Id == id, ct);
            if (item is null) return Results.NotFound("workload_role_not_found");
            if (IamTenantAccessGuard.EnsureScopeAccess(item.ScopeId, filter) is { } scopeError) return scopeError;
            item.IsActive = false;
            await db.SaveChangesAsync(ct);
            await blacklist.RevokeAllUserTokensAsync(item.Audience, ct);
            await AdminAudit.LogAuthorizationChangeAsync(db, http, "IAM_WORKLOAD_ROLE_DEACTIVATE", "IamWorkloadRole", id.ToString(), "Workload role deactivated and sessions revoked.", "active", "inactive", ct);
            return Results.Ok(item);
        }).RequireAuthorization(AuthorizationPolicyNames.Permissions.AdminRolesWrite)
            .WithTenantMutationScope();

        group.MapPost("/workload-roles/{id:guid}/activate", async (Guid id, IdentityDbContext db, HttpContext http, CancellationToken ct) =>
        {
            var filter = IamTenantHttpContext.RequireFilter(http);
            var item = await db.IamWorkloadRoles.FirstOrDefaultAsync(x => x.Id == id, ct);
            if (item is null) return Results.NotFound("workload_role_not_found");
            if (IamTenantAccessGuard.EnsureScopeAccess(item.ScopeId, filter) is { } scopeError) return scopeError;
            if (!await db.IamScopes.AnyAsync(x => x.Id == item.ScopeId && x.IsActive, ct)) return Results.Conflict("scope_inactive");
            item.IsActive = true;
            await db.SaveChangesAsync(ct);
            await AdminAudit.LogAuthorizationChangeAsync(db, http, "IAM_WORKLOAD_ROLE_ACTIVATE", "IamWorkloadRole", id.ToString(), "Workload role activated.", "inactive", "active", ct);
            return Results.Ok(item);
        }).RequireAuthorization(AuthorizationPolicyNames.Permissions.AdminRolesWrite)
            .WithTenantMutationScope();

        group.MapGet("/groups", async (IdentityDbContext db, HttpContext http, CancellationToken ct) =>
        {
            var filter = IamTenantHttpContext.RequireFilter(http);

            var query = db.IamGroups.AsNoTracking();
            if (filter.AllowedScopeIds is not null)
                query = query.Where(x => filter.AllowedScopeIds.Contains(x.ScopeId));

            return Results.Ok(await query.OrderBy(x => x.Key).ToListAsync(ct));
        }).RequireAuthorization(AuthorizationPolicyNames.Permissions.AdminRolesRead)
            .WithTenantReadScope(HisHopePermissions.Admin.RolesRead);

        group.MapPost("/groups", async (GroupRequest request, IdentityDbContext db, HttpContext http, CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(request.Key) || request.ScopeId == Guid.Empty) return Results.ValidationProblem(new Dictionary<string, string[]> { ["group"] = ["key and scopeId are required."] });
            var filter = IamTenantHttpContext.RequireFilter(http);
            if (IamTenantAccessGuard.EnsureScopeAccess(request.ScopeId, filter) is { } scopeError) return scopeError;
            if (!await db.IamScopes.AnyAsync(x => x.Id == request.ScopeId && x.IsActive, ct)) return Results.NotFound("scope_not_found");
            var key = request.Key.Trim().ToLowerInvariant(); if (await db.IamGroups.AnyAsync(x => x.ScopeId == request.ScopeId && x.Key == key, ct)) return Results.Conflict("group_key_exists");
            var item = new IamGroup { Key = key, DisplayName = request.DisplayName.Trim(), ScopeId = request.ScopeId, CreatedBy = http.User.FindFirstValue("sub") }; db.IamGroups.Add(item); await db.SaveChangesAsync(ct);
            return Results.Created(IdentityApiRoutes.IamGroup(item.Id), item);
        }).RequireAuthorization(AuthorizationPolicyNames.Permissions.AdminRolesWrite)
            .WithTenantMutationScope();

        group.MapPut("/groups/{id:guid}", async (Guid id, GroupRequest request, IdentityDbContext db, HttpContext http, CancellationToken ct) =>
        {
            var filter = IamTenantHttpContext.RequireFilter(http);
            var item = await db.IamGroups.FirstOrDefaultAsync(x => x.Id == id, ct);
            if (item is null) return Results.NotFound("group_not_found");
            if (IamTenantAccessGuard.EnsureScopeAccess(item.ScopeId, filter) is { } currentScopeError) return currentScopeError;
            if (string.IsNullOrWhiteSpace(request.Key) || request.ScopeId == Guid.Empty) return Results.ValidationProblem(new Dictionary<string, string[]> { ["group"] = ["key and scopeId are required."] });
            if (IamTenantAccessGuard.EnsureScopeAccess(request.ScopeId, filter) is { } targetScopeError) return targetScopeError;
            if (!await db.IamScopes.AnyAsync(x => x.Id == request.ScopeId && x.IsActive, ct)) return Results.NotFound("scope_not_found");
            var key = request.Key.Trim().ToLowerInvariant();
            if (await db.IamGroups.AnyAsync(x => x.Id != id && x.ScopeId == request.ScopeId && x.Key == key, ct)) return Results.Conflict("group_key_exists");
            item.Key = key; item.DisplayName = request.DisplayName.Trim(); item.ScopeId = request.ScopeId;
            await db.SaveChangesAsync(ct);
            await AdminAudit.LogAuthorizationChangeAsync(db, http, "IAM_GROUP_UPDATE", "IamGroup", id.ToString(), "IAM group updated.", null, JsonSerializer.Serialize(new { item.Key, item.DisplayName, item.ScopeId }), ct);
            return Results.Ok(item);
        }).RequireAuthorization(AuthorizationPolicyNames.Permissions.AdminRolesWrite)
            .WithTenantMutationScope();

        group.MapPost("/groups/{id:guid}/deactivate", async (Guid id, IdentityDbContext db, HttpContext http, CancellationToken ct) =>
        {
            var filter = IamTenantHttpContext.RequireFilter(http);
            var item = await db.IamGroups.FirstOrDefaultAsync(x => x.Id == id, ct);
            if (item is null) return Results.NotFound("group_not_found");
            if (IamTenantAccessGuard.EnsureScopeAccess(item.ScopeId, filter) is { } scopeError) return scopeError;
            item.IsActive = false;
            await db.SaveChangesAsync(ct);
            await AdminAudit.LogAuthorizationChangeAsync(db, http, "IAM_GROUP_DEACTIVATE", "IamGroup", id.ToString(), "IAM group deactivated.", "active", "inactive", ct);
            return Results.Ok(item);
        }).RequireAuthorization(AuthorizationPolicyNames.Permissions.AdminRolesWrite)
            .WithTenantMutationScope();

        group.MapPost("/groups/{id:guid}/activate", async (Guid id, IdentityDbContext db, HttpContext http, CancellationToken ct) =>
        {
            var filter = IamTenantHttpContext.RequireFilter(http);
            var item = await db.IamGroups.FirstOrDefaultAsync(x => x.Id == id, ct);
            if (item is null) return Results.NotFound("group_not_found");
            if (IamTenantAccessGuard.EnsureScopeAccess(item.ScopeId, filter) is { } scopeError) return scopeError;
            if (!await db.IamScopes.AnyAsync(x => x.Id == item.ScopeId && x.IsActive, ct)) return Results.Conflict("scope_inactive");
            item.IsActive = true;
            await db.SaveChangesAsync(ct);
            await AdminAudit.LogAuthorizationChangeAsync(db, http, "IAM_GROUP_ACTIVATE", "IamGroup", id.ToString(), "IAM group activated.", "inactive", "active", ct);
            return Results.Ok(item);
        }).RequireAuthorization(AuthorizationPolicyNames.Permissions.AdminRolesWrite)
            .WithTenantMutationScope();

        group.MapPost("/groups/{id:guid}/members/{userId:guid}", async (Guid id, Guid userId, IdentityDbContext db, HttpContext http, CancellationToken ct) =>
        {
            var filter = IamTenantHttpContext.RequireFilter(http);
            var groupScopeId = await db.IamGroups.AsNoTracking().Where(x => x.Id == id && x.IsActive).Select(x => (Guid?)x.ScopeId).SingleOrDefaultAsync(ct);
            if (groupScopeId is null || !await db.Users.AnyAsync(x => x.Id == userId && x.IsActive, ct)) return Results.NotFound("group_or_user_not_found");
            if (IamTenantAccessGuard.EnsureScopeAccess(groupScopeId.Value, filter) is { } scopeError) return scopeError;
            if (await IamTenantAccessGuard.EnsureUserAccessAsync(db, userId, filter, ct) is { } userError) return userError;
            if (await db.IamGroupMemberships.AnyAsync(x => x.GroupId == id && x.UserId == userId, ct)) return Results.Conflict("membership_exists");
            var item = new IamGroupMembership { GroupId = id, UserId = userId, CreatedBy = http.User.FindFirstValue("sub") }; db.IamGroupMemberships.Add(item); await db.SaveChangesAsync(ct); return Results.Created($"{IdentityApiRoutes.IamGroup(id)}/members/{userId:D}", item);
        }).RequireAuthorization(AuthorizationPolicyNames.Permissions.AdminRolesWrite)
            .WithTenantMutationScope();

        group.MapDelete("/groups/{id:guid}/members/{userId:guid}", async (Guid id, Guid userId, IdentityDbContext db, HttpContext http, CancellationToken ct) =>
        {
            var filter = IamTenantHttpContext.RequireFilter(http);
            var groupScopeId = await db.IamGroups.AsNoTracking().Where(x => x.Id == id).Select(x => (Guid?)x.ScopeId).SingleOrDefaultAsync(ct);
            if (groupScopeId is not null && IamTenantAccessGuard.EnsureScopeAccess(groupScopeId.Value, filter) is { } scopeError) return scopeError;
            var item = await db.IamGroupMemberships.FirstOrDefaultAsync(x => x.GroupId == id && x.UserId == userId, ct); if (item is null) return Results.NotFound("membership_not_found");
            db.IamGroupMemberships.Remove(item); await db.SaveChangesAsync(ct); return Results.NoContent();
        }).RequireAuthorization(AuthorizationPolicyNames.Permissions.AdminRolesWrite)
            .WithTenantMutationScope();

        group.MapGet("/boundaries", async (Guid? principalId, IdentityDbContext db, HttpContext http, CancellationToken ct) =>
        {
            var filter = IamTenantHttpContext.RequireFilter(http);

            var query = db.IamPermissionBoundaries.AsNoTracking();
            if (filter.AllowedScopeIds is not null)
                query = query.Where(x => filter.AllowedScopeIds.Contains(x.ScopeId));
            if (principalId.HasValue) query = query.Where(x => x.PrincipalId == principalId.Value);
            return Results.Ok(await query.OrderBy(x => x.PrincipalId).ToListAsync(ct));
        }).RequireAuthorization(AuthorizationPolicyNames.Permissions.AdminRolesRead)
            .WithTenantReadScope(HisHopePermissions.Admin.RolesRead);

        group.MapPost("/boundaries", async (BoundaryRequest request, IdentityDbContext db, HttpContext http, CancellationToken ct) =>
        {
            if (request.PrincipalId == Guid.Empty || request.ScopeId == Guid.Empty || !AuthorizationConstants.PrincipalTypes.All.Contains(request.PrincipalType))
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["boundary"] = ["principalId, scopeId and principalType (human|workload) are required."] });
            var filter = IamTenantHttpContext.RequireFilter(http);
            if (IamTenantAccessGuard.EnsureScopeAccess(request.ScopeId, filter) is { } scopeError) return scopeError;
            if (request.PrincipalType == AuthorizationConstants.PrincipalTypes.Human &&
                await IamTenantAccessGuard.EnsureUserAccessAsync(db, request.PrincipalId, filter, ct) is { } principalError)
                return principalError;
            if (!await db.IamScopes.AnyAsync(x => x.Id == request.ScopeId && x.IsActive, ct)) return Results.NotFound("scope_not_found");
            var principalExists = request.PrincipalType switch
            {
                AuthorizationConstants.PrincipalTypes.Human => await db.Users.AnyAsync(x => x.Id == request.PrincipalId && x.IsActive, ct),
                AuthorizationConstants.PrincipalTypes.Workload => await db.IamWorkloadRoles.AnyAsync(x => x.Id == request.PrincipalId && x.IsActive, ct),
                _ => false
            };
            if (!principalExists) return Results.NotFound("principal_not_found");
            var permissions = request.AllowedPermissions?.Select(x => x.Trim().ToLowerInvariant()).Where(x => x.Length > 0).Distinct(StringComparer.Ordinal).Order().ToArray() ?? [];
            if (permissions.Any(x => !HisHopePermissions.All.Contains(x)))
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["allowedPermissions"] = ["Boundary permissions must reference the canonical server permission catalog."] });
            if (await db.IamPermissionBoundaries.AnyAsync(x => x.PrincipalId == request.PrincipalId && x.PrincipalType == request.PrincipalType && x.ScopeId == request.ScopeId, ct))
                return Results.Conflict("boundary_exists");
            if (!TryParseObject(request.ResourceConstraintsJson, out _)) return Results.ValidationProblem(new Dictionary<string, string[]> { ["resourceConstraintsJson"] = ["Resource constraints must be a JSON object."] });
            var item = new IamPermissionBoundary { PrincipalId = request.PrincipalId, PrincipalType = request.PrincipalType, ScopeId = request.ScopeId, AllowedPermissionsJson = JsonSerializer.Serialize(permissions), ResourceConstraintsJson = request.ResourceConstraintsJson, CreatedBy = http.User.FindFirstValue("sub") };
            db.IamPermissionBoundaries.Add(item); await db.SaveChangesAsync(ct);
            return Results.Created(IdentityApiRoutes.IamBoundary(item.Id), item);
        }).RequireAuthorization(AuthorizationPolicyNames.Permissions.AdminRolesWrite)
            .WithTenantMutationScope();

        group.MapPut("/boundaries/{id:guid}", async (Guid id, BoundaryRequest request, IdentityDbContext db, HttpContext http, CancellationToken ct) =>
        {
            var filter = IamTenantHttpContext.RequireFilter(http);
            var item = await db.IamPermissionBoundaries.FirstOrDefaultAsync(x => x.Id == id, ct);
            if (item is null) return Results.NotFound("boundary_not_found");
            if (IamTenantAccessGuard.EnsureScopeAccess(item.ScopeId, filter) is { } scopeError) return scopeError;
            var permissions = request.AllowedPermissions?.Select(x => x.Trim().ToLowerInvariant()).Where(x => x.Length > 0).Distinct(StringComparer.Ordinal).Order().ToArray() ?? [];
            if (permissions.Any(x => !HisHopePermissions.All.Contains(x)) || !TryParseObject(request.ResourceConstraintsJson, out _)) return Results.ValidationProblem(new Dictionary<string, string[]> { ["boundary"] = ["Boundary permissions and resource constraints are invalid."] });
            item.AllowedPermissionsJson = JsonSerializer.Serialize(permissions); item.ResourceConstraintsJson = request.ResourceConstraintsJson; item.IsActive = request.IsActive;
            await db.SaveChangesAsync(ct); return Results.Ok(item);
        }).RequireAuthorization(AuthorizationPolicyNames.Permissions.AdminRolesWrite)
            .WithTenantMutationScope();

        group.MapPost("/boundaries/{id:guid}/deactivate", async (Guid id, IdentityDbContext db, HttpContext http, CancellationToken ct) =>
        {
            var filter = IamTenantHttpContext.RequireFilter(http);
            var item = await db.IamPermissionBoundaries.FirstOrDefaultAsync(x => x.Id == id, ct);
            if (item is null) return Results.NotFound("boundary_not_found");
            if (IamTenantAccessGuard.EnsureScopeAccess(item.ScopeId, filter) is { } scopeError) return scopeError;
            item.IsActive = false;
            await db.SaveChangesAsync(ct);
            await AdminAudit.LogAuthorizationChangeAsync(db, http, "IAM_BOUNDARY_DEACTIVATE", "IamPermissionBoundary", id.ToString(), "Permission boundary deactivated.", "active", "inactive", ct);
            return Results.Ok(item);
        }).RequireAuthorization(AuthorizationPolicyNames.Permissions.AdminRolesWrite)
            .WithTenantMutationScope();

        group.MapPost("/boundaries/{id:guid}/activate", async (Guid id, IdentityDbContext db, HttpContext http, CancellationToken ct) =>
        {
            var filter = IamTenantHttpContext.RequireFilter(http);
            var item = await db.IamPermissionBoundaries.FirstOrDefaultAsync(x => x.Id == id, ct);
            if (item is null) return Results.NotFound("boundary_not_found");
            if (IamTenantAccessGuard.EnsureScopeAccess(item.ScopeId, filter) is { } scopeError) return scopeError;
            if (!await db.IamScopes.AnyAsync(x => x.Id == item.ScopeId && x.IsActive, ct)) return Results.Conflict("scope_inactive");
            item.IsActive = true;
            await db.SaveChangesAsync(ct);
            await AdminAudit.LogAuthorizationChangeAsync(db, http, "IAM_BOUNDARY_ACTIVATE", "IamPermissionBoundary", id.ToString(), "Permission boundary activated.", "inactive", "active", ct);
            return Results.Ok(item);
        }).RequireAuthorization(AuthorizationPolicyNames.Permissions.AdminRolesWrite)
            .WithTenantMutationScope();

        group.MapGet("/resource-policies", async (IdentityDbContext db, HttpContext http, CancellationToken ct) =>
        {
            var filter = IamTenantHttpContext.RequireFilter(http);

            var query = db.IamResourcePolicies.AsNoTracking();
            if (filter.AllowedScopeIds is not null)
                query = query.Where(x => filter.AllowedScopeIds.Contains(x.ScopeId));
            return Results.Ok(await query.OrderBy(x => x.ServiceKey).ThenBy(x => x.ResourcePattern).ToListAsync(ct));
        }).RequireAuthorization(AuthorizationPolicyNames.Permissions.AdminRolesRead)
            .WithTenantReadScope(HisHopePermissions.Admin.RolesRead);

        group.MapPost("/resource-policies", async (ResourcePolicyRequest request, IdentityDbContext db, HttpContext http, CancellationToken ct) =>
        {
            if (request.ScopeId == Guid.Empty || string.IsNullOrWhiteSpace(request.ServiceKey) || string.IsNullOrWhiteSpace(request.ResourcePattern) || !TryParseArray(request.StatementsJson, out _))
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["resourcePolicy"] = ["scopeId, serviceKey, resourcePattern and a JSON statements array are required."] });
            var filter = IamTenantHttpContext.RequireFilter(http);
            if (IamTenantAccessGuard.EnsureScopeAccess(request.ScopeId, filter) is { } scopeError) return scopeError;
            if (!await db.IamScopes.AnyAsync(x => x.Id == request.ScopeId && x.IsActive, ct)) return Results.NotFound("scope_not_found");
            var serviceKey = request.ServiceKey.Trim().ToLowerInvariant();
            if (!await db.IamServiceDefinitions.AnyAsync(x => x.Key == serviceKey && x.IsActive, ct)) return Results.NotFound("service_not_found");
            if (await db.IamResourcePolicies.AnyAsync(x => x.ScopeId == request.ScopeId && x.ServiceKey == serviceKey && x.ResourcePattern == request.ResourcePattern.Trim(), ct)) return Results.Conflict("resource_policy_exists");
            var item = new IamResourcePolicy { ScopeId = request.ScopeId, ServiceKey = serviceKey, ResourcePattern = request.ResourcePattern.Trim(), StatementsJson = request.StatementsJson, CreatedBy = http.User.FindFirstValue("sub") };
            db.IamResourcePolicies.Add(item); await db.SaveChangesAsync(ct); return Results.Created(IdentityApiRoutes.IamResourcePolicy(item.Id), item);
        }).RequireAuthorization(AuthorizationPolicyNames.Permissions.AdminRolesWrite)
            .WithTenantMutationScope();

        group.MapPut("/resource-policies/{id:guid}", async (Guid id, ResourcePolicyRequest request, IdentityDbContext db, HttpContext http, CancellationToken ct) =>
        {
            var filter = IamTenantHttpContext.RequireFilter(http);
            var item = await db.IamResourcePolicies.FirstOrDefaultAsync(x => x.Id == id, ct);
            if (item is null) return Results.NotFound("resource_policy_not_found");
            if (IamTenantAccessGuard.EnsureScopeAccess(item.ScopeId, filter) is { } currentScopeError) return currentScopeError;
            if (request.ScopeId == Guid.Empty || string.IsNullOrWhiteSpace(request.ServiceKey) || string.IsNullOrWhiteSpace(request.ResourcePattern) || !TryParseArray(request.StatementsJson, out _))
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["resourcePolicy"] = ["scopeId, serviceKey, resourcePattern and a JSON statements array are required."] });
            if (IamTenantAccessGuard.EnsureScopeAccess(request.ScopeId, filter) is { } targetScopeError) return targetScopeError;
            if (!await db.IamScopes.AnyAsync(x => x.Id == request.ScopeId && x.IsActive, ct)) return Results.NotFound("scope_not_found");
            var serviceKey = request.ServiceKey.Trim().ToLowerInvariant();
            if (!await db.IamServiceDefinitions.AnyAsync(x => x.Key == serviceKey && x.IsActive, ct)) return Results.NotFound("service_not_found");
            var before = item.StatementsJson;
            item.ScopeId = request.ScopeId; item.ServiceKey = serviceKey; item.ResourcePattern = request.ResourcePattern.Trim(); item.StatementsJson = request.StatementsJson; item.Version++; item.LifecycleStatus = AuthorizationConstants.LifecycleStatuses.Draft; item.PublishedAt = null;
            await db.SaveChangesAsync(ct);
            await AdminAudit.LogAuthorizationChangeAsync(db, http, "IAM_RESOURCE_POLICY_UPDATE", "IamResourcePolicy", id.ToString(), "Resource policy updated and returned to draft.", before, item.StatementsJson, ct);
            return Results.Ok(item);
        }).RequireAuthorization(AuthorizationPolicyNames.Permissions.AdminRolesWrite)
            .WithTenantMutationScope();

        group.MapPost("/resource-policies/{id:guid}/publish", async (Guid id, IdentityDbContext db, HttpContext http, CancellationToken ct) =>
        {
            var filter = IamTenantHttpContext.RequireFilter(http);
            var item = await db.IamResourcePolicies.FirstOrDefaultAsync(x => x.Id == id, ct);
            if (item is null) return Results.NotFound("resource_policy_not_found");
            if (IamTenantAccessGuard.EnsureScopeAccess(item.ScopeId, filter) is { } scopeError) return scopeError;
            if (!TryValidateResourcePolicy(item.StatementsJson, out var policyError))
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["statementsJson"] = [policyError!] });
            item.Version++; item.LifecycleStatus = AuthorizationConstants.LifecycleStatuses.Published; item.PublishedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(ct); return Results.Ok(item);
        }).RequireAuthorization(AuthorizationPolicyNames.Permissions.AdminRolesWrite)
            .WithTenantMutationScope();

        group.MapPost("/analyzer", async (IdentityDbContext db, HttpContext http, CancellationToken ct) =>
        {
            var filter = IamTenantHttpContext.RequireFilter(http);

            var findings = new List<object>();
            var setQuery = db.IamPermissionSets.AsNoTracking();
            var roleQuery = db.IamWorkloadRoles.AsNoTracking();
            if (filter.AllowedScopeIds is not null)
            {
                setQuery = setQuery.Where(x => filter.AllowedScopeIds.Contains(x.ScopeId));
                roleQuery = roleQuery.Where(x => filter.AllowedScopeIds.Contains(x.ScopeId));
            }
            var sets = await setQuery.ToListAsync(ct);
            foreach (var set in sets)
            {
                var permissions = JsonSerializer.Deserialize<string[]>(set.PermissionsJson) ?? [];
                if (permissions.Any(x => x == "*" || x.EndsWith(".*", StringComparison.Ordinal)))
                    findings.Add(new { resourceType = "permissionSet", resourceId = set.Id, severity = "high", code = "WILDCARD_PERMISSION", message = "Permission set contains wildcard access." });
            }
            var roles = await roleQuery.ToListAsync(ct);
            foreach (var role in roles)
            {
                if (role.MaxSessionSeconds > 1800)
                    findings.Add(new { resourceType = "workloadRole", resourceId = role.Id, severity = "medium", code = "LONG_SESSION", message = "Workload session exceeds 30 minutes." });
                if (string.IsNullOrWhiteSpace(role.Audience))
                    findings.Add(new { resourceType = "workloadRole", resourceId = role.Id, severity = "high", code = "MISSING_AUDIENCE", message = "Workload role must be audience restricted." });
            }
            return Results.Ok(new { schemaVersion = "iam-access-analyzer.v1", analyzedAt = DateTime.UtcNow, findingCount = findings.Count, findings });
        }).RequireAuthorization(AuthorizationPolicyNames.Permissions.AdminPolicySimulate)
            .WithTenantReadScope(HisHopePermissions.Admin.PolicySimulate);

        group.MapPost("/analyzer/new-access-diff", (NewAccessDiffRequest request) =>
        {
            var before = request.Before?.Select(x => x.Trim().ToLowerInvariant()).Where(x => x.Length > 0).ToHashSet(StringComparer.Ordinal) ?? [];
            var after = request.After?.Select(x => x.Trim().ToLowerInvariant()).Where(x => x.Length > 0).ToHashSet(StringComparer.Ordinal) ?? [];
            return Results.Ok(new { schemaVersion = "iam-access-analyzer.v1", added = after.Except(before).Order().ToArray(), removed = before.Except(after).Order().ToArray(), unchanged = before.Intersect(after).Order().ToArray(), evaluatedAt = DateTime.UtcNow });
        }).RequireAuthorization(AuthorizationPolicyNames.Permissions.AdminPolicySimulate);

        group.MapGet("/analyzer/unused", async (IdentityDbContext db, HttpContext http, CancellationToken ct) =>
        {
            _ = IamTenantHttpContext.RequireFilter(http);

            var used = await db.RolePermissions.AsNoTracking().Select(x => x.PermissionCode).Distinct().ToListAsync(ct);
            var publishedSets = await db.IamPermissionSets.AsNoTracking().Where(x => x.LifecycleStatus == AuthorizationConstants.LifecycleStatuses.Published).Select(x => x.PermissionsJson).ToListAsync(ct);
            used.AddRange(publishedSets.SelectMany(x => JsonSerializer.Deserialize<string[]>(x) ?? []));
            var unused = HisHopePermissions.All.Except(used, StringComparer.Ordinal).Order().ToArray();
            return Results.Ok(new { schemaVersion = "iam-access-analyzer.v1", unusedPermissions = unused, count = unused.Length, evaluatedAt = DateTime.UtcNow });
        }).RequireAuthorization(AuthorizationPolicyNames.Permissions.AdminPolicySimulate)
            .WithTenantReadScope(HisHopePermissions.Admin.PolicySimulate);
    }

    public sealed record ScopeRequest(string Key, string DisplayName, string Kind, Guid? ParentId);
    public sealed record ServiceRequest(string Key, string DisplayName, string PermissionPrefix, string Owner = "identity-service");
    public sealed record PermissionSetRequest(string Key, string DisplayName, Guid ScopeId, IReadOnlyCollection<string> Permissions);
    public sealed record AssignmentRequest(Guid PrincipalId, string PrincipalType, Guid ScopeId, DateTime? ExpiresAt);
    public sealed record WorkloadRoleRequest(string Key, string DisplayName, Guid ScopeId, string Audience, string TrustPolicyJson, IReadOnlyCollection<string>? Permissions, int MaxSessionSeconds = 900);
    public sealed record BoundaryRequest(Guid PrincipalId, string PrincipalType, Guid ScopeId, IReadOnlyCollection<string>? AllowedPermissions, string ResourceConstraintsJson = "{}", bool IsActive = true);
    public sealed record ResourcePolicyRequest(Guid ScopeId, string ServiceKey, string ResourcePattern, string StatementsJson);
    public sealed record GroupRequest(string Key, string DisplayName, Guid ScopeId);
    public sealed record NewAccessDiffRequest(IReadOnlyCollection<string>? Before, IReadOnlyCollection<string>? After);

    private static void AddIssuer(List<object> issuers, string? key, string? displayName, string? authority, string protocol, string? clientId)
    {
        if (string.IsNullOrWhiteSpace(key) || !Uri.TryCreate(authority, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps)
            return;
        issuers.Add(new { key, displayName, issuer = uri.ToString().TrimEnd('/'), protocol, configured = !string.IsNullOrWhiteSpace(clientId), active = true });
    }

    private static bool TryValidateTrust(string json, out string? error)
    {
        error = null;
        try
        {
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind != JsonValueKind.Object) { error = "Trust policy must be a JSON object."; return false; }
            if (!document.RootElement.TryGetProperty("principals", out var principals) || principals.ValueKind != JsonValueKind.Array || principals.GetArrayLength() == 0)
            { error = "Trust policy must contain a non-empty principals array."; return false; }
            return true;
        }
        catch (JsonException) { error = "Trust policy is not valid JSON."; return false; }
    }

    private static bool TryParseObject(string json, out JsonDocument? document)
    {
        try { document = JsonDocument.Parse(json); return document.RootElement.ValueKind == JsonValueKind.Object; }
        catch (JsonException) { document = null; return false; }
    }

    private static bool TryParseArray(string json, out JsonDocument? document)
    {
        try { document = JsonDocument.Parse(json); return document.RootElement.ValueKind == JsonValueKind.Array; }
        catch (JsonException) { document = null; return false; }
    }

    private static bool TryValidateResourcePolicy(string json, out string? error)
    {
        error = null;
        try
        {
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind != JsonValueKind.Array || document.RootElement.GetArrayLength() == 0)
            { error = "Policy statements must be a non-empty JSON array."; return false; }

            foreach (var statement in document.RootElement.EnumerateArray())
            {
                if (statement.ValueKind != JsonValueKind.Object)
                { error = "Each policy statement must be a JSON object."; return false; }
                if (!statement.TryGetProperty("principal", out var principal) || !HasStringValue(principal))
                { error = "Each policy statement requires a principal."; return false; }
                if (!statement.TryGetProperty("actions", out var actions) || !HasStringValue(actions))
                { error = "Each policy statement requires at least one action."; return false; }
                if (statement.TryGetProperty("effect", out var effect) &&
                    (effect.ValueKind != JsonValueKind.String || !string.Equals(effect.GetString(), "allow", StringComparison.OrdinalIgnoreCase) && !string.Equals(effect.GetString(), "deny", StringComparison.OrdinalIgnoreCase)))
                { error = "Policy effect must be allow or deny."; return false; }
                if (statement.TryGetProperty("condition", out var condition) && !TryValidateCondition(condition))
                { error = "Condition must contain supported operator objects with scalar string values."; return false; }
            }
            return true;
        }
        catch (JsonException) { error = "Policy statements are not valid JSON."; return false; }
    }

    private static bool HasStringValue(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.String) return !string.IsNullOrWhiteSpace(value.GetString());
        return value.ValueKind == JsonValueKind.Array && value.EnumerateArray().Any(item => item.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(item.GetString()));
    }

    private static bool TryValidateCondition(JsonElement condition)
    {
        var supportedOperators = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "StringEquals", "StringNotEquals", "StringLike", "StringNotLike",
            "ArnEquals", "ArnNotEquals", "ArnLike", "ArnNotLike", "Bool",
            "NumericEquals", "NumericLessThan", "NumericLessThanEquals",
            "NumericGreaterThan", "NumericGreaterThanEquals", "DateEquals",
            "DateLessThan", "DateLessThanEquals", "DateGreaterThan", "DateGreaterThanEquals"
        };
        if (condition.ValueKind != JsonValueKind.Object || condition.EnumerateObject().Any(item =>
                !supportedOperators.Contains(item.Name) || item.Value.ValueKind != JsonValueKind.Object ||
                !item.Value.EnumerateObject().Any() || item.Value.EnumerateObject().Any(property => !HasStringValue(property.Value))))
            return false;
        return condition.EnumerateObject().Any();
    }

    private static async Task<int> CountGovernanceSubjectsAsync<T>(
        IQueryable<T> query,
        HashSet<Guid>? allowedScopeIds,
        IdentityDbContext db,
        System.Linq.Expressions.Expression<Func<T, Guid>> subjectUserIdSelector,
        CancellationToken ct)
    {
        if (allowedScopeIds is null)
            return await query.CountAsync(ct);

        var tenantKeys = await db.IamScopes.AsNoTracking()
            .Where(scope => allowedScopeIds.Contains(scope.Id) && scope.Kind == "tenant")
            .Select(scope => scope.Key)
            .ToArrayAsync(ct);
        if (tenantKeys.Length == 0)
            return 0;

        var subjectUserIds = await query.Select(subjectUserIdSelector).Distinct().ToListAsync(ct);
        if (subjectUserIds.Count == 0)
            return 0;

        var allowedSubjectUserIds = await db.UserClaims.AsNoTracking()
            .Where(claim =>
                subjectUserIds.Contains(claim.UserId) &&
                claim.ClaimType == IamTenantScopeResolver.TenantMembershipClaimType &&
                tenantKeys.Contains(claim.ClaimValue))
            .Select(claim => claim.UserId)
            .Distinct()
            .ToListAsync(ct);
        if (allowedSubjectUserIds.Count == 0)
            return 0;

        return await query
            .Where(BuildSubjectUserIdContainsPredicate(subjectUserIdSelector, allowedSubjectUserIds.ToHashSet()))
            .CountAsync(ct);
    }

    private static System.Linq.Expressions.Expression<Func<T, bool>> BuildSubjectUserIdContainsPredicate<T>(
        System.Linq.Expressions.Expression<Func<T, Guid>> selector,
        HashSet<Guid> allowedUserIds)
    {
        var parameter = selector.Parameters[0];
        var body = System.Linq.Expressions.Expression.Call(
            typeof(Enumerable),
            nameof(Enumerable.Contains),
            [typeof(Guid)],
            System.Linq.Expressions.Expression.Constant(allowedUserIds),
            selector.Body);
        return System.Linq.Expressions.Expression.Lambda<Func<T, bool>>(body, parameter);
    }

    private static async Task<int> CountScopedAsync<T>(
        IQueryable<T> query,
        HashSet<Guid>? allowedScopeIds,
        System.Linq.Expressions.Expression<Func<T, Guid>> scopeIdSelector,
        CancellationToken ct)
    {
        if (allowedScopeIds is null)
            return await query.CountAsync(ct);

        return await query.Where(BuildScopeContainsPredicate(scopeIdSelector, allowedScopeIds)).CountAsync(ct);
    }

    private static System.Linq.Expressions.Expression<Func<T, bool>> BuildScopeContainsPredicate<T>(
        System.Linq.Expressions.Expression<Func<T, Guid>> selector,
        HashSet<Guid> allowedScopeIds)
    {
        var parameter = selector.Parameters[0];
        var body = System.Linq.Expressions.Expression.Call(
            typeof(Enumerable),
            nameof(Enumerable.Contains),
            [typeof(Guid)],
            System.Linq.Expressions.Expression.Constant(allowedScopeIds),
            selector.Body);
        return System.Linq.Expressions.Expression.Lambda<Func<T, bool>>(body, parameter);
    }

    private static bool IsValidParentKind(string parentKind, string childKind)
        => (parentKind, childKind) switch
        {
            ("organization", "tenant") => true,
            ("tenant", "account") => true,
            ("account", "environment") => true,
            _ => false
        };

    private static async Task<Guid[]> GetScopeLineageAsync(IdentityDbContext db, Guid scopeId, CancellationToken ct)
    {
        var scopes = await db.IamScopes.AsNoTracking().ToDictionaryAsync(x => x.Id, ct);
        if (!scopes.TryGetValue(scopeId, out var current) || !current.IsActive) return [];
        var result = new List<Guid>();
        var visited = new HashSet<Guid>();
        while (current is not null && visited.Add(current.Id))
        {
            result.Add(current.Id);
            if (!current.ParentId.HasValue || !scopes.TryGetValue(current.ParentId.Value, out current!)) break;
        }
        return result.ToArray();
    }

    private static async Task<bool> IsScopeWithinAsync(IdentityDbContext db, Guid scopeId, Guid ancestorScopeId, CancellationToken ct)
        => (await GetScopeLineageAsync(db, scopeId, ct)).Contains(ancestorScopeId);
}
