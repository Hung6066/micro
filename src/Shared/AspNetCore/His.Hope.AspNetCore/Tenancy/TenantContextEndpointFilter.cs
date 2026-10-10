using System.Diagnostics.Metrics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace His.Hope.AspNetCore.Tenancy;

/// <summary>
/// Enforces the canonical request tenant context at the endpoint boundary.
/// Query/body tenant selectors remain a measured compatibility path while the
/// client migration is in progress; handlers must use <see cref="IHisHopeTenantContext"/>.
/// </summary>
public sealed class TenantContextEndpointFilter(
    IConfiguration configuration,
    ILogger<TenantContextEndpointFilter> logger) : IEndpointFilter
{
    private const string LegacySelectorEnabledKey = "TenantContext:LegacySelectorEnabled";

    public async ValueTask<object?> InvokeAsync(
        EndpointFilterInvocationContext invocationContext,
        EndpointFilterDelegate next)
    {
        var http = invocationContext.HttpContext;
        var tenantContext = http.RequestServices.GetService<IHisHopeTenantContext>();
        if (tenantContext is null || !tenantContext.HasTenant)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status403Forbidden,
                title: "A tenant context is required.",
                extensions: new Dictionary<string, object?> { ["errorCode"] = "tenant_context_required" });
        }

        var legacySelector = ResolveLegacySelector(http);
        if (legacySelector is not null)
        {
            TenantContextTelemetry.LegacySelectorUsage.Add(
                1,
                new KeyValuePair<string, object?>("service", configuration["ServiceName"] ?? "unknown"),
                new KeyValuePair<string, object?>("path", http.Request.Path.Value ?? "unknown"),
                new KeyValuePair<string, object?>("selector", legacySelector));
            logger.LogWarning(
                "Legacy tenant selector used on {Method} {Path}; migrate to X-HisHope-Tenant",
                http.Request.Method,
                http.Request.Path);

            if (!configuration.GetValue(LegacySelectorEnabledKey, true))
            {
                return Results.Problem(
                    statusCode: StatusCodes.Status410Gone,
                    title: "The legacy tenant selector is no longer supported.",
                    extensions: new Dictionary<string, object?> { ["errorCode"] = "legacy_tenant_selector_disabled" });
            }

            http.Response.Headers["Deprecation"] = "true";
            http.Response.Headers["Sunset"] = configuration["TenantContext:LegacySelectorSunset"] ?? "planned";
            http.Response.Headers["X-HisHope-Tenant-Mode"] = "legacy-compatibility";
        }
        else
        {
            http.Response.Headers["X-HisHope-Tenant-Mode"] = "context";
        }

        return await next(invocationContext);
    }

    private static string? ResolveLegacySelector(HttpContext http)
    {
        if (http.Request.Query.ContainsKey("tenantKey"))
            return "query";
        if (http.Items.TryGetValue(TenantContextTelemetry.LegacyBodySelectorItemKey, out var body) && body is true)
            return "body";
        return null;
    }
}

public static class TenantContextEndpointExtensions
{
    public static RouteGroupBuilder RequireTenantContext(this RouteGroupBuilder group) =>
        group.AddEndpointFilter<TenantContextEndpointFilter>();
}

public static class TenantContextTelemetry
{
    public const string LegacyBodySelectorItemKey = "HisHope.TenantContext.LegacyBodySelector";
    private static readonly Meter Meter = new("His.Hope.AspNetCore.Tenancy", "1.0.0");
    internal static readonly Counter<long> LegacySelectorUsage =
        Meter.CreateCounter<long>("tenant.legacy_selector.usage", description: "Legacy tenant selector requests.");
}
