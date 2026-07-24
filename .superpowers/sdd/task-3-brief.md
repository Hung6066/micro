### Task 3: Track sessions in BFF OidcSetup

**Project:** His.Hope — Cross-Port SSO Logout
**Plan:** docs/superpowers/plans/2026-07-24-cross-port-sso-logout.md

**Files:**
- Modify: `src/Bff/His.Hope.Bff.Core/Authentication/OidcSetup.cs`

**Context:** We need to track all BFF sessions per user so that cross-port logout can revoke them all. In `OnTokenValidated` event handler (line 63-124), after the session is created in Redis, we need to also add the session ID to a Redis SET keyed by user ID.

**The change:** In OidcSetup.cs, locate the `OnTokenValidated` event handler. After the existing `db.StringSetAsync()` call on line 101-104, add these 3 lines BEFORE the cookie append on line 106:

Add right after line 104 (`TimeSpan.FromHours(1));`) and before line 106 (`ctx.Response.Cookies.Append(SessionCookieName...`):

```csharp
                    // Track this session in the user's session set for cross-port logout
                    var userSessionsKey = $"HisHope:user_sessions:{subjectId}";
                    await db.SetAddAsync(userSessionsKey, sessionId);
                    await db.KeyExpireAsync(userSessionsKey, TimeSpan.FromDays(7));
```

The surrounding code should look like this (for context):
```csharp
                    await db.StringSetAsync(
                        $"session:{sessionId}",
                        sessionJson,
                        TimeSpan.FromHours(1));

                    // Track this session in the user's session set for cross-port logout
                    var userSessionsKey = $"HisHope:user_sessions:{subjectId}";
                    await db.SetAddAsync(userSessionsKey, sessionId);
                    await db.KeyExpireAsync(userSessionsKey, TimeSpan.FromDays(7));

                    ctx.Response.Cookies.Append(SessionCookieName, sessionId, new CookieOptions
```

**Steps:**
1. Edit OidcSetup.cs — insert the 3 lines as shown above
2. Build: `dotnet build src/Bff/His.Hope.Bff.Core/His.Hope.Bff.Core.csproj` (expected: success)
3. Stage ONLY OidcSetup.cs: `git add src/Bff/His.Hope.Bff.Core/Authentication/OidcSetup.cs`
4. Commit: `git commit -m "feat: track active sessions in Redis set for cross-port logout"`
5. Write report to: `D:\AI\micro\.superpowers\sdd\task-3-report.md`

**CRITICAL:** Do NOT use `git add .` or `git add -A`. Do NOT stage or commit any files except the ONE file listed above. Use `git add` with the exact file path only.

**Return:** DONE + commit hash in 1-2 lines.