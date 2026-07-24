# Task 5 Report: BroadcastChannel API for Cross-Tab SSO Logout

**Status:** ✅ DONE

**Commit:** `e4d4412`

**File modified:** `src/Frontend/his-hope-app/src/app/core/services/auth.service.ts`

## Changes Applied

1. Added `Router` import
2. Added `private router = inject(Router)` and `private static readonly AUTH_CHANNEL = 'hishop_auth'` class fields
3. Added `initBroadcastChannel()` call in constructor
4. Modified `logout()` to broadcast before calling API
5. Modified `oidcLogout()` to broadcast before calling API
6. Added `initBroadcastChannel()` and `broadcastLogout()` private methods

## Verification

- Production build: ✅ (14.378 seconds, no errors)
- Staged only `auth.service.ts`: ✅
- Committed with message: `feat: add BroadcastChannel API for cross-tab SSO logout notification`
