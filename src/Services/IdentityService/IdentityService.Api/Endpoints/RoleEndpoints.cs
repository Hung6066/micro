using His.Hope.IdentityService.Application.DTOs;
using His.Hope.Contracts;
using His.Hope.IdentityService.Application.UseCases.Roles.Commands;
using His.Hope.IdentityService.Application.UseCases.Roles.Queries;
using MediatR;
using His.Hope.Infrastructure.Audit;
using His.Hope.Contracts.Query;
using His.Hope.Infrastructure.Security;
using His.Hope.IdentityService.Infrastructure.Persistence;
using His.Hope.IdentityService.Application.Interfaces;
using His.Hope.IdentityService.Domain.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using His.Hope.Authorization;
using His.Hope.IdentityService.Api.Authorization;
using His.Hope.Contracts.Identity;
using His.Hope.SharedKernel.Authorization;
using His.Hope.SharedKernel.Domain.Common;

namespace His.Hope.IdentityService.Api.Endpoints;

/// <summary>
/// Role and permission management endpoints.
/// All endpoints require authorization.
/// </summary>
public static class RoleEndpoints
{
    public static RouteGroupBuilder MapRoleEndpoints(this RouteGroupBuilder group)
    {
        // GET /api/v1/auth/roles - List all roles
        group.MapGet(IdentityApiRoutes.RolesSegment, async (
            int page = 1,
            int pageSize = 20,
            string? search = null,
            string? sort = null,
            IMediator mediator = null!,
            HttpContext http = null!,
            CancellationToken ct = default) =>
        {
            QueryRequest normalized;
            try
            {
                normalized = new QueryRequest(page, pageSize, search, sort)
                    .Normalize(
                        new HashSet<string>(["name", "description", "createdat"], StringComparer.OrdinalIgnoreCase),
                        new HashSet<string>());
            }
            catch (ArgumentException ex) { return Results.ValidationProblem(new Dictionary<string, string[]> { ["query"] = [ex.Message] }); }
            if (normalized.Search?.Length > 100)
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["search"] = ["Search must be 100 characters or fewer."] });

            var tenantFilter = IamTenantHttpContext.RequireFilter(http);
            var roles = await mediator.Send(
                new GetRolesQuery(
                    normalized.Page,
                    normalized.PageSize,
                    normalized.Search,
                    normalized.Sort,
                    tenantFilter.AllowedTenantKeys?.ToArray()), ct);
            return Results.Ok(roles);
        })
            .RequireAuthorization(AuthorizationPolicyNames.Permissions.AdminRolesRead)
            .WithTenantReadScope(HisHopePermissions.Admin.RolesRead);

        // GET /api/v1/auth/roles/{id} - Get role with permissions
        group.MapGet(IdentityApiRoutes.RolesSegment + "/{id:guid}", async (
            Guid id,
            IMediator mediator = null!,
            IdentityDbContext db = null!,
            HttpContext http = null!,
            CancellationToken ct = default) =>
        {
            var tenantFilter = IamTenantHttpContext.RequireFilter(http);
            var role = Guard.Against.NotFound(
                await mediator.Send(new GetRoleByIdQuery(id), ct), "Role", id);

            if (await IamTenantAccessGuard.EnsureRoleVisibleAsync(db, id, tenantFilter, ct) is { } visibilityError)
                return visibilityError;

            return Results.Ok(role);
        })
            .RequireAuthorization(AuthorizationPolicyNames.Permissions.AdminRolesRead)
            .WithTenantReadScope(HisHopePermissions.Admin.RolesRead);

        // POST /api/v1/auth/roles - Create role
        group.MapPost(IdentityApiRoutes.RolesSegment, async (
            CreateRoleRequest request,
            IMediator mediator = null!,
            IAuditService audit = null!,
            HttpContext http = null!,
            IApplicationDbContext db = null!,
            IdentityDbContext identityDb = null!,
            CancellationToken ct = default) =>
        {
            _ = IamTenantHttpContext.RequireFilter(http);
            try
            {
                if (!IsKnownRoleOwner(request.Owner))
                    return Results.ValidationProblem(new Dictionary<string, string[]> { ["owner"] = ["Owner must be selected from the server catalog."] });
                var governanceError = await RoleGovernanceEvaluator.ValidateRolePermissionsAsync(
                    db, http.User, request.Permissions, ct);
                if (governanceError is not null)
                    return Results.Problem(statusCode: 403, extensions: new Dictionary<string, object?> { ["errorCode"] = ApiErrorCodes.FacilityScopeDenied });
                var role = await mediator.Send(
                    new CreateRoleCommand(request.Name, request.Description, request.Permissions, request.Owner), ct);
                await CaptureTemplateVersionAsync(db, role.Id, "published", http.User.FindFirst(His.Hope.SharedKernel.Protocol.HisHopeProtocolConstants.Claims.Subject)?.Value, ct);
                await AdminAudit.LogAuthorizationChangeAsync(db, http, "ROLE_CREATE", "Role", role.Id.ToString(),
                    "Role created through admin control plane.", null, JsonSerializer.Serialize(new { request.Name, request.Description, request.Permissions }), ct);
                await AdminAudit.LogAsync(audit, http, "CREATE", "Role", role.Id.ToString(), ct);
                return Results.Created($"/api/v1/auth/roles/{role.Id}", role);
            }
            catch (InvalidOperationException ex)
            {
                return Results.Problem(statusCode: 400, detail: ex.Message,
                    extensions: new Dictionary<string, object?> { [ApiProblemExtensions.ErrorCode] = ApiErrorCodes.RoleRequestRejected });
            }
        }).RequireAuthorization(AuthorizationPolicyNames.Permissions.AdminRolesWrite)
            .WithTenantMutationScope();

        // PUT /api/v1/auth/roles/{id} - Update role
        group.MapPut(IdentityApiRoutes.RolesSegment + "/{id:guid}", async (
            Guid id,
            UpdateRoleRequest request,
            IMediator mediator = null!,
            IAuditService audit = null!,
            HttpContext http = null!,
            IdentityDbContext db = null!,
            IApplicationDbContext auditDb = null!,
            ITokenBlacklistService tokenBlacklist = null!,
            CancellationToken ct = default) =>
        {
            var tenantFilter = IamTenantHttpContext.RequireFilter(http);
            if (await IamTenantAccessGuard.EnsureRoleVisibleAsync(db, id, tenantFilter, ct) is { } visibilityError)
                return visibilityError;

            try
            {
                if (!IsKnownRoleOwner(request.Owner))
                    return Results.ValidationProblem(new Dictionary<string, string[]> { ["owner"] = ["Owner must be selected from the server catalog."] });
                var governanceError = await RoleGovernanceEvaluator.ValidateRolePermissionsAsync(
                    db, http.User, request.Permissions, ct);
                if (governanceError is not null)
                    return Results.Problem(statusCode: 403, extensions: new Dictionary<string, object?> { ["errorCode"] = ApiErrorCodes.FacilityScopeDenied });
                var role = await mediator.Send(
                    new UpdateRoleCommand(id, request.Name, request.Description, request.Permissions, request.ConcurrencyToken, request.Owner), ct);
                await RevokeRoleUsersAsync(db, tokenBlacklist, id, ct);
                await CaptureTemplateVersionAsync(auditDb, id, "published", http.User.FindFirst(His.Hope.SharedKernel.Protocol.HisHopeProtocolConstants.Claims.Subject)?.Value, ct);
                await AdminAudit.LogAuthorizationChangeAsync(auditDb, http, "ROLE_UPDATE", "Role", id.ToString(),
                    "Role permissions or metadata changed through admin control plane.", null, JsonSerializer.Serialize(new { request.Name, request.Description, request.Permissions }), ct);
                await AdminAudit.LogAsync(audit, http, "UPDATE", "Role", id.ToString(), ct);
                return Results.Ok(role);
            }
            catch (InvalidOperationException ex)
            {
                return Results.Problem(statusCode: ex.Message.StartsWith("CONCURRENCY_CONFLICT:", StringComparison.Ordinal) ? 409 : 400,
                    detail: ex.Message,
                    extensions: new Dictionary<string, object?> { ["errorCode"] = ex.Message.StartsWith("CONCURRENCY_CONFLICT:", StringComparison.Ordinal) ? ApiErrorCodes.ConcurrencyConflict : ApiErrorCodes.RoleRequestRejected });
            }
        }).RequireAuthorization(AuthorizationPolicyNames.Permissions.AdminRolesWrite)
            .WithTenantMutationScope();

        // GET /api/v1/auth/roles/{id}/versions - immutable role template history
        group.MapGet(IdentityApiRoutes.RolesSegment + "/{id:guid}/versions", async (
            Guid id,
            IApplicationDbContext db = null!,
            IdentityDbContext identityDb = null!,
            HttpContext http = null!,
            CancellationToken ct = default) =>
        {
            var tenantFilter = IamTenantHttpContext.RequireFilter(http);
            if (await IamTenantAccessGuard.EnsureRoleVisibleAsync(identityDb, id, tenantFilter, ct) is { } visibilityError)
                return visibilityError;

            _ = Guard.Against.NotFound(
                await db.Roles.AsNoTracking().FirstOrDefaultAsync(role => role.Id == id, ct), "Role", id);
            var versions = await db.RoleTemplateVersions.AsNoTracking()
                .Where(version => version.RoleId == id)
                .OrderByDescending(version => version.Version)
                .Select(version => new RoleTemplateVersionDto(
                    version.Id, version.RoleId, version.Version, version.Name,
                    version.Description, version.Owner, version.RiskTier,
                    version.ReviewCadenceDays, version.LifecycleStatus,
                    version.PermissionsJson, version.CreatedBy, version.CreatedAt,
                    version.PublishedAt, version.PublishedBy))
                .ToListAsync(ct);
            return Results.Ok(versions);
        })
            .RequireAuthorization(AuthorizationPolicyNames.Permissions.AdminRolesRead)
            .WithTenantReadScope(HisHopePermissions.Admin.RolesRead);

        // POST /api/v1/auth/roles/{id}/publish - explicit control-plane publish
        group.MapPost(IdentityApiRoutes.RolesSegment + "/{id:guid}/publish", async (
            Guid id,
            IApplicationDbContext db = null!,
            IdentityDbContext identityDb = null!,
            IAuditService audit = null!,
            HttpContext http = null!,
            CancellationToken ct = default) =>
        {
            if (StepUpAuthenticationGuard.RequireFreshMfa(http) is { } stepUp) return stepUp;
            var tenantFilter = IamTenantHttpContext.RequireFilter(http);
            if (await IamTenantAccessGuard.EnsureRoleVisibleAsync(identityDb, id, tenantFilter, ct) is { } visibilityError)
                return visibilityError;

            var role = Guard.Against.NotFound(
                await db.Roles.Include(item => item.RolePermissions)
                    .FirstOrDefaultAsync(item => item.Id == id, ct), "Role", id);
            if (role.IsSystem)
                return Results.Problem(statusCode: 409, extensions: new Dictionary<string, object?> { [ApiProblemExtensions.ErrorCode] = ApiErrorCodes.SystemRoleImmutable });
            var governanceError = await RoleGovernanceEvaluator.ValidateRolePermissionsAsync(
                db, http.User, role.RolePermissions.Select(link => link.PermissionCode), ct);
            if (governanceError is not null)
                return Results.Problem(statusCode: 403, extensions: new Dictionary<string, object?> { ["errorCode"] = ApiErrorCodes.FacilityScopeDenied });
            if (string.Equals(role.LifecycleStatus, "retired", StringComparison.OrdinalIgnoreCase))
                return Results.Problem(statusCode: 409, extensions: new Dictionary<string, object?> { [ApiProblemExtensions.ErrorCode] = ApiErrorCodes.RetiredRoleCannotBePublished });

            var changeRequestValue = http.Request.Query["changeRequestId"].FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(changeRequestValue) && !Guid.TryParse(changeRequestValue, out var changeRequestId))
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["changeRequestId"] = ["changeRequestId must be a valid GUID."] });
            AuthorizationChangeRequest? approvedChange = null;
            if (Guid.TryParse(changeRequestValue, out changeRequestId))
            {
                approvedChange = await AuthorizationChangeRequestWorkflow.FindApprovedAsync(
                    db, changeRequestId, "Role", id, "role.publish", AuthorizationChangeRequestWorkflow.Actor(http), ct);
                if (approvedChange is null)
                    return Results.Conflict(new { errorCode = "authorization_change_not_approved" });
                using var snapshot = JsonDocument.Parse(approvedChange.PayloadJson);
                if (!snapshot.RootElement.TryGetProperty("authorizationVersion", out var version) ||
                    version.GetInt32() != role.AuthorizationVersion)
                    return Results.Conflict(new { errorCode = "authorization_change_stale" });
            }
            else
            {
                var pending = await AuthorizationChangeRequestWorkflow.CreatePendingAsync(
                    db, http, "Role", id, "role.publish",
                    JsonSerializer.Serialize(new { authorizationVersion = role.AuthorizationVersion }),
                    "Role template publish requires independent approval.", ct);
                return Results.Accepted($"/api/v1/admin/authorization-change-requests/{pending.Id:D}", new
                {
                    changeRequestId = pending.Id,
                    pending.Status,
                    pending.ExpiresAt
                });
            }

            var before = JsonSerializer.Serialize(new { role.LifecycleStatus, role.AuthorizationVersion });
            role.LifecycleStatus = "active";
            role.PublishedAt = DateTime.UtcNow;
            role.PublishedBy = http.User.FindFirst(His.Hope.SharedKernel.Protocol.HisHopeProtocolConstants.Claims.Subject)?.Value ?? "system";
            role.ConcurrencyStamp = Guid.NewGuid().ToString("N");
            await CaptureTemplateVersionAsync(db, role.Id, "published", role.PublishedBy, ct);
            await AdminAudit.LogAuthorizationChangeAsync(db, http, "ROLE_PUBLISH", "Role", id.ToString(),
                "Role template published through admin control plane.", before,
                JsonSerializer.Serialize(new { role.LifecycleStatus, role.AuthorizationVersion }), ct);
            await AuthorizationChangeRequestWorkflow.MarkExecutedAsync(db, approvedChange, http, ct);
            await AdminAudit.LogAsync(audit, http, "PUBLISH", "Role", id.ToString(), ct);
            return Results.Ok(new { role.Id, role.AuthorizationVersion, role.LifecycleStatus, role.PublishedAt });
        }).RequireAuthorization(AuthorizationPolicyNames.Permissions.AdminRolesWrite)
            .WithTenantMutationScope();

        // POST /api/v1/auth/roles/{id}/rollback - restore the previous published template
        group.MapPost(IdentityApiRoutes.RolesSegment + "/{id:guid}/rollback", async (
            Guid id,
            IApplicationDbContext db = null!,
            IdentityDbContext identityDb = null!,
            ITokenBlacklistService tokenBlacklist = null!,
            IAuditService audit = null!,
            HttpContext http = null!,
            CancellationToken ct = default) =>
        {
            if (StepUpAuthenticationGuard.RequireFreshMfa(http) is { } stepUp) return stepUp;
            var tenantFilter = IamTenantHttpContext.RequireFilter(http);
            if (await IamTenantAccessGuard.EnsureRoleVisibleAsync(identityDb, id, tenantFilter, ct) is { } visibilityError)
                return visibilityError;

            var role = Guard.Against.NotFound(
                await db.Roles.Include(item => item.RolePermissions)
                    .FirstOrDefaultAsync(item => item.Id == id, ct), "Role", id);
            if (role.IsSystem)
                return Results.Problem(statusCode: 409, extensions: new Dictionary<string, object?> { [ApiProblemExtensions.ErrorCode] = ApiErrorCodes.SystemRoleImmutable });
            var target = await db.RoleTemplateVersions.AsNoTracking()
                .Where(version => version.RoleId == id && version.Version < role.AuthorizationVersion && version.LifecycleStatus == "published")
                .OrderByDescending(version => version.Version)
                .FirstOrDefaultAsync(ct);
            if (target is null) return Results.Problem(statusCode: 409, extensions: new Dictionary<string, object?> { [ApiProblemExtensions.ErrorCode] = ApiErrorCodes.PreviousRoleTemplateUnavailable });

            var permissions = JsonSerializer.Deserialize<string[]>(target.PermissionsJson) ?? [];
            var governanceError = await RoleGovernanceEvaluator.ValidateRolePermissionsAsync(
                db, http.User, permissions, ct);
            if (governanceError is not null)
                return Results.Problem(statusCode: 403, extensions: new Dictionary<string, object?> { ["errorCode"] = ApiErrorCodes.FacilityScopeDenied });
            var validPermissions = await db.Permissions.AsNoTracking()
                .Where(permission => permissions.Contains(permission.Code))
                .Select(permission => permission.Code)
                .ToArrayAsync(ct);

            var changeRequestValue = http.Request.Query["changeRequestId"].FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(changeRequestValue) && !Guid.TryParse(changeRequestValue, out var changeRequestId))
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["changeRequestId"] = ["changeRequestId must be a valid GUID."] });
            AuthorizationChangeRequest? approvedChange = null;
            if (Guid.TryParse(changeRequestValue, out changeRequestId))
            {
                approvedChange = await AuthorizationChangeRequestWorkflow.FindApprovedAsync(
                    db, changeRequestId, "Role", id, "role.rollback", AuthorizationChangeRequestWorkflow.Actor(http), ct);
                if (approvedChange is null)
                    return Results.Conflict(new { errorCode = "authorization_change_not_approved" });
                using var snapshot = JsonDocument.Parse(approvedChange.PayloadJson);
                if (!snapshot.RootElement.TryGetProperty("currentVersion", out var currentVersion) ||
                    !snapshot.RootElement.TryGetProperty("targetVersion", out var targetVersion) ||
                    currentVersion.GetInt32() != role.AuthorizationVersion || targetVersion.GetInt32() != target.Version)
                    return Results.Conflict(new { errorCode = "authorization_change_stale" });
            }
            else
            {
                var pending = await AuthorizationChangeRequestWorkflow.CreatePendingAsync(
                    db, http, "Role", id, "role.rollback",
                    JsonSerializer.Serialize(new { currentVersion = role.AuthorizationVersion, targetVersion = target.Version }),
                    $"Role template rollback to version {target.Version} requires independent approval.", ct);
                return Results.Accepted($"/api/v1/admin/authorization-change-requests/{pending.Id:D}", new
                {
                    changeRequestId = pending.Id,
                    pending.Status,
                    pending.ExpiresAt,
                    targetVersion = target.Version
                });
            }

            var before = JsonSerializer.Serialize(new { role.Name, role.AuthorizationVersion, permissions = role.RolePermissions.Select(link => link.PermissionCode) });
            db.RolePermissions.RemoveRange(role.RolePermissions);
            foreach (var permissionCode in validPermissions)
                db.RolePermissions.Add(new RolePermission { RoleId = role.Id, PermissionCode = permissionCode });
            role.Name = target.Name;
            role.NormalizedName = target.Name.ToUpperInvariant();
            role.Description = target.Description;
            role.Owner = target.Owner;
            role.RiskTier = target.RiskTier;
            role.ReviewCadenceDays = target.ReviewCadenceDays;
            role.AuthorizationVersion++;
            role.LifecycleStatus = "active";
            role.PublishedAt = DateTime.UtcNow;
            role.PublishedBy = http.User.FindFirst(His.Hope.SharedKernel.Protocol.HisHopeProtocolConstants.Claims.Subject)?.Value ?? "system";
            role.ConcurrencyStamp = Guid.NewGuid().ToString("N");
            await db.SaveChangesAsync(ct);
            await RevokeRoleUsersAsync(identityDb, tokenBlacklist, id, ct);
            await CaptureTemplateVersionAsync(db, id, "published", role.PublishedBy, ct);
            await AdminAudit.LogAuthorizationChangeAsync(db, http, "ROLE_ROLLBACK", "Role", id.ToString(),
                $"Role template rolled back to version {target.Version}.", before,
                JsonSerializer.Serialize(new { role.Name, role.AuthorizationVersion, permissions = validPermissions }), ct);
            await AuthorizationChangeRequestWorkflow.MarkExecutedAsync(db, approvedChange, http, ct);
            await AdminAudit.LogAsync(audit, http, "ROLLBACK", "Role", id.ToString(), ct);
            return Results.Ok(new { role.Id, role.AuthorizationVersion, role.LifecycleStatus, restoredFromVersion = target.Version });
        }).RequireAuthorization(AuthorizationPolicyNames.Permissions.AdminRolesWrite)
            .WithTenantMutationScope();

        // DELETE /api/v1/auth/roles/{id} - Delete role (only if no users assigned)
        group.MapDelete(IdentityApiRoutes.RolesSegment + "/{id:guid}", async (
            Guid id,
            IMediator mediator = null!,
            IAuditService audit = null!,
            HttpContext http = null!,
            IApplicationDbContext db = null!,
            IdentityDbContext identityDb = null!,
            CancellationToken ct = default) =>
        {
            var tenantFilter = IamTenantHttpContext.RequireFilter(http);
            if (await IamTenantAccessGuard.EnsureRoleVisibleAsync(identityDb, id, tenantFilter, ct) is { } visibilityError)
                return visibilityError;

            try
            {
                var existingRole = Guard.Against.NotFound(
                    await db.Roles.AsNoTracking().FirstOrDefaultAsync(item => item.Id == id, ct), "Role", id);
                if (existingRole.IsSystem)
                    return Results.Problem(statusCode: 409, extensions: new Dictionary<string, object?> { [ApiProblemExtensions.ErrorCode] = ApiErrorCodes.SystemRoleImmutable });
                await mediator.Send(new DeleteRoleCommand(id), ct);
                await AdminAudit.LogAuthorizationChangeAsync(db, http, "ROLE_DELETE", "Role", id.ToString(),
                    "Role deleted through admin control plane.", null, null, ct);
                await AdminAudit.LogAsync(audit, http, "DELETE", "Role", id.ToString(), ct);
                return Results.NoContent();
            }
            catch (InvalidOperationException ex)
            {
                return Results.Problem(statusCode: 400, detail: ex.Message,
                    extensions: new Dictionary<string, object?> { [ApiProblemExtensions.ErrorCode] = ApiErrorCodes.RoleRequestRejected });
            }
        }).RequireAuthorization(AuthorizationPolicyNames.Permissions.AdminRolesWrite)
            .WithTenantMutationScope();

        // GET /api/v1/auth/permissions - List all permissions
        group.MapGet(IdentityApiRoutes.PermissionsSegment, async (
            IMediator mediator = null!,
            CancellationToken ct = default) =>
        {
            var permissions = await mediator.Send(new GetPermissionsQuery(), ct);
            return Results.Ok(permissions);
        }).RequireAuthorization(AuthorizationPolicyNames.Permissions.AdminPermissionsRead);

        // GET /api/v1/auth/role-owners - Server-owned role owner catalog
        group.MapGet("/role-owners", () =>
        {
            var owners = HisHopePermissions.AllDescriptors
                .Select(permission => permission.Owner)
                .Append("identity-service")
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(owner => owner, StringComparer.OrdinalIgnoreCase)
                .Select(owner => new RoleOwnerDto(owner, owner))
                .ToArray();
            return Results.Ok(owners);
        }).RequireAuthorization(AuthorizationPolicyNames.Permissions.AdminRolesRead);

        return group;
    }

    private static async Task RevokeRoleUsersAsync(
        IdentityDbContext db,
        ITokenBlacklistService tokenBlacklist,
        Guid roleId,
        CancellationToken ct)
    {
        var userIds = await db.Set<IdentityUserRole<Guid>>()
            .Where(link => link.RoleId == roleId)
            .Select(link => link.UserId)
            .Distinct()
            .ToArrayAsync(ct);

        foreach (var userId in userIds)
            await tokenBlacklist.RevokeAllUserTokensAsync(userId.ToString(), ct);
    }

    private static bool IsKnownRoleOwner(string? owner) =>
        !string.IsNullOrWhiteSpace(owner) && HisHopePermissions.AllDescriptors
            .Select(permission => permission.Owner)
            .Append("identity-service")
            .Contains(owner.Trim(), StringComparer.OrdinalIgnoreCase);

    private static async Task CaptureTemplateVersionAsync(
        IApplicationDbContext db,
        Guid roleId,
        string lifecycleStatus,
        string? actor,
        CancellationToken ct)
    {
        var role = await db.Roles.AsNoTracking().FirstAsync(item => item.Id == roleId, ct);
        var permissions = await db.RolePermissions.AsNoTracking()
            .Where(link => link.RoleId == roleId)
            .Select(link => link.PermissionCode)
            .OrderBy(code => code)
            .ToArrayAsync(ct);
        var existing = await db.RoleTemplateVersions.AnyAsync(version => version.RoleId == roleId && version.Version == role.AuthorizationVersion, ct);
        if (existing) return;
        db.RoleTemplateVersions.Add(new RoleTemplateVersion
        {
            RoleId = role.Id,
            Version = role.AuthorizationVersion,
            Name = role.Name ?? string.Empty,
            Description = role.Description,
            Owner = role.Owner,
            RiskTier = role.RiskTier,
            ReviewCadenceDays = role.ReviewCadenceDays,
            LifecycleStatus = lifecycleStatus,
            PermissionsJson = JsonSerializer.Serialize(permissions),
            CreatedBy = actor,
            PublishedAt = lifecycleStatus == "published" ? DateTime.UtcNow : null,
            PublishedBy = lifecycleStatus == "published" ? actor : null
        });
        await db.SaveChangesAsync(ct);
    }

    public sealed record RoleTemplateVersionDto(
        Guid Id,
        Guid RoleId,
        int Version,
        string Name,
        string? Description,
        string Owner,
        string RiskTier,
        int ReviewCadenceDays,
        string LifecycleStatus,
        string PermissionsJson,
        string? CreatedBy,
        DateTime CreatedAt,
        DateTime? PublishedAt,
        string? PublishedBy);

    public sealed record RoleOwnerDto(string Key, string Name);
}
