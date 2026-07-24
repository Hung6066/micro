# Task 3 Report: Track sessions in BFF OidcSetup

**Status:** ✅ Complete

## Summary
Added session tracking to the `OnTokenValidated` event handler in OidcSetup.cs. After the existing `StringSetAsync` call, three lines were inserted to:

1. Build a Redis SET key (`HisHope:user_sessions:{subjectId}`)
2. Add the session ID to that SET via `SetAddAsync`
3. Set a 7-day TTL on the SET via `KeyExpireAsync`

## Verification
- **Build:** `dotnet build src/Bff/His.Hope.Bff.Core/His.Hope.Bff.Core.csproj` — succeeded (0 warnings, 0 errors)
- **Staged files:** Only `src/Bff/His.Hope.Bff.Core/Authentication/OidcSetup.cs`
- **Commit:** `2c3b696` — `feat: track active sessions in Redis set for cross-port logout`
- **Diff:** 1 file changed, 5 insertions
