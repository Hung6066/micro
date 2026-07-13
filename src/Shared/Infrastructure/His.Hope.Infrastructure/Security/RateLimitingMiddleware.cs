using System.Collections.Concurrent;
using System.Net;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace His.Hope.Infrastructure.Security;

public class RateLimitingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RateLimitingMiddleware> _logger;
    private readonly ConcurrentDictionary<string, RateLimitEntry> _clients = new();
    private readonly int _maxRequests = 100;
    private readonly TimeSpan _window = TimeSpan.FromMinutes(1);

    public RateLimitingMiddleware(
        RequestDelegate next,
        ILogger<RateLimitingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (context.Request.Path.StartsWithSegments("/health"))
        {
            await _next(context);
            return;
        }

        var clientIp = GetClientIp(context);
        var entry = _clients.GetOrAdd(clientIp, _ => new RateLimitEntry());

        lock (entry)
        {
            if (entry.ExpiresAt < DateTime.UtcNow)
            {
                entry.Count = 0;
                entry.ExpiresAt = DateTime.UtcNow.Add(_window);
            }

            entry.Count++;

            if (entry.Count > _maxRequests)
            {
                context.Response.StatusCode = (int)HttpStatusCode.TooManyRequests;
                context.Response.Headers["Retry-After"] = _window.TotalSeconds.ToString();
                context.Response.Headers["X-RateLimit-Limit"] = _maxRequests.ToString();
                context.Response.Headers["X-RateLimit-Remaining"] = "0";
                context.Response.Headers["X-RateLimit-Reset"] =
                    new DateTimeOffset(entry.ExpiresAt).ToUnixTimeSeconds().ToString();

                _logger.LogWarning("Rate limit exceeded for {ClientIp}", clientIp);
                return;
            }

            context.Response.Headers["X-RateLimit-Limit"] = _maxRequests.ToString();
            context.Response.Headers["X-RateLimit-Remaining"] =
                (_maxRequests - entry.Count).ToString();
            context.Response.Headers["X-RateLimit-Reset"] =
                new DateTimeOffset(entry.ExpiresAt).ToUnixTimeSeconds().ToString();
        }

        await _next(context);
    }

    private static string GetClientIp(HttpContext context) =>
        context.Request.Headers["X-Forwarded-For"].FirstOrDefault()
        ?? context.Connection.RemoteIpAddress?.ToString()
        ?? "unknown";

    private class RateLimitEntry
    {
        public int Count { get; set; }
        public DateTime ExpiresAt { get; set; } = DateTime.UtcNow.AddMinutes(1);
    }
}
