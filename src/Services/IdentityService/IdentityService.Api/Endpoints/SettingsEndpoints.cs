using His.Hope.IdentityService.Application.DTOs;
using His.Hope.IdentityService.Application.UseCases.Settings.Commands;
using His.Hope.IdentityService.Application.UseCases.Settings.Queries;
using MediatR;
using System.Security.Claims;

namespace His.Hope.IdentityService.Api.Endpoints;

/// <summary>
/// System settings endpoints for hospital-wide configuration.
/// All endpoints require authorization.
/// </summary>
public static class SettingsEndpoints
{
    public static RouteGroupBuilder MapSettingsEndpoints(this RouteGroupBuilder group)
    {
        // GET /api/v1/settings - All settings (key-value)
        group.MapGet("/settings", async (
            IMediator mediator = null!,
            CancellationToken ct = default) =>
        {
            var settings = await mediator.Send(new GetSettingsQuery(), ct);
            return Results.Ok(settings);
        }).RequireAuthorization("Permission:admin.settings.read");

        // GET /api/v1/settings/{key} - Get single setting
        group.MapGet("/settings/{key}", async (
            string key,
            IMediator mediator = null!,
            CancellationToken ct = default) =>
        {
            var setting = await mediator.Send(new GetSettingByKeyQuery(key), ct);
            return setting is null ? Results.NotFound() : Results.Ok(setting);
        }).RequireAuthorization("Permission:admin.settings.read");

        // PUT /api/v1/settings/{key} - Update single setting
        group.MapPut("/settings/{key}", async (
            string key,
            UpdateSettingRequest request,
            HttpContext httpContext,
            IMediator mediator = null!,
            CancellationToken ct = default) =>
        {
            var userId = httpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                ?? httpContext.User.FindFirst("sub")?.Value;

            var setting = await mediator.Send(
                new UpdateSettingCommand(key, request.Value, request.Description, userId), ct);
            return Results.Ok(setting);
        }).RequireAuthorization("Permission:admin.settings.write");

        // PUT /api/v1/settings - Bulk update settings
        group.MapPut("/settings", async (
            List<BulkUpdateSettingItem> request,
            HttpContext httpContext,
            IMediator mediator = null!,
            CancellationToken ct = default) =>
        {
            var userId = httpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                ?? httpContext.User.FindFirst("sub")?.Value;

            var settings = await mediator.Send(
                new BulkUpdateSettingsCommand(request, userId), ct);
            return Results.Ok(settings);
        }).RequireAuthorization("Permission:admin.settings.write");

        return group;
    }
}
