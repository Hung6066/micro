using System.Security.Claims;
using His.Hope.IdentityService.Application.Conglomerate;

namespace His.Hope.IdentityService.Api.Authorization;

public static class PortalClassGuard
{
    public static IResult? EnsureOperatorPortal(ClaimsPrincipal user)
    {
        var portalClass = user.FindFirst(ConglomerateConstants.ClaimPortalClass)?.Value;
        if (string.IsNullOrWhiteSpace(portalClass) ||
            string.Equals(portalClass, ConglomerateConstants.PortalClassOperator, StringComparison.OrdinalIgnoreCase))
            return null;

        return Results.Forbid();
    }

    public static IResult? EnsureCustomerOperatorPortal(ClaimsPrincipal user)
    {
        var portalClass = user.FindFirst(ConglomerateConstants.ClaimPortalClass)?.Value;
        return string.Equals(portalClass, ConglomerateConstants.PortalClassCustomerOperator, StringComparison.OrdinalIgnoreCase)
            ? null
            : Results.Forbid();
    }

    public static IResult? EnsureCustomerOperatorAdminPath(HttpContext http)
    {
        var portalClass = http.User.FindFirst(ConglomerateConstants.ClaimPortalClass)?.Value;
        if (!string.Equals(portalClass, ConglomerateConstants.PortalClassCustomerOperator, StringComparison.OrdinalIgnoreCase))
            return null;

        var path = http.Request.Path.Value ?? string.Empty;
        var allowed =
            path.Contains("/dashboard", StringComparison.OrdinalIgnoreCase) ||
            path.Contains("/users", StringComparison.OrdinalIgnoreCase) ||
            path.Contains("/consents", StringComparison.OrdinalIgnoreCase);
        return allowed ? null : Results.Forbid();
    }

    public static IResult? EnsureNotEndUserPortal(ClaimsPrincipal user)
    {
        var portalClass = user.FindFirst(ConglomerateConstants.ClaimPortalClass)?.Value;
        return string.Equals(portalClass, ConglomerateConstants.PortalClassEndUser, StringComparison.OrdinalIgnoreCase)
            ? Results.Forbid()
            : null;
    }
}

public static class PortalEndpointExtensions
{
    public static RouteGroupBuilder RequireOperatorPortal(this RouteGroupBuilder group) =>
        group.AddEndpointFilter((context, next) =>
        {
            var error = PortalClassGuard.EnsureOperatorPortal(context.HttpContext.User);
            return error is null ? next(context) : ValueTask.FromResult<object?>(error);
        });

    public static RouteGroupBuilder RequireCustomerOperatorPortal(this RouteGroupBuilder group) =>
        group.AddEndpointFilter((context, next) =>
        {
            var error = PortalClassGuard.EnsureCustomerOperatorPortal(context.HttpContext.User);
            return error is null ? next(context) : ValueTask.FromResult<object?>(error);
        });

    public static RouteGroupBuilder BlockEndUserPortal(this RouteGroupBuilder group) =>
        group.AddEndpointFilter((context, next) =>
        {
            var error = PortalClassGuard.EnsureNotEndUserPortal(context.HttpContext.User);
            return error is null ? next(context) : ValueTask.FromResult<object?>(error);
        });

    public static RouteGroupBuilder RestrictCustomerOperatorPaths(this RouteGroupBuilder group) =>
        group.AddEndpointFilter((context, next) =>
        {
            var error = PortalClassGuard.EnsureCustomerOperatorAdminPath(context.HttpContext);
            return error is null ? next(context) : ValueTask.FromResult<object?>(error);
        });
}
