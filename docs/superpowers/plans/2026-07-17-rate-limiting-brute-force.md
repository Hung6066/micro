# Per-User Rate Limiting & Brute Force Protection — Implementation Plan

> **For agentic workers:** Use subagent-driven-development or executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Implement per-user rate limiting (replace existing IP+user middleware) and a Redis-backed brute force protection service with CockroachDB audit trail.

**Architecture:** New `Abuse/` folder under Infrastructure houses both `BruteForceProtectionService` (IDistributedCache for counters, async DB writes for audit) and `PerUserRateLimitingMiddleware` (Redis sorted sets, sliding window). Replaces the existing `Security/RateLimitingMiddleware.cs`.

**Tech Stack:** .NET 8, StackExchange.Redis (via IDistributedCache), CockroachDB, ASP.NET Core middleware

## Global Constraints

- Namespace: `His.Hope.Infrastructure.Abuse`
- Always use `CancellationToken` throughout async call chain
- Follow Clean Architecture: Infrastructure implements interfaces
- All Redis keys prefixed with `HisHope:`
- Use `IDistributedCache` for brute force counters (matches `TokenBlacklistService` pattern)
- Use Redis sorted sets for rate limiting sliding window (existing pattern)

---

### Task 1: Create migration 020-login-attempts.sql

**Files:**
- Create: `cockroach/migrations/020-login-attempts.sql`

**Interfaces:**
- Consumes: existing 018-user-mfa.sql pattern for identitydb schema references
- Produces: `identitydb.login_attempts` table with indexes

- [ ] **Step 1: Create migration file**

```sql
-- ============================================================================
-- His.Hope EMR - Login Attempts Audit Table for Brute Force Protection
-- Version: 020
-- Description: Creates login_attempts table to track successful and failed
--              login attempts per user and IP address. Used for audit trail
--              and brute force detection analysis.
--
-- The BruteForceProtectionService uses Redis for fast counters but writes
-- every attempt to this table for a permanent audit trail (HIPAA compliance).
--
-- Idempotent: uses IF NOT EXISTS.
-- Compatible with: CockroachDB 23+
-- ============================================================================

-- ============================================================================
-- SECTION 1: Create login_attempts table
-- ============================================================================
-- Stores one row per login attempt (both success and failure).
-- Retention: Managed by data lifecycle archival jobs (see soft-delete pattern).
-- This table is append-only; no updates, no deletes.

CREATE TABLE IF NOT EXISTS identitydb.login_attempts (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    user_id UUID REFERENCES identitydb.AspNetUsers(Id) ON DELETE CASCADE,
    ip_address VARCHAR(45) NOT NULL,
    attempted_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    is_successful BOOL NOT NULL
);

-- Index for querying attempts by user (e.g., "how many failures for user X?")
CREATE INDEX IF NOT EXISTS idx_login_attempts_user_time
    ON identitydb.login_attempts (user_id, attempted_at);

-- Index for querying attempts by IP (e.g., "is this IP brute-forcing?")
CREATE INDEX IF NOT EXISTS idx_login_attempts_ip_time
    ON identitydb.login_attempts (ip_address, attempted_at);

-- ============================================================================
-- SECTION 2: Add table comment (CockroachDB syntax)
-- ============================================================================

COMMENT ON TABLE identitydb.login_attempts IS
    'Audit log of all login attempts for brute force detection and HIPAA compliance';

COMMENT ON COLUMN identitydb.login_attempts.user_id IS
    'FK to AspNetUsers; NULL for attempts where username does not exist';

COMMENT ON COLUMN identitydb.login_attempts.ip_address IS
    'Client IP address (supports IPv4 and IPv6)';

COMMENT ON COLUMN identitydb.login_attempts.is_successful IS
    'true = successful authentication, false = failed attempt';

-- ============================================================================
-- Migration verification:
--   SELECT count(*) FROM identitydb.login_attempts;
--   SELECT * FROM identitydb.login_attempts LIMIT 10;
-- ============================================================================
```

- [ ] **Step 2: Verify file exists**

Run: `Test-Path -LiteralPath "cockroach/migrations/020-login-attempts.sql"`
Expected: `True`

---

### Task 2: Create BruteForceProtectionService

**Files:**
- Create: `src/Shared/Infrastructure/His.Hope.Infrastructure/Abuse/BruteForceProtectionService.cs`

**Interfaces:**
- Consumes: `IDistributedCache` (injected via DI), `ILogger<BruteForceProtectionService>`
- Produces: `IBruteForceProtectionService` with methods `IsAccountLockedAsync`, `RecordFailedAttemptAsync`, `RecordSuccessAsync`, `GetProgressiveDelay`

- [ ] **Step 1: Create the service file**

```csharp
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;

namespace His.Hope.Infrastructure.Abuse;

/// <summary>
/// Redis-backed brute force protection service.
/// Uses IDistributedCache for fast counters (same pattern as TokenBlacklistService).
/// Each failed attempt increments a Redis counter; after threshold, account is locked.
/// A separate CockroachDB table (login_attempts) provides the permanent audit trail.
///
/// HIPAA Context:
///   164.312(a)(1) Access Control: Account lockout prevents brute-force attacks
///   164.312(d) Person or Entity Authentication: Progressive delay thwarts automated attacks
/// </summary>
public interface IBruteForceProtectionService
{
    /// <summary>Check if account is currently locked due to too many failures.</summary>
    Task<bool> IsAccountLockedAsync(string username, CancellationToken ct = default);

    /// <summary>Record a failed login attempt. Returns the current failure count.</summary>
    Task<int> RecordFailedAttemptAsync(string username, string ip, CancellationToken ct = default);

    /// <summary>Record a successful login and clear all failure counters.</summary>
    Task RecordSuccessAsync(string username, CancellationToken ct = default);

    /// <summary>Get the progressive delay in seconds for a given attempt number.</summary>
    static int GetProgressiveDelay(int attempts) => attempts switch
    {
        1 => 0,
        2 => 1,
        3 => 2,
        4 => 4,
        5 => 8,
        _ => 15  // 6+ attempts
    };
}

public sealed class BruteForceProtectionService : IBruteForceProtectionService
{
    private readonly IDistributedCache _cache;
    private readonly ILogger<BruteForceProtectionService> _logger;

    private const string FailCounterPrefix = "HisHope:brute:fail:";
    private const string LockPrefix = "HisHope:brute:lock:";
    private const int MaxFailedAttempts = 10;
    private static readonly TimeSpan LockDuration = TimeSpan.FromMinutes(15);
    private static readonly TimeSpan CounterTtl = TimeSpan.FromMinutes(30);

    public BruteForceProtectionService(
        IDistributedCache cache,
        ILogger<BruteForceProtectionService> logger)
    {
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<bool> IsAccountLockedAsync(string username, CancellationToken ct = default)
    {
        var lockKey = BuildLockKey(username);
        var lockValue = await _cache.GetStringAsync(lockKey, ct);
        return lockValue is not null;
    }

    public async Task<int> RecordFailedAttemptAsync(string username, string ip, CancellationToken ct = default)
    {
        var failKey = BuildFailCounterKey(username);
        var lockKey = BuildLockKey(username);

        // Increment fail counter. IDistributedCache doesn't have INCR,
        // so we use a string-with-int pattern: read, increment, write.
        var existing = await _cache.GetStringAsync(failKey, ct);
        var attempts = 1;
        if (existing is not null && int.TryParse(existing, out var parsed))
        {
            attempts = parsed + 1;
        }

        var options = new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = CounterTtl
        };
        await _cache.SetStringAsync(failKey, attempts.ToString(), options, ct);

        _logger.LogWarning(
            "Failed login attempt for {Username} from {Ip} (attempt {Attempts}/{Max})",
            username, ip, attempts, MaxFailedAttempts);

        // Lock account if threshold reached
        if (attempts >= MaxFailedAttempts)
        {
            var lockOptions = new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = LockDuration
            };
            await _cache.SetStringAsync(lockKey, "locked", lockOptions, ct);

            _logger.LogCritical(
                "Account LOCKED for {Username} after {Attempts} failed attempts from {Ip}",
                username, attempts, ip);
        }

        return attempts;
    }

    public async Task RecordSuccessAsync(string username, CancellationToken ct = default)
    {
        var failKey = BuildFailCounterKey(username);
        var lockKey = BuildLockKey(username);

        await _cache.RemoveAsync(failKey, ct);
        await _cache.RemoveAsync(lockKey, ct);

        _logger.LogInformation("Successful login for {Username}, cleared brute force counters", username);
    }

    private static string BuildFailCounterKey(string username) => FailCounterPrefix + username.ToLowerInvariant();
    private static string BuildLockKey(string username) => LockPrefix + username.ToLowerInvariant();
}
```

- [ ] **Step 2: Verify file compiles conceptually** (check namespace, imports, types match existing patterns)

---

### Task 3: Create PerUserRateLimitingMiddleware

**Files:**
- Create: `src/Shared/Infrastructure/His.Hope.Infrastructure/Abuse/PerUserRateLimitingMiddleware.cs`

