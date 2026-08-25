using System.Security.Claims;

namespace His.Hope.CommerceService.Api.Security;

/// <summary>
/// Production PEP for commerce: portal_class enforcement, cross-tenant audit,
/// and operator-only route blocking for end_user tokens.
/// </summary>
internal sealed class CommerceSecurityMiddleware(
    RequestDelegate next,
    ILogger<CommerceSecurityMiddleware> logger)
{
    private const string SupportElevationHeader = "X-Support-Elevation-Id";

    public async Task InvokeAsync(HttpContext context)
    {
        if (!context.Request.Path.StartsWithSegments("/api/v1/commerce", StringComparison.OrdinalIgnoreCase))
        {
            await next(context);
            return;
        }

        if (context.User.Identity?.IsAuthenticated != true)
        {
            await next(context);
            return;
        }

        var user = context.User;
        var portalClass = CommercePortalGuard.GetPortalClass(user);
        if (string.IsNullOrWhiteSpace(portalClass))
        {
            logger.LogWarning(
                "Commerce access denied: missing portal_class (sub={Sub}, path={Path})",
                user.FindFirst("sub")?.Value,
                context.Request.Path);
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            await context.Response.WriteAsJsonAsync(new { error = "portal_class_required" });
            return;
        }

        if (CommercePortalGuard.IsEndUser(user)
            && CommercePortalGuard.IsOperatorOnlyPath(context.Request.Path, context.Request.Method))
        {
            logger.LogWarning(
                "Commerce operator route blocked for end_user (sub={Sub}, path={Path})",
                user.FindFirst("sub")?.Value,
                context.Request.Path);
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            await context.Response.WriteAsJsonAsync(new { error = "operator_route_forbidden" });
            return;
        }

        var tokenTenant = user.FindFirst("tenant_id")?.Value ?? user.FindFirst("tenant")?.Value;
        var requestedTenant = context.Request.Query["tenantKey"].FirstOrDefault()?.Trim();
        var crossTenant = !string.IsNullOrWhiteSpace(requestedTenant)
            && !string.IsNullOrWhiteSpace(tokenTenant)
            && !string.Equals(requestedTenant, tokenTenant, StringComparison.OrdinalIgnoreCase);

        if (crossTenant && CommercePortalGuard.IsEndUser(user))
        {
            logger.LogWarning(
                "Cross-tenant commerce access denied for end_user (sub={Sub}, tokenTenant={TokenTenant}, requested={Requested})",
                user.FindFirst("sub")?.Value,
                tokenTenant,
                requestedTenant);
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            await context.Response.WriteAsJsonAsync(new { error = "cross_tenant_forbidden" });
            return;
        }

        if (crossTenant
            && CommercePortalGuard.IsOperator(user)
            && IsMutating(context.Request.Method))
        {
            var elevationId = context.Request.Headers[SupportElevationHeader].FirstOrDefault();
            if (string.IsNullOrWhiteSpace(elevationId))
            {
                logger.LogWarning(
                    "ALERT cross_tenant_commerce_mutation_without_jit sub={Sub} client={ClientId} tokenTenant={TokenTenant} targetTenant={TargetTenant} method={Method} path={Path}",
                    user.FindFirst("sub")?.Value,
                    CommercePortalGuard.GetClientId(user),
                    tokenTenant,
                    requestedTenant,
                    context.Request.Method,
                    context.Request.Path);
            }
            else
            {
                logger.LogInformation(
                    "Cross-tenant commerce mutation with elevation {ElevationId} sub={Sub} targetTenant={TargetTenant} path={Path}",
                    elevationId,
                    user.FindFirst("sub")?.Value,
                    requestedTenant,
                    context.Request.Path);
            }
        }

        logger.LogInformation(
            "Commerce access client={ClientId} portal={PortalClass} tenant={TenantId} sub={Sub} {Method} {Path}",
            CommercePortalGuard.GetClientId(user),
            portalClass,
            tokenTenant,
            user.FindFirst("sub")?.Value,
            context.Request.Method,
            context.Request.Path);

        await next(context);
    }

    private static bool IsMutating(string method) =>
        HttpMethods.IsPost(method)
        || HttpMethods.IsPut(method)
        || HttpMethods.IsPatch(method)
        || HttpMethods.IsDelete(method);
}

internal static class CommerceSecurityMiddlewareExtensions
{
    public static IApplicationBuilder UseCommerceSecurity(this IApplicationBuilder app) =>
        app.UseMiddleware<CommerceSecurityMiddleware>();
}
