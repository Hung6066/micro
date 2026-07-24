# Task 4 — Revoke-all sessions on logout

**Status:** ✅ DONE

## Files Modified
- `src/Services/IdentityService/IdentityService.Api/Program.cs` — replaced old logout handler (lines ~516-552) with new cross-port revoke-all handler

## Build Result
```
dotnet build src/Services/IdentityService/IdentityService.Api/IdentityService.Api.csproj
→ 0 Error(s), 333 Warning(s)
Time Elapsed: 00:00:14.89
```

All 333 warnings are pre-existing (gRPC generated type conflicts, nullability hints). No new warnings introduced.

## Summary
- Old handler: read single session → revoke one refresh token → delete one session → clear cookies
- New handler: reads session → extracts userId → revokes refresh token + calls `tokenBlacklist.RevokeAllUserTokensAsync(userId)` + deletes **all** Redis sessions via `sessionTracker.GetUserSessionsAsync` + clears session set → clears cookies
- Injects: `IUserSessionTracker`, `ITokenBlacklistService`, `ILogger<Program>`
- Logs cross-port logout event with session count

## Output Files
- `task-4-logout-handler.cs` — new handler code (reference copy)
- `task-4-report.md` — this file

## Git Operations
None. No commits or adds.
