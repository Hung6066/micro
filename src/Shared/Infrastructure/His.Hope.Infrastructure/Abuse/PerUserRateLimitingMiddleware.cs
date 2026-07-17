using System.Collections.Concurrent;
using System.Net;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace His.Hope.Infrastructure.Abuse;

/// <summary>
/// Per-user rate limiting middleware using Redis sorted sets for sliding window.
/// Replaces the previous Security.RateLimitingMiddleware with consolidated logic.
/// Supports both IP-based and authenticated user-based rate limiting.
///
/// SECURITY: Extracts userId from JWT 'sub' claim for authenticated rate limiting.
/// Falls back to in-memory ConcurrentDictionary if Redis is unavailable.
///
/// HIPAA Context: Rate limiting helps prevent brute-force attacks on PHI access,
/// credential stuffing, and DoS attacks against healthcare APIs.
/// </summary>
public sealed class PerUserRateLimitingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<PerUserRateLimitingMiddleware> _logger;
    private readonly ConnectionMultiplexer? _redis;
    private readonly int _maxRequestsPerIp;
    private readonly int _maxRequestsPerUser;
    private readonly TimeSpan _window;
    private readonly bool _redisAvailable;

    // Fallback in-memory store in case Redis is unavailable
    private static readonly ConcurrentDictionary<string, RateLimitEntry> _fallbackStore = new();

    public PerUserRateLimitingMiddleware(
        RequestDelegate next,
        ILogger<PerUserRateLimitingMiddleware> logger,
        IConfiguration configuration)
    {
        _next = next;
        _logger = logger;

        _maxRequestsPerIp = configuration.GetValue("RateLimiting:MaxRequestsPerIp", 100);
        _maxRequestsPerUser = configuration.GetValue("RateLimiting:MaxRequestsPerUser", 200);
        _window = TimeSpan.FromMinutes(configuration.GetValue("RateLimiting:WindowMinutes", 1));

        // Try to connect to Redis; fall back to in-memory if unavailable
        try
        {
            var redisConnectionString = configuration.GetValue<string>("Redis:ConnectionString")
                ?? configuration.GetValue<string>("RateLimiting:RedisConnectionString")
                ?? "localhost:6379";
            _redis = ConnectionMultiplexer.Connect(new ConfigurationOptions
            {
                EndPoints = { redisConnectionString },
                AbortOnConnectFail = false,
                ConnectTimeout = 2000,
                SyncTimeout = 1000
            });
            _redisAvailable = _redis.IsConnected;
        }
        catch (Exception ex)
        {
            _redisAvailable = false;
            _logger.LogWarning(ex, "Redis unavailable for rate limiting - falling back to in-memory storage");
        }
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Always allow health checks to prevent rate limiting from causing cascading failures
        if (context.Request.Path.StartsWithSegments("/health"))
        {
            await _next(context);
            return;
        }

        var clientIp = GetClientIp(context);
        // SECURITY: Use JWT 'sub' claim for user-based rate limiting when authenticated
        var userId = context.User?.FindFirst("sub")?.Value;
        var ipKey = $"ratelimit:ip:{clientIp}";

        // Check IP-based limit
        if (!await IncrementAndCheckLimit(context, ipKey, _maxRequestsPerIp))
            return; // Rate limited - response already set

        // Check user-based limit (separate, higher limit for authenticated users)
        if (!string.IsNullOrEmpty(userId))
        {
            var userKey = $"ratelimit:user:{userId}";
            if (!await IncrementAndCheckLimit(context, userKey, _maxRequestsPerUser))
                return; // Rate limited - response already set
        }

        await _next(context);
    }

    /// <summary>
    /// Increments the rate counter for a given key and checks if the limit is exceeded.
    /// Returns false if the request should be blocked.
    /// </summary>
    private async Task<bool> IncrementAndCheckLimit(HttpContext context, string key, int limit)
    {
        long currentCount;

        if (_redisAvailable)
        {
            try
            {
                var db = _redis!.GetDatabase();
                var now = DateTime.UtcNow;
                var minScore = now.AddSeconds(-_window.TotalSeconds).Ticks;

                // SECURITY: Use Redis sorted set with timestamp scores for sliding window
                await db.SortedSetRemoveRangeByScoreAsync(key, 0, minScore);
                await db.SortedSetAddAsync(key, Guid.NewGuid().ToString(), now.Ticks);
                currentCount = await db.SortedSetLengthAsync(key);
                await db.KeyExpireAsync(key, _window * 2); // TTL to prevent memory leaks
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Redis rate limit operation failed for {Key}, falling back", key);
                currentCount = Interlocked.Increment(ref _fallbackCounter);
            }
        }
        else
        {
            // Fallback: use in-memory storage
            currentCount = _fallbackStore.GetOrAdd(key, _ => new RateLimitEntry(_window)).Increment();
        }

        // Set response headers regardless of limit status
        context.Response.Headers["X-RateLimit-Limit"] = limit.ToString();
        context.Response.Headers["X-RateLimit-Remaining"] = Math.Max(0, limit - currentCount).ToString();

        if (currentCount > limit)
        {
            context.Response.StatusCode = (int)HttpStatusCode.TooManyRequests;
            context.Response.Headers["Retry-After"] = _window.TotalSeconds.ToString();
            context.Response.Headers["X-RateLimit-Reset"] =
                new DateTimeOffset(DateTime.UtcNow.Add(_window)).ToUnixTimeSeconds().ToString();

            _logger.LogWarning("Rate limit exceeded for key {Key} (count: {Count}, limit: {Limit})",
                key, currentCount, limit);
            return false;
        }

        return true;
    }

    private static string GetClientIp(HttpContext context) =>
        context.Request.Headers["X-Forwarded-For"].FirstOrDefault()
        ?? context.Connection.RemoteIpAddress?.ToString()
        ?? "unknown";

    private static long _fallbackCounter;

    private sealed class RateLimitEntry
    {
        private long _count;
        private readonly TimeSpan _window;
        private DateTime _windowStart;
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
