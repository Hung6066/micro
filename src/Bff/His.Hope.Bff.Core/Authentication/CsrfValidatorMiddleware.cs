using System.Text.Json;
using His.Hope.Bff.Core.Telemetry;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace His.Hope.Bff.Core.Authentication;

public sealed class CsrfValidatorMiddleware
{
    private static readonly HashSet<string> MutationMethods = new(StringComparer.OrdinalIgnoreCase)
        { "POST", "PUT", "PATCH", "DELETE" };

    private readonly RequestDelegate _next;
    private readonly IConnectionMultiplexer _redis;
    private readonly ILogger<CsrfValidatorMiddleware> _logger;

    public CsrfValidatorMiddleware(
        RequestDelegate next,
        IConnectionMultiplexer redis,
        ILogger<CsrfValidatorMiddleware> logger)
    {
        _next = next;
        _redis = redis;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (!MutationMethods.Contains(context.Request.Method))
        {
            await _next(context);
            return;
        }

        var sessionId = context.Items["SessionId"] as string;
        if (string.IsNullOrEmpty(sessionId))
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            return;
        }

        var csrfHeader = context.Request.Headers["X-CSRF-Token"].FirstOrDefault();
        if (string.IsNullOrEmpty(csrfHeader))
        {
            _logger.LogWarning("CSRF token missing for {Method} {Path}", context.Request.Method, context.Request.Path);
            BffMetrics.CsrfFailures.Add(1);
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            return;
        }

        var db = _redis.GetDatabase();
        var sessionJson = await db.StringGetAsync($"session:{sessionId}");
        if (!sessionJson.HasValue)
        {
            BffMetrics.CsrfFailures.Add(1);
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            return;
        }

        var session = JsonSerializer.Deserialize<SessionData>(sessionJson!);
        if (session is null || !string.Equals(session.CsrfToken, csrfHeader, StringComparison.Ordinal))
        {
            _logger.LogWarning("CSRF token mismatch for session '{SessionId}'", sessionId);
            BffMetrics.CsrfFailures.Add(1);
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            return;
        }

        await _next(context);
    }
}

public static class CsrfValidatorMiddlewareExtensions
{
    public static IApplicationBuilder UseBffCsrfProtection(this IApplicationBuilder builder)
        => builder.UseMiddleware<CsrfValidatorMiddleware>();
}
