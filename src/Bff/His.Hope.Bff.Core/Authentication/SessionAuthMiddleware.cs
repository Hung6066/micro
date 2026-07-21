using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using His.Hope.Bff.Core.Telemetry;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace His.Hope.Bff.Core.Authentication;

public sealed class SessionAuthMiddleware
{
    private readonly RequestDelegate _next;
    private readonly SessionCookieOptions _options;
    private readonly IConnectionMultiplexer _redis;
    private readonly ILogger<SessionAuthMiddleware> _logger;

    public SessionAuthMiddleware(
        RequestDelegate next,
        SessionCookieOptions options,
        IConnectionMultiplexer redis,
        ILogger<SessionAuthMiddleware> logger)
    {
        _next = next;
        _options = options;
        _redis = redis;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var cookieValue = context.Request.Cookies[_options.CookieName];
        if (string.IsNullOrEmpty(cookieValue))
        {
            _logger.LogWarning("Session cookie '{CookieName}' missing", _options.CookieName);
            BffMetrics.SessionMisses.Add(1);
            BffMetrics.AuthFailures.Add(1);
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return;
        }

        var db = _redis.GetDatabase();
        var sessionJson = await db.StringGetAsync($"session:{cookieValue}");

        if (!sessionJson.HasValue)
        {
            _logger.LogWarning("Session '{SessionId}' not found in Redis", cookieValue);
            BffMetrics.SessionMisses.Add(1);
            BffMetrics.AuthFailures.Add(1);
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return;
        }

        var session = JsonSerializer.Deserialize<SessionData>(sessionJson!);

        if (session is null || session.IsExpired)
        {
            _logger.LogWarning("Session '{SessionId}' expired at {ExpiresAt}", cookieValue, session?.ExpiresAt);
            BffMetrics.SessionExpired.Add(1);
            BffMetrics.AuthFailures.Add(1);
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return;
        }

        var userAgentHash = ComputeHash(context.Request.Headers.UserAgent.ToString());
        if (!string.Equals(session.UserAgentHash, userAgentHash, StringComparison.Ordinal))
        {
            _logger.LogWarning("Session '{SessionId}' user-agent mismatch", cookieValue);
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return;
        }

        context.Items["SessionJwt"] = session.Jwt;
        context.Items["Permissions"] = session.Permissions;
        context.Items["SessionId"] = cookieValue;
        context.Items["UserId"] = session.UserId;

        BffMetrics.SessionHits.Add(1);

        await _next(context);
    }

    internal static string ComputeHash(string input)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input ?? ""));
        return Convert.ToHexString(bytes);
    }
}

public static class SessionAuthMiddlewareExtensions
{
    public static IApplicationBuilder UseBffSessionAuth(this IApplicationBuilder builder)
        => builder.UseMiddleware<SessionAuthMiddleware>();
}
