# Cross-Port SSO Logout — Design Spec

**Date:** 2026-07-24
**Status:** Approved
**Approach:** Redis TokenBlacklist + Revoke-All + BroadcastChannel API

## Problem

His.Hope runs multiple Angular SPAs on different ports (8081, 8082, 8083), all behind Duende BFF proxies sharing a common OpenIddict identity server. Each SPA caches JWT tokens in `localStorage` and maintains its own Redis-backed BFF session (`hishop_sid` cookie).

When a user logs out from one port:
1. Only that port's Redis session is deleted
2. That port's cookies are cleared
3. **Other ports' cached JWTs remain valid** — authenticated API calls still succeed

The root cause: `TokenBlacklistService` uses `AddDistributedMemoryCache()` (in-memory), so blacklisted tokens are invisible to other services.

## Solution Overview

Three independent layers, each building on the last:

1. **Redis-backed TokenBlacklist** — shared blacklist across all services
2. **User Session Set + Revoke-All** — bulk revoke all tokens on logout
3. **BroadcastChannel API** — zero-cost cross-tab notification

---

## Section 1: Redis-backed TokenBlacklist

**Files changed:**
- `src/Services/IdentityService/IdentityService.Api/Program.cs`

**Current:**
```csharp
builder.Services.AddDistributedMemoryCache();
```

**New:**
```csharp
builder.Services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = builder.Configuration.GetConnectionString("Redis");
    options.InstanceName = "HisHope:";
});
```

**Mechanism:**
- `TokenBlacklistService` stores blacklisted tokens by `jti` (JWT ID) in `IDistributedCache`
- With `AddStackExchangeRedisCache`, that cache is Redis — not in-memory
- Every BFF and API service already runs `OnTokenValidated` which checks `ITokenBlacklistService`

**Invariant:** No code change to `TokenBlacklistService` or any consumer. Only the `IDistributedCache` backing store changes.

---

## Section 2: User Session Set + Revoke-All

**Files changed:**
- `src/Services/IdentityService/IdentityService.Api/Program.cs` (logout handler)
- `src/Services/IdentityService/IdentityService.Infrastructure/Services/` (new `UserSessionTracker.cs`)

### Redis data structure

```
Key: HisHope:user_sessions:{userId}
Type: Redis SET
Members: {sessionId1, sessionId2, ...}
TTL: 7 days (sliding — refreshed on each successful token validation)
```

### New component: `UserSessionTracker`

```csharp
public class UserSessionTracker
{
    // Called after login/token issuance
    Task AddSessionAsync(string userId, string sessionId);

    // Called on logout — returns all session IDs for bulk cleanup
    Task<string[]> GetUserSessionsAsync(string userId);

    // Called after revoke-all — cleans up the set
    Task ClearUserSessionsAsync(string userId);
}
```

### Modified logout flow

1. Read `hishop_sid` cookie → get `sessionId` (existing)
2. Delete Redis `session:{sessionId}` (existing)
3. Clear `hishop_sid` and `hishop_csrf` cookies (existing)
4. **NEW:** Call `UserSessionTracker.GetUserSessionsAsync(userId)` → get all session IDs
5. **NEW:** For each session, extract JWT `jti` → call `TokenBlacklistService.BlacklistTokenAsync(jti, expiry)`
6. **NEW:** Delete each session `session:{sessionId}` from Redis
7. **NEW:** Call `UserSessionTracker.ClearUserSessionsAsync(userId)`

### Session tracking integration

Session `SADD` happens in `BffProxyConfigExtensions.OidcSetup.cs` `OnTokenValidated` event — where the Redis session is already created. Add the `SADD` call there.

---

## Section 3: BroadcastChannel API (Angular Client)

**Files changed:**
- `src/Frontend/his-hope-app/src/app/core/services/auth.service.ts`

### Channel protocol

```typescript
const CHANNEL_NAME = 'hishop_auth';

interface AuthChannelMessage {
  type: 'LOGOUT' | 'SESSION_EXPIRED';
}
```

### On logout (active tab)

```typescript
logout() {
  // ... existing logout logic (POST /api/v1/auth/logout) ...
  // Broadcast to other tabs
  const channel = new BroadcastChannel(CHANNEL_NAME);
  channel.postMessage({ type: 'LOGOUT' });
  channel.close();
}
```

### On init (passive tabs)

```typescript
// In constructor or ngOnInit of AuthService
const channel = new BroadcastChannel(CHANNEL_NAME);
channel.onmessage = (event: MessageEvent<AuthChannelMessage>) => {
  if (event.data.type === 'LOGOUT') {
    this.clearAuthState();
    this.router.navigate(['/auth/login']);
  }
};
```

### Angular HTTP interceptor (existing, verify)

The existing 401 interceptor already handles token rejection → redirect to login. No change needed — but verify it:
1. On 401 → clear `localStorage` → navigate to `/auth/login`
2. Skip redirect if already on `/auth/login` (prevent loops)
3. On BroadcastChannel `LOGOUT` → same handler

---

## Security Considerations

| Concern | Mitigation |
|---|---|
| BroadcastChannel is same-origin only | Each SPA on different port = different origin → BroadcastChannel works across same-browser tabs but NOT across browsers/devices |
| Token replay window | JWTs checked against Redis blacklist on every API call; max window = token lifetime |
| Redis data leakage | Blacklist entries use `HisHope:` prefix; Redis in isolated Docker network |
| Race condition: token validated before blacklist propagated | Redis single-instance = no propagation delay |

## Non-goals

- Cross-device logout (requires different mechanism — push notification)
- WebSocket/SSE infrastructure (over-engineered for current scale)
- Full session management UI ("sessions" page, device management)

## Testing Strategy

| Layer | Test | Tool |
|---|---|---|
| Unit | `UserSessionTracker` add/get/clear | xUnit |
| Integration | Logout → blacklist → API call with old token → 401 | Testcontainers + Redis |
| E2E | Login port A → open port B → logout A → verify B redirects | Playwright |
| E2E | Same-browser cross-tab BroadcastChannel | Playwright (browser context) |

## Rollout Plan

1. **Phase 1:** Redis `IDistributedCache` switch — change one line, deploy
2. **Phase 2:** User session tracking + revoke-all — add `UserSessionTracker`, modify logout
3. **Phase 3:** BroadcastChannel API — Angular client changes only
