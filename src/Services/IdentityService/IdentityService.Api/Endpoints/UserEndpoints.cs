using His.Hope.IdentityService.Application.DTOs;
using His.Hope.Contracts;
using His.Hope.IdentityService.Application.UseCases.Users.Commands;
using His.Hope.IdentityService.Application.UseCases.Users.Queries;
using MediatR;
using His.Hope.Contracts.Query;
using His.Hope.Infrastructure.Security;
using His.Hope.IdentityService.Application.Interfaces;
using His.Hope.Infrastructure.Audit;
using System.Text.Json;
using His.Hope.IdentityService.Api.Authorization;
using His.Hope.IdentityService.Infrastructure.Persistence;
using His.Hope.Contracts.Identity;

namespace His.Hope.IdentityService.Api.Endpoints;

/// <summary>
/// User management endpoints for the Identity Service.
/// All endpoints require authorization.
/// </summary>
public static class UserEndpoints
{
    public static RouteGroupBuilder MapUserEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet(IdentityApiRoutes.UsersSegment, async (
            int page = 1,
            int pageSize = 20,
            string? search = null,
            string? role = null,
            bool? isActive = null,
            string? sort = null,
            IMediator mediator = null!,
            HttpContext http = null!,
            CancellationToken ct = default) =>
        {
            QueryRequest normalized;
            try
            {
                normalized = new QueryRequest(
                    page,
                    pageSize,
                    search,
                    sort,
                    Filters: new Dictionary<string, string?>
                    {
                        ["role"] = role,
                        ["isActive"] = isActive?.ToString()
                    })
                    .Normalize(
                        new HashSet<string>(["username", "email", "isactive", "createdat"], StringComparer.OrdinalIgnoreCase),
                        new HashSet<string>(["role", "isActive"], StringComparer.OrdinalIgnoreCase));
            }
            catch (ArgumentException ex) { return Results.ValidationProblem(new Dictionary<string, string[]> { ["query"] = [ex.Message] }); }
            if (normalized.Search?.Length > 100 || normalized.Filters["role"]?.Length > 100)
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["search"] = ["Search and role filters must be 100 characters or fewer."] });

            var tenantFilter = IamTenantHttpContext.RequireFilter(http);
            var result = await mediator.Send(
                new GetUsersQuery(
                    normalized.Page,
                    normalized.PageSize,
                    normalized.Search,
                    normalized.Filters["role"],
                    isActive,
                    normalized.Sort,
                    tenantFilter.AllowedTenantKeys?.ToArray()), ct);
            return Results.Ok(result);
        })
            .RequireAuthorization(AuthorizationPolicyNames.Permissions.AdminUsersRead)
            .WithTenantReadScope(HisHopePermissions.Admin.UsersRead);

        group.MapGet(IdentityApiRoutes.UsersSegment + "/{id:guid}", async (
            Guid id,
            IMediator mediator = null!,
            IdentityDbContext db = null!,
            HttpContext http = null!,
            CancellationToken ct = default) =>
        {
            var tenantFilter = IamTenantHttpContext.RequireFilter(http);
            var user = await mediator.Send(new GetUserByIdQuery(id), ct);
            if (user is null)
                return Results.NotFound();

            if (await IamTenantAccessGuard.EnsureUserAccessAsync(db, id, tenantFilter, ct) is { } accessError)
                return accessError;

            return Results.Ok(user);
        })
            .RequireAuthorization(AuthorizationPolicyNames.Permissions.AdminUsersRead)
            .WithTenantReadScope(HisHopePermissions.Admin.UsersRead);

        group.MapPost(IdentityApiRoutes.UsersSegment, async (
            CreateUserRequest request,
            IMediator mediator = null!,
            IAuditService audit = null!,
            HttpContext http = null!,
            CancellationToken ct = default) =>
        {
            try
            {
                var command = new CreateUserCommand(
                    request.Username, request.Email, request.Password,
                    request.FirstName, request.LastName, request.MiddleName,
                    request.LicenseNumber, request.Specialty,
                    request.PhoneNumber, request.Role);

                var user = await mediator.Send(command, ct);
                await AdminAudit.LogAsync(audit, http, "CREATE", "User", user.Id.ToString(), ct);
                return Results.Created($"/api/v1/auth/users/{user.Id}", user);
            }
            catch (InvalidOperationException ex)
            {
                return Results.Problem(statusCode: 400, detail: ex.Message,
                    extensions: new Dictionary<string, object?> { [ApiProblemExtensions.ErrorCode] = ApiErrorCodes.UserRequestRejected });
            }
        })
            .RequireAuthorization(AuthorizationPolicyNames.Permissions.AdminUsersWrite)
            .WithTenantMutationScope();

        group.MapPut(IdentityApiRoutes.UsersSegment + "/{id:guid}", async (
            Guid id,
            UpdateUserRequest request,
            IMediator mediator = null!,
            IdentityDbContext db = null!,
            IAuditService audit = null!,
            HttpContext http = null!,
            ITokenBlacklistService tokenBlacklist = null!,
            CancellationToken ct = default) =>
        {
            var tenantFilter = IamTenantHttpContext.RequireFilter(http);
            if (await IamTenantAccessGuard.EnsureUserAccessAsync(db, id, tenantFilter, ct) is { } accessError)
                return accessError;

            try
            {
                var command = new UpdateUserCommand(
                    id, request.FirstName, request.LastName, request.Email,
                    request.PhoneNumber, request.Role, request.IsActive, request.ConcurrencyToken);

                var user = await mediator.Send(command, ct);
                await tokenBlacklist.RevokeAllUserTokensAsync(id.ToString(), ct);
                await AdminAudit.LogAsync(audit, http, "UPDATE", "User", id.ToString(), ct);
                return Results.Ok(user);
            }
            catch (KeyNotFoundException)
            {
                return Results.NotFound();
            }
            catch (InvalidOperationException ex)
            {
                return Results.Problem(statusCode: ex.Message.StartsWith("CONCURRENCY_CONFLICT:", StringComparison.Ordinal) ? 409 : 400,
                    detail: ex.Message,
                    extensions: new Dictionary<string, object?> { ["errorCode"] = ex.Message.StartsWith("CONCURRENCY_CONFLICT:", StringComparison.Ordinal) ? ApiErrorCodes.ConcurrencyConflict : ApiErrorCodes.UserRequestRejected });
            }
        })
            .RequireAuthorization(AuthorizationPolicyNames.Permissions.AdminUsersWrite)
            .WithTenantMutationScope();

        group.MapPut(IdentityApiRoutes.UsersSegment + "/{id:guid}/deactivate", async (
            Guid id,
            IMediator mediator = null!,
            IdentityDbContext db = null!,
            ITokenBlacklistService tokenBlacklist = null!,
            IAuditService audit = null!,
            HttpContext http = null!,
            CancellationToken ct = default) =>
        {
            var tenantFilter = IamTenantHttpContext.RequireFilter(http);
            if (await IamTenantAccessGuard.EnsureUserAccessAsync(db, id, tenantFilter, ct) is { } accessError)
                return accessError;

            try
            {
                await mediator.Send(new DeactivateUserCommand(id), ct);
                await tokenBlacklist.RevokeAllUserTokensAsync(id.ToString(), ct);
                await AdminAudit.LogAsync(audit, http, "DEACTIVATE", "User", id.ToString(), ct);
                return Results.NoContent();
            }
            catch (KeyNotFoundException)
            {
                return Results.NotFound();
            }
        })
            .RequireAuthorization(AuthorizationPolicyNames.Permissions.AdminUsersWrite)
            .WithTenantMutationScope();

        group.MapPut(IdentityApiRoutes.UsersSegment + "/{id:guid}/activate", async (
            Guid id,
            IMediator mediator = null!,
            IdentityDbContext db = null!,
            ITokenBlacklistService tokenBlacklist = null!,
            IAuditService audit = null!,
            HttpContext http = null!,
            CancellationToken ct = default) =>
        {
            var tenantFilter = IamTenantHttpContext.RequireFilter(http);
            if (await IamTenantAccessGuard.EnsureUserAccessAsync(db, id, tenantFilter, ct) is { } accessError)
                return accessError;

            try
            {
                await mediator.Send(new ActivateUserCommand(id), ct);
                await tokenBlacklist.RevokeAllUserTokensAsync(id.ToString(), ct);
                await AdminAudit.LogAsync(audit, http, "ACTIVATE", "User", id.ToString(), ct);
                return Results.NoContent();
            }
            catch (KeyNotFoundException)
            {
                return Results.NotFound();
            }
        })
            .RequireAuthorization(AuthorizationPolicyNames.Permissions.AdminUsersWrite)
            .WithTenantMutationScope();

        group.MapPut(IdentityApiRoutes.UsersSegment + "/{id:guid}/roles", async (
            Guid id,
            AssignRolesRequest request,
            IMediator mediator = null!,
            ITokenBlacklistService tokenBlacklist = null!,
            IApplicationDbContext db = null!,
            IdentityDbContext identityDb = null!,
            IAuditService audit = null!,
            HttpContext http = null!,
            CancellationToken ct = default) =>
        {
            var tenantFilter = IamTenantHttpContext.RequireFilter(http);
            if (await IamTenantAccessGuard.EnsureUserAccessAsync(identityDb, id, tenantFilter, ct) is { } accessError)
                return accessError;

            try
            {
                var governanceError = await RoleGovernanceEvaluator.ValidateRoleAssignmentAsync(
                    db, http.User, id, request.RoleIds, ct);
                if (governanceError is not null)
                    return Results.Problem(statusCode: governanceError.StartsWith("FACILITY_SCOPE_DENIED", StringComparison.Ordinal) ? 403 : 400,
                        extensions: new Dictionary<string, object?> { ["errorCode"] = governanceError.StartsWith("FACILITY_SCOPE_DENIED", StringComparison.Ordinal) ? ApiErrorCodes.FacilityScopeDenied : ApiErrorCodes.UserRequestRejected });
                var user = await mediator.Send(
                    new AssignRolesCommand(id, request.RoleIds), ct);
                await tokenBlacklist.RevokeAllUserTokensAsync(id.ToString(), ct);
                await AdminAudit.LogAuthorizationChangeAsync(
                    db, http, "ROLE_ASSIGNMENT", "User", id.ToString(),
                    "Role assignment changed through admin control plane.",
                    null, JsonSerializer.Serialize(new { roleIds = request.RoleIds }), ct);
                await AdminAudit.LogAsync(audit, http, "ASSIGN_ROLES", "User", id.ToString(), ct);
                return Results.Ok(user);
            }
            catch (KeyNotFoundException)
            {
                return Results.NotFound();
            }
            catch (InvalidOperationException ex)
            {
                return Results.Problem(statusCode: 400, detail: ex.Message,
                    extensions: new Dictionary<string, object?> { [ApiProblemExtensions.ErrorCode] = ApiErrorCodes.UserRequestRejected });
            }
        })
            .RequireAuthorization(AuthorizationPolicyNames.Permissions.AdminRolesWrite)
            .WithTenantMutationScope();

        return group;
    }
}
