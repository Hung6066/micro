using His.Hope.IdentityService.Application.DTOs;
using His.Hope.IdentityService.Application.UseCases.Users.Commands;
using His.Hope.IdentityService.Application.UseCases.Users.Queries;
using MediatR;

namespace His.Hope.IdentityService.Api.Endpoints;

/// <summary>
/// User management endpoints for the Identity Service.
/// All endpoints require authorization.
/// </summary>
public static class UserEndpoints
{
    public static RouteGroupBuilder MapUserEndpoints(this RouteGroupBuilder group)
    {
        // GET /api/v1/auth/users - Paginated user list
        group.MapGet("/users", async (
            int page = 1,
            int pageSize = 20,
            string? search = null,
            string? role = null,
            bool? isActive = null,
            IMediator mediator = null!,
            CancellationToken ct = default) =>
        {
            var result = await mediator.Send(
                new GetUsersQuery(page, pageSize, search, role, isActive), ct);
            return Results.Ok(result);
        }).RequireAuthorization("Permission:admin.users.read");

        // GET /api/v1/auth/users/{id} - User detail
        group.MapGet("/users/{id:guid}", async (
            Guid id,
            IMediator mediator = null!,
            CancellationToken ct = default) =>
        {
            var user = await mediator.Send(new GetUserByIdQuery(id), ct);
            return user is null ? Results.NotFound() : Results.Ok(user);
        }).RequireAuthorization("Permission:admin.users.read");

        // POST /api/v1/auth/users - Create user
        group.MapPost("/users", async (
            CreateUserRequest request,
            IMediator mediator = null!,
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
                return Results.Created($"/api/v1/auth/users/{user.Id}", user);
            }
            catch (InvalidOperationException ex)
            {
                return Results.Problem(ex.Message, statusCode: 400);
            }
        }).RequireAuthorization("Permission:admin.users.write");

        // PUT /api/v1/auth/users/{id} - Update user
        group.MapPut("/users/{id:guid}", async (
            Guid id,
            UpdateUserRequest request,
            IMediator mediator = null!,
            CancellationToken ct = default) =>
        {
            try
            {
                var command = new UpdateUserCommand(
                    id, request.FirstName, request.LastName, request.Email,
                    request.PhoneNumber, request.Role, request.IsActive);

                var user = await mediator.Send(command, ct);
                return Results.Ok(user);
            }
            catch (KeyNotFoundException)
            {
                return Results.NotFound();
            }
            catch (InvalidOperationException ex)
            {
                return Results.Problem(ex.Message, statusCode: 400);
            }
        }).RequireAuthorization("Permission:admin.users.write");

        // PUT /api/v1/auth/users/{id}/deactivate - Soft-delete user
        group.MapPut("/users/{id:guid}/deactivate", async (
            Guid id,
            IMediator mediator = null!,
            CancellationToken ct = default) =>
        {
            try
            {
                await mediator.Send(new DeactivateUserCommand(id), ct);
                return Results.NoContent();
            }
            catch (KeyNotFoundException)
            {
                return Results.NotFound();
            }
        }).RequireAuthorization("Permission:admin.users.write");

        // PUT /api/v1/auth/users/{id}/activate - Reactivate user
        group.MapPut("/users/{id:guid}/activate", async (
            Guid id,
            IMediator mediator = null!,
            CancellationToken ct = default) =>
        {
            try
            {
                await mediator.Send(new ActivateUserCommand(id), ct);
                return Results.NoContent();
            }
            catch (KeyNotFoundException)
            {
                return Results.NotFound();
            }
        }).RequireAuthorization("Permission:admin.users.write");

        // PUT /api/v1/auth/users/{id}/roles - Assign roles
        group.MapPut("/users/{id:guid}/roles", async (
            Guid id,
            AssignRolesRequest request,
            IMediator mediator = null!,
            CancellationToken ct = default) =>
        {
            try
            {
                var user = await mediator.Send(
                    new AssignRolesCommand(id, request.RoleIds), ct);
                return Results.Ok(user);
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

        return group;
    }
}
