using His.Hope.IdentityService.Application.DTOs;
using His.Hope.IdentityService.Application.UseCases.Roles.Commands;
using His.Hope.IdentityService.Application.UseCases.Roles.Queries;
using MediatR;

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
            IMediator mediator = null!,
            CancellationToken ct = default) =>
        {
            var roles = await mediator.Send(new GetRolesQuery(), ct);
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
            CancellationToken ct = default) =>
        {
            try
            {
                var role = await mediator.Send(
                    new CreateRoleCommand(request.Name, request.Description, request.Permissions), ct);
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
            CancellationToken ct = default) =>
        {
            try
            {
                var role = await mediator.Send(
                    new UpdateRoleCommand(id, request.Name, request.Description, request.Permissions), ct);
                return Results.Ok(role);
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

        // DELETE /api/v1/auth/roles/{id} - Delete role (only if no users assigned)
        group.MapDelete("/roles/{id:guid}", async (
            Guid id,
            IMediator mediator = null!,
            CancellationToken ct = default) =>
        {
            try
            {
                await mediator.Send(new DeleteRoleCommand(id), ct);
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
