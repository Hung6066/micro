using System.Collections.Concurrent;
using System.Net;
using System.Security.Claims;
using StackExchange.Redis;

namespace His.Hope.CommerceService.Api.Security;

/// <summary>
/// Rate limits commerce API by (client_id, tenant_id) partition after authentication.
/// </summary>
internal sealed class CommerceRateLimitMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<CommerceRateLimitMiddleware> _logger;
    private readonly IConnectionMultiplexer? _redis;
    private readonly int _endUserLimit;
    private readonly int _operatorLimit;
    private readonly TimeSpan _window;
    private readonly bool _redisAvailable;
    private static readonly ConcurrentDictionary<string, RateLimitEntry> FallbackStore = new();

    public CommerceRateLimitMiddleware(
        RequestDelegate next,
        ILogger<CommerceRateLimitMiddleware> logger,
        IConfiguration configuration,
        IConnectionMultiplexer? redis = null)
    {
        _next = next;
        _logger = logger;
        _redis = redis;
        _endUserLimit = configuration.GetValue("RateLimiting:Commerce:EndUserPermitLimit", 120);
        _operatorLimit = configuration.GetValue("RateLimiting:Commerce:OperatorPermitLimit", 300);
        _window = TimeSpan.FromMinutes(configuration.GetValue("RateLimiting:Commerce:WindowMinutes", 1));
        _redisAvailable = redis?.IsConnected == true;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (!context.Request.Path.StartsWithSegments("/api/v1/commerce", StringComparison.OrdinalIgnoreCase)
            || context.User.Identity?.IsAuthenticated != true)
        {
            await _next(context);
            return;
        }

        var clientId = CommercePortalGuard.GetClientId(context.User) ?? "unknown-client";
        var tenantId = context.User.FindFirst("tenant_id")?.Value
            ?? context.User.FindFirst("tenant")?.Value
            ?? "unknown-tenant";
        var limit = CommercePortalGuard.IsOperator(context.User) ? _operatorLimit : _endUserLimit;
        var key = $"ratelimit:commerce:{clientId}:{tenantId}";

        if (!await IncrementAndCheckLimitAsync(context, key, limit))
            return;

        await _next(context);
    }

    private async Task<bool> IncrementAndCheckLimitAsync(HttpContext context, string key, int limit)
    {
        long currentCount;

        if (_redisAvailable)
        {
            try
            {
                var db = _redis!.GetDatabase();
                var now = DateTime.UtcNow;
                var minScore = now.Subtract(_window).Ticks;
                await db.SortedSetRemoveRangeByScoreAsync(key, 0, minScore);
                await db.SortedSetAddAsync(key, Guid.NewGuid().ToString(), now.Ticks);
                currentCount = await db.SortedSetLengthAsync(key);
                await db.KeyExpireAsync(key, _window * 2);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Commerce rate limit Redis failed for {Key}", key);
                currentCount = FallbackStore.GetOrAdd(key, _ => new RateLimitEntry(_window)).Increment();
            }
        }
        else
        {
            currentCount = FallbackStore.GetOrAdd(key, _ => new RateLimitEntry(_window)).Increment();
        }

        context.Response.Headers["X-RateLimit-Limit"] = limit.ToString();
        context.Response.Headers["X-RateLimit-Remaining"] = Math.Max(0, limit - currentCount).ToString();

        if (currentCount > limit)
        {
            context.Response.StatusCode = (int)HttpStatusCode.TooManyRequests;
            context.Response.Headers["Retry-After"] = ((int)_window.TotalSeconds).ToString();
            _logger.LogWarning(
                "Commerce rate limit exceeded key={Key} count={Count} limit={Limit}",
                key,
                currentCount,
                limit);
            return false;
        }

        return true;
    }

    private sealed class RateLimitEntry
    {
        private long _count;
        private DateTime _windowStart;
        private readonly TimeSpan _window;
        private readonly object _lock = new();

        public RateLimitEntry(TimeSpan window)
        {
            _window = window;
            _windowStart = DateTime.UtcNow;
        }

        public long Increment()
        {
            lock (_lock)
            {
                if (DateTime.UtcNow - _windowStart > _window)
                {
                    _count = 0;
                    _windowStart = DateTime.UtcNow;
                }

                return ++_count;
            }
        }
    }
}

internal static class CommerceRateLimitMiddlewareExtensions
{
    public static IApplicationBuilder UseCommerceRateLimiting(this IApplicationBuilder app) =>
        app.UseMiddleware<CommerceRateLimitMiddleware>();
}
