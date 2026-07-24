### Task 1: Switch IdentityService to Redis-backed IDistributedCache

**Project:** His.Hope — Cross-Port SSO Logout
**Plan:** docs/superpowers/plans/2026-07-24-cross-port-sso-logout.md

**Files:**
- Modify: `src/Services/IdentityService/IdentityService.Api/Program.cs`

**Context:** The `TokenBlacklistService` stores blacklisted JWT token IDs in `IDistributedCache`. Currently IdentityService uses `AddDistributedMemoryCache()` (in-memory), so the blacklist is not shared across services/instances. Switching to Redis-backed `IDistributedCache` makes the blacklist globally visible.

**Interfaces:**
- Consumes: `IConnectionMultiplexer` already registered at line 60-61, Redis connection string at config keys `Redis:ConnectionString` or `ConnectionStrings:Redis`
- Produces: Redis-backed `IDistributedCache` used by `TokenBlacklistService` and `RedisRefreshTokenStore`

**NuGet:** `Microsoft.Extensions.Caching.StackExchangeRedis` version 8.0.4 is already referenced by `His.Hope.Infrastructure.csproj` (a project dependency). No new package needed.

**The key line MUST match this exactly:**

Old (lines 67-69):
```csharp
// Use in-memory distributed cache for token blacklist + refresh token storage in this service.
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSingleton<ICacheService, NoOpCacheService>();
```

New:
```csharp
// Use Redis distributed cache for token blacklist + refresh token storage (shared across services).
builder.Services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = builder.Configuration.GetConnectionString("Redis")
        ?? builder.Configuration.GetValue<string>("Redis:ConnectionString")
        ?? "localhost:6379";
    options.InstanceName = "HisHope:";
});
builder.Services.AddSingleton<ICacheService, NoOpCacheService>();
```

**Steps:**
1. Replace the old lines with new lines in Program.cs
2. Build: `dotnet build src/Services/IdentityService/IdentityService.Api/IdentityService.Api.csproj` (expected: success)
3. Commit with message: `"feat: switch IdentityService to Redis-backed IDistributedCache for shared token blacklist"`

**Important:** Only touch `Program.cs`. Do NOT modify any other files. The existing modified files in the workspace are pre-existing work-in-progress — do not stage or commit them.

**Report file:** `.superpowers/sdd/task-1-report.md`
Write your report to this file with: status, build output, commit hash, any concerns.

**Return to controller:** Just return "DONE" (or "DONE_WITH_CONCERNS" / "BLOCKED") plus the commit hash and any concerns in 1-2 lines.