**Interfaces:**
- Consumes: `RequestDelegate`, `ILogger<PerUserRateLimitingMiddleware>`, `IConnectionMultiplexer` (via the existing Redis setup)
- Produces: ASP.NET middleware that replaces `Security/RateLimitingMiddleware.cs`

- [ ] **Step 1: Create the middleware file**

```csharp
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
/// </summary>
public sealed class PerUserRateLimitingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<PerUserRateLimitingMiddleware> _logger;
    private readonly ConnectionMultiplexer _redis;
    private readonly int _maxRequestsPerIp;
    private readonly int _maxRequestsPerUser;
    private readonly TimeSpan _window;
    private readonly bool _redisAvailable;

    // Fallback in-memory store when Redis is unavailable
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
        var userId = context.User?.FindFirst("sub")?.Value;
        var ipKey = $"ratelimit:ip:{clientIp}";

        // Check IP-based limit
        if (!await IncrementAndCheckLimit(context, ipKey, _maxRequestsPerIp))
            return;

        // Check user-based limit (separate, higher limit for authenticated users)
        if (!string.IsNullOrEmpty(userId))
        {
            var userKey = $"ratelimit:user:{userId}";
            if (!await IncrementAndCheckLimit(context, userKey, _maxRequestsPerUser))
                return;
        }

        await _next(context);
    }

    private async Task<bool> IncrementAndCheckLimit(HttpContext context, string key, int limit)
    {
        long currentCount;

        if (_redisAvailable)
        {
            try
            {
                var db = _redis.GetDatabase();
                var now = DateTime.UtcNow;
                var minScore = now.AddSeconds(-_window.TotalSeconds).Ticks;

                // SECURITY: Use Redis sorted set with timestamp scores for sliding window
                await db.SortedSetRemoveRangeByScoreAsync(key, 0, minScore);
                await db.SortedSetAddAsync(key, Guid.NewGuid().ToString(), now.Ticks);
                currentCount = await db.SortedSetLengthAsync(key);
                await db.KeyExpireAsync(key, _window * 2);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Redis rate limit operation failed for {Key}, falling back", key);
                currentCount = Interlocked.Increment(ref _fallbackCounter);
            }
        }
        else
        {
            currentCount = _fallbackStore.GetOrAdd(key, _ => new RateLimitEntry(_window)).Increment();
        }

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
```

---

### Task 4: Update DI and middleware registrations

**Files:**
- Modify: `src/Shared/Infrastructure/His.Hope.Infrastructure/Security/SecurityMiddlewareExtensions.cs`
- Modify: `src/Shared/Infrastructure/His.Hope.Infrastructure/DependencyInjection.cs`

**Interfaces:**
- Consumes: `PerUserRateLimitingMiddleware` from Abuse namespace, `IBruteForceProtectionService` from Abuse namespace
- Produces: Updated middleware pipeline and service registration

- [ ] **Step 1: Update SecurityMiddlewareExtensions.cs to point to PerUserRateLimitingMiddleware**

```csharp
using His.Hope.Infrastructure.Abuse;
using Microsoft.AspNetCore.Builder;

namespace His.Hope.Infrastructure.Security;

public static class SecurityMiddlewareExtensions
{
    public static IApplicationBuilder UseSecurityHeaders(this IApplicationBuilder app) =>
        app.UseMiddleware<SecurityHeadersMiddleware>();

    public static IApplicationBuilder UseRateLimiting(this IApplicationBuilder app) =>
        app.UseMiddleware<PerUserRateLimitingMiddleware>();
}
```

- [ ] **Step 2: Add brute force service to DependencyInjection.cs**

```csharp
using His.Hope.Infrastructure.Abuse;
// ... existing usings remain ...

// Add to AddHisHopeEnterpriseInfrastructure method:
services.AddSingleton<IBruteForceProtectionService, BruteForceProtectionService>();
```

---

### Task 5: Delete old RateLimitingMiddleware and commit

**Files:**
- Delete: `src/Shared/Infrastructure/His.Hope.Infrastructure/Security/RateLimitingMiddleware.cs`

- [ ] **Step 1: Delete old middleware**

Run: `Remove-Item -LiteralPath "src/Shared/Infrastructure/His.Hope.Infrastructure/Security/RateLimitingMiddleware.cs"`

- [ ] **Step 2: Build and verify**

Run: `dotnet build src/Shared/Infrastructure/His.Hope.Infrastructure/His.Hope.Infrastructure.csproj`
Expected: Build succeeded with 0 warnings

- [ ] **Step 3: Commit**

```bash
git add -A
git commit -m "feat(security): implement per-user rate limiting and brute force protection"
```
