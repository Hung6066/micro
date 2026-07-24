### Task 4: Revoke-all sessions on logout

**Project:** His.Hope — Cross-Port SSO Logout

**Files:**
- Modify: `src/Services/IdentityService/IdentityService.Api/Program.cs` (logout handler)

**Context:** Replace the existing logout handler with one that revokes ALL user sessions (not just the current one). The new handler uses `IUserSessionTracker`, `ITokenBlacklistService`, and `IConnectionMultiplexer`.

**Implementation:** Locate the logout handler in Program.cs. It starts at line ~509 with `auth.MapPost("/logout"` and ends around line 545. Replace the ENTIRE handler with the code below.

**OLD handler pattern** (line ~509-545):
```
auth.MapPost("/logout", async (IConnectionMultiplexer redis, HttpContext httpContext,
    IIdentityService identityService, CancellationToken ct) =>
{ ... });
```
The old handler: reads session → gets refreshToken → revokes it → deletes session → clears cookies

**NEW handler** (complete replacement):
```csharp
auth.MapPost("/logout", async (IConnectionMultiplexer redis, HttpContext httpContext,
    IIdentityService identityService, IUserSessionTracker sessionTracker,
    ITokenBlacklistService tokenBlacklist, ILogger<Program> logger, CancellationToken ct) =>
{
    var sessionId = httpContext.Request.Cookies["hishop_sid"];
    string? refreshToken = null;
    string? userId = null;

    if (!string.IsNullOrEmpty(sessionId))
    {
        var db = redis.GetDatabase();
        var sessionJson = await db.StringGetAsync($"session:{sessionId}");
        if (sessionJson.HasValue)
        {
            var session = JsonSerializer.Deserialize<SessionData>(sessionJson!);
            if (session is not null)
            {
                refreshToken = session.RefreshToken;
                userId = session.UserId;
            }
        }
    }

    // Revoke refresh token
    if (!string.IsNullOrWhiteSpace(refreshToken))
        await identityService.LogoutAsync(refreshToken, ct);

    // Revoke ALL sessions for this user (cross-port logout)
    if (!string.IsNullOrWhiteSpace(userId))
    {
        // Blacklist all user tokens at user level (checked by JWT validation)
        await tokenBlacklist.RevokeAllUserTokensAsync(userId, ct);

        // Delete all Redis sessions for this user
        var sessions = await sessionTracker.GetUserSessionsAsync(userId);
        if (sessions.Length > 0)
        {
            var db = redis.GetDatabase();
            var keys = sessions.Select(s => (RedisKey)$"session:{s}").ToArray();
            await db.KeyDeleteAsync(keys);
        }

        // Clean up the user session set
        await sessionTracker.ClearUserSessionsAsync(userId);

        logger.LogInformation(
            "Cross-port logout: UserId={UserId}, sessions cleared={SessionCount}",
            userId, sessions.Length);
    }

    // Clear cookies
    httpContext.Response.Cookies.Append("hishop_sid", "", new CookieOptions
    {
        HttpOnly = true, Secure = httpContext.Request.IsHttps, SameSite = SameSiteMode.Lax,
        Path = "/api", Expires = DateTimeOffset.UnixEpoch
    });
    httpContext.Response.Cookies.Append("hishop_csrf", "", new CookieOptions
    {
        HttpOnly = false, Secure = httpContext.Request.IsHttps, SameSite = SameSiteMode.Strict,
        Path = "/", Expires = DateTimeOffset.UnixEpoch
    });

    return Results.NoContent();
})
.WithDeprecationNotice()
.WithOpenApi()
.RequireRateLimiting("auth")
.AllowAnonymous();
```

**Steps:**
1. Find the old logout handler in Program.cs and replace it entirely with the new code
2. Build: `dotnet build src/Services/IdentityService/IdentityService.Api/IdentityService.Api.csproj` (expected: 0 errors)
3. **DO NOT COMMIT.** Write the full new logout handler code to a separate reference file at `D:\AI\micro\.superpowers\sdd\task-4-logout-handler.cs` (just the handler code, not the entire file)
4. Write report: `D:\AI\micro\.superpowers\sdd\task-4-report.md`

**CRITICAL:** Program.cs has pre-existing uncommitted changes. Do NOT do git add or commit. Just edit the file and verify it builds. The architect will handle git staging.

**Return:** DONE (no commit hash — just report build status)