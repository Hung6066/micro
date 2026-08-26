using System.Net;
using System.Security.Claims;
using His.Hope.CommerceService.Api;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Mvc;
using StackExchange.Redis;

namespace His.Hope.CommerceService.Api.Middleware;

/// <summary>
/// Rate limits and audits commerce traffic keyed by (client_id, tenant_id).
/// Emits security alerts for cross-tenant writes attempted without JIT elevation.
/// </summary>
public sealed class CommerceSecurityMiddleware
{
    private const string ElevationHeader = "X-Support-Elevation-Id";
    private readonly RequestDelegate _next;
    private readonly ILogger<CommerceSecurityMiddleware> _logger;
    private readonly IConnectionMultiplexer? _redis;
    private readonly int _maxRequestsPerClientTenant;
    private readonly TimeSpan _window;
    private readonly bool _redisAvailable;

    public CommerceSecurityMiddleware(
        RequestDelegate next,
        ILogger<CommerceSecurityMiddleware> logger,
        IConfiguration configuration,
        IConnectionMultiplexer? redis = null)
    {
        _next = next;
        _logger = logger;
        _redis = redis;
        _maxRequestsPerClientTenant = configuration.GetValue("Commerce:RateLimit:MaxRequestsPerClientTenant", 120);
        _window = TimeSpan.FromMinutes(configuration.GetValue("Commerce:RateLimit:WindowMinutes", 1));
        _redisAvailable = redis?.IsConnected == true;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (!context.Request.Path.StartsWithSegments("/api/v1/commerce", StringComparison.OrdinalIgnoreCase))
        {
            await _next(context);
            return;
        }

        if (context.User.Identity?.IsAuthenticated != true)
        {
            await _next(context);
            return;
        }

        var clientId = ResolveClientId(context.User);
        var tenantId = context.User.GetTokenTenant() ?? "unknown";
        var rateLimitKey = $"ratelimit:commerce:{clientId}:{tenantId}";

        if (!await IncrementAndCheckLimit(context, rateLimitKey, _maxRequestsPerClientTenant))
            return;

        var resolvedTenant = CommerceHttpExtensions.ResolveCommerceTenant(context, isMutation: IsMutation(context.Request.Method));
        if (resolvedTenant is null)
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            return;
        }

        if (IsMutation(context.Request.Method) &&
            CommerceHttpExtensions.IsCrossTenant(context.User, resolvedTenant) &&
            !HasElevationHeader(context))
        {
            LogCrossTenantWriteAlert(context, clientId, tenantId, resolvedTenant);
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            context.Response.ContentType = "application/problem+json";
            var problem = new ProblemDetails
            {
                Type = "https://his-hope.com/errors/support_elevation_required",
                Title = "Support elevation is required.",
                Status = StatusCodes.Status403Forbidden,
                Detail = "Cross-tenant commerce writes require an approved support elevation.",
                Instance = context.Request.Path
            };
            problem.Extensions["errorCode"] = "support_elevation_required";
            problem.Extensions["correlationId"] = context.TraceIdentifier;
            problem.Extensions["traceId"] = context.TraceIdentifier;
            await context.Response.WriteAsJsonAsync(problem);
            return;
        }

        var started = DateTimeOffset.UtcNow;
        await _next(context);

        _logger.LogInformation(
            "Commerce audit {Method} {Path} status={StatusCode} clientId={ClientId} tenantId={TenantId} resolvedTenant={ResolvedTenant} userId={UserId} durationMs={DurationMs}",
            context.Request.Method,
            context.Request.Path.Value,
            context.Response.StatusCode,
            clientId,
            tenantId,
            resolvedTenant,
            context.User.GetUserId(),
            (DateTimeOffset.UtcNow - started).TotalMilliseconds);
    }

    private void LogCrossTenantWriteAlert(
        HttpContext context,
        string clientId,
        string sourceTenant,
        string targetTenant)
    {
        _logger.LogWarning(
            "SECURITY_ALERT commerce_cross_tenant_write_without_jit method={Method} path={Path} clientId={ClientId} sourceTenant={SourceTenant} targetTenant={TargetTenant} userId={UserId}",
            context.Request.Method,
            context.Request.Path.Value,
            clientId,
            sourceTenant,
            targetTenant,
            context.User.GetUserId());
    }

    private static bool HasElevationHeader(HttpContext context) =>
        context.Request.Headers.TryGetValue(ElevationHeader, out var values) &&
        !string.IsNullOrWhiteSpace(values.FirstOrDefault()) &&
        Guid.TryParse(values.FirstOrDefault(), out _);

    private static bool IsMutation(string method) =>
        HttpMethods.IsPost(method) ||
        HttpMethods.IsPut(method) ||
        HttpMethods.IsPatch(method) ||
        HttpMethods.IsDelete(method);

    private static string ResolveClientId(ClaimsPrincipal user) =>
        user.FindFirst("client_id")?.Value ??
        user.FindFirst("azp")?.Value ??
        "anonymous";

    private async Task<bool> IncrementAndCheckLimit(HttpContext context, string key, int limit)
    {
        long currentCount = 0;

        if (_redisAvailable)
        {
            try
            {
                var db = _redis!.GetDatabase();
                var now = DateTime.UtcNow;
                var minScore = now.AddSeconds(-_window.TotalSeconds).Ticks;
                await db.SortedSetRemoveRangeByScoreAsync(key, 0, minScore);
                await db.SortedSetAddAsync(key, Guid.NewGuid().ToString(), now.Ticks);
                currentCount = await db.SortedSetLengthAsync(key);
                await db.KeyExpireAsync(key, _window * 2);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Commerce rate limit Redis failure for {Key}", key);
                currentCount = 1;
            }
        }

        context.Response.Headers["X-RateLimit-Limit"] = limit.ToString();
        context.Response.Headers["X-RateLimit-Remaining"] = Math.Max(0, limit - currentCount).ToString();

        if (currentCount > limit)
        {
            context.Response.StatusCode = (int)HttpStatusCode.TooManyRequests;
            context.Response.Headers["Retry-After"] = _window.TotalSeconds.ToString();
            return false;
        }

        return true;
    }
}

public static class CommerceSecurityMiddlewareExtensions
{
    public static IApplicationBuilder UseCommerceSecurity(this IApplicationBuilder app) =>
        app.UseMiddleware<CommerceSecurityMiddleware>();
}
