### Task 2: Create UserSessionTracker

**Project:** His.Hope — Cross-Port SSO Logout
**Plan:** docs/superpowers/plans/2026-07-24-cross-port-sso-logout.md

**Files:**
- Create: `src/Services/IdentityService/IdentityService.Infrastructure/Services/UserSessionTracker.cs`
- Modify: `src/Services/IdentityService/IdentityService.Api/Program.cs` (register DI)
- Test: `tests/Services/IdentityService/IdentityService.Infrastructure.Tests/UserSessionTrackerTests.cs` (optional but recommended)

**Context:** We need to track all active BFF sessions per user so that when a user logs out from one port, we can find and revoke all their sessions. This component maintains a Redis SET `HisHope:user_sessions:{userId}` with session IDs as members.

**Important context:** Program.cs currently has pre-existing modifications (work in progress from other features). Only add the DI registration line — do not touch anything else in Program.cs. Specifically, add the registration after the `NoOpLockManager` registration around line 73-79.

**Interface:**
```csharp
namespace His.Hope.IdentityService.Infrastructure.Services;

public interface IUserSessionTracker
{
    Task AddSessionAsync(string userId, string sessionId);
    Task<string[]> GetUserSessionsAsync(string userId);
    Task ClearUserSessionsAsync(string userId);
}
```

**Implementation:**
```csharp
using StackExchange.Redis;

namespace His.Hope.IdentityService.Infrastructure.Services;

public sealed class UserSessionTracker : IUserSessionTracker
{
    private readonly IDatabase _db;
    private const string UserSessionsPrefix = "HisHope:user_sessions:";

    public UserSessionTracker(IConnectionMultiplexer redis)
    {
        _db = redis.GetDatabase();
    }

    public async Task AddSessionAsync(string userId, string sessionId)
    {
        var key = UserSessionsPrefix + userId;
        await _db.SetAddAsync(key, sessionId);
        // Expire in 7 days so stale entries don't accumulate
        await _db.KeyExpireAsync(key, TimeSpan.FromDays(7));
    }

    public async Task<string[]> GetUserSessionsAsync(string userId)
    {
        var key = UserSessionsPrefix + userId;
        var members = await _db.SetMembersAsync(key);
        return members.Select(m => m.ToString()).ToArray();
    }

    public async Task ClearUserSessionsAsync(string userId)
    {
        var key = UserSessionsPrefix + userId;
        await _db.KeyDeleteAsync(key);
    }
}
```

**DI Registration** (add in Program.cs after the `NoOpLockManager` line):
```csharp
builder.Services.AddSingleton<IUserSessionTracker, UserSessionTracker>();
```

The needed `using` for `His.Hope.IdentityService.Infrastructure.Services` is already present at the top of Program.cs (line ~20).

**Steps:**
1. Create UserSessionTracker.cs with interface + implementation
2. Add DI registration to Program.cs (only this one line)
3. Build: `dotnet build src/Services/IdentityService/IdentityService.Api/IdentityService.Api.csproj` (expected: success)
4. Optionally create tests in `tests/Services/IdentityService/IdentityService.Infrastructure.Tests/`
5. Commit: `git add src/Services/IdentityService/IdentityService.Infrastructure/Services/UserSessionTracker.cs src/Services/IdentityService/IdentityService.Api/Program.cs` then commit with message: `"feat: add UserSessionTracker for tracking active BFF sessions per user"`

**IMPORTANT:** There are many other modified files in the workspace (pre-existing work). Do NOT stage or commit any files except the ones listed above. Specifically for Program.cs, use `git add -p src/Services/IdentityService/IdentityService.Api/Program.cs` and select ONLY the new DI registration line when prompted. Do NOT stage any pre-existing unrelated changes in Program.cs.

**Report file:** `.superpowers/sdd/task-2-report.md`
Write your report to this file with: status, build output, commit hash, any concerns.

**Return to controller:** Just return "DONE" (or "DONE_WITH_CONCERNS" / "BLOCKED") plus the commit hash and any concerns in 1-2 lines.
