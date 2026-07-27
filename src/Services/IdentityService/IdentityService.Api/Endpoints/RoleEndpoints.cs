using His.Hope.IdentityService.Application.DTOs;
using His.Hope.IdentityService.Application.UseCases.Roles.Commands;
using His.Hope.IdentityService.Application.UseCases.Roles.Queries;
using MediatR;
using His.Hope.Infrastructure.Audit;
using His.Hope.Contracts.Query;

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
        group.MapGet("/roles", async (
            int page = 1,
            int pageSize = 20,
            string? search = null,
            string? sort = null,
            IMediator mediator = null!,
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
            var roles = await mediator.Send(
                new GetRolesQuery(normalized.Page, normalized.PageSize, normalized.Search, normalized.Sort), ct);
            return Results.Ok(roles);
        }).RequireAuthorization("Permission:admin.roles.read");

        // GET /api/v1/auth/roles/{id} - Get role with permissions
        group.MapGet("/roles/{id:guid}", async (
            Guid id,
            IMediator mediator = null!,
            CancellationToken ct = default) =>
        {
            var role = await mediator.Send(new GetRoleByIdQuery(id), ct);
            return role is null ? Results.NotFound() : Results.Ok(role);
        }).RequireAuthorization("Permission:admin.roles.read");

        // POST /api/v1/auth/roles - Create role
        group.MapPost("/roles", async (
            CreateRoleRequest request,
            IMediator mediator = null!,
            IAuditService audit = null!,
            HttpContext http = null!,
            CancellationToken ct = default) =>
        {
            try
            {
                var role = await mediator.Send(
                    new CreateRoleCommand(request.Name, request.Description, request.Permissions), ct);
                await AdminAudit.LogAsync(audit, http, "CREATE", "Role", role.Id.ToString(), ct);
                return Results.Created($"/api/v1/auth/roles/{role.Id}", role);
            }
            catch (InvalidOperationException ex)
            {
                return Results.Problem(ex.Message, statusCode: 400);
            }
        }).RequireAuthorization("Permission:admin.roles.write");

        // PUT /api/v1/auth/roles/{id} - Update role
        group.MapPut("/roles/{id:guid}", async (
            Guid id,
            UpdateRoleRequest request,
            IMediator mediator = null!,
            IAuditService audit = null!,
            HttpContext http = null!,
            CancellationToken ct = default) =>
        {
            try
            {
                var role = await mediator.Send(
                    new UpdateRoleCommand(id, request.Name, request.Description, request.Permissions, request.ConcurrencyToken), ct);
                await AdminAudit.LogAsync(audit, http, "UPDATE", "Role", id.ToString(), ct);
                return Results.Ok(role);
            }
            catch (KeyNotFoundException)
            {
                return Results.NotFound();
            }
            catch (InvalidOperationException ex)
            {
                return Results.Problem(ex.Message, statusCode: ex.Message.StartsWith("CONCURRENCY_CONFLICT:", StringComparison.Ordinal) ? 409 : 400);
            }
        }).RequireAuthorization("Permission:admin.roles.write");

        // DELETE /api/v1/auth/roles/{id} - Delete role (only if no users assigned)
        group.MapDelete("/roles/{id:guid}", async (
            Guid id,
            IMediator mediator = null!,
            IAuditService audit = null!,
            HttpContext http = null!,
            CancellationToken ct = default) =>
        {
            try
            {
                await mediator.Send(new DeleteRoleCommand(id), ct);
                await AdminAudit.LogAsync(audit, http, "DELETE", "Role", id.ToString(), ct);
                return Results.NoContent();
            }
            catch (KeyNotFoundException)
            {
                return Results.NotFound();
            }
            catch (InvalidOperationException ex)
            {
                return Results.Problem(ex.Message, statusCode: 400);
            }
        }).RequireAuthorization("Permission:admin.roles.write");

        // GET /api/v1/auth/permissions - List all permissions
        group.MapGet("/permissions", async (
            IMediator mediator = null!,
            CancellationToken ct = default) =>
        {
            var permissions = await mediator.Send(new GetPermissionsQuery(), ct);
            return Results.Ok(permissions);
        }).RequireAuthorization("Permission:admin.permissions.read");

        return group;
    }
}
