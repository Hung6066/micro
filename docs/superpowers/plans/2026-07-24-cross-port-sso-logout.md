# Cross-Port SSO Logout Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Fix SSO logout across multiple Angular SPAs (ports 8081, 8082, 8083) so logging out on one port invalidates all sessions immediately.

**Architecture:** Three layers: (1) Redis-backed `IDistributedCache` makes `TokenBlacklistService` shared across all services; (2) `UserSessionTracker` tracks all BFF sessions per user for bulk revocation on logout; (3) BroadcastChannel API notifies same-browser tabs with zero server cost.

**Tech Stack:** .NET 8, OpenIddict, StackExchange.Redis, Angular 17, BroadcastChannel API

## Global Constraints

- No new NuGet packages — `Microsoft.Extensions.Caching.StackExchangeRedis` already referenced via `His.Hope.Infrastructure`
- `IConnectionMultiplexer` already registered in IdentityService — reuse it
- All Redis keys use `HisHope:` prefix convention
- JWT token lifetime unchanged (1 hour)
- Angular changes must not add new npm dependencies

---

### Task 1: Switch IdentityService to Redis-backed IDistributedCache

**Files:**
- Modify: `src/Services/IdentityService/IdentityService.Api/Program.cs`

**Interfaces:**
- Consumes: `IConnectionMultiplexer` (already registered at line 60-61)
- Produces: Redis-backed `IDistributedCache` that `TokenBlacklistService` and `RedisRefreshTokenStore` use

- [ ] **Step 1: Replace `AddDistributedMemoryCache` with `AddStackExchangeRedisCache`**

  In `Program.cs` line 67-69, replace:
  ```csharp
  // Use in-memory distributed cache for token blacklist + refresh token storage in this service.
  builder.Services.AddDistributedMemoryCache();
  builder.Services.AddSingleton<ICacheService, NoOpCacheService>();
  ```
  with:
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

- [ ] **Step 2: Verify build**

  Run: `dotnet build src/Services/IdentityService/IdentityService.Api/IdentityService.Api.csproj`
  Expected: Build succeeds

- [ ] **Step 3: Commit**

  ```bash
  git add src/Services/IdentityService/IdentityService.Api/Program.cs
  git commit -m "feat: switch IdentityService to Redis-backed IDistributedCache for shared token blacklist"
  ```

---

### Task 2: Create UserSessionTracker

**Files:**
- Create: `src/Services/IdentityService/IdentityService.Infrastructure/Services/UserSessionTracker.cs`
- Modify: `src/Services/IdentityService/IdentityService.Api/Program.cs` (register in DI)
- Create: `tests/Services/IdentityService/IdentityService.Infrastructure.Tests/UserSessionTrackerTests.cs` (optional, if test project has Redis Testcontainer setup)

**Interfaces:**
- Consumes: `IConnectionMultiplexer` (injected)
- Produces: `IUserSessionTracker` interface with methods used by logout handler

- [ ] **Step 1: Create IUserSessionTracker interface + implementation**

  Create `src/Services/IdentityService/IdentityService.Infrastructure/Services/UserSessionTracker.cs`:

  ```csharp
  using StackExchange.Redis;

  namespace His.Hope.IdentityService.Infrastructure.Services;

  public interface IUserSessionTracker
  {
      Task AddSessionAsync(string userId, string sessionId);
      Task<string[]> GetUserSessionsAsync(string userId);
      Task ClearUserSessionsAsync(string userId);
  }

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
          // Expire the set in 7 days (sliding) so stale entries don't accumulate
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

- [ ] **Step 2: Register UserSessionTracker in DI**

  In `Program.cs`, add after line 73 (after `NoOpLockManager`):
  ```csharp
  builder.Services.AddSingleton<IUserSessionTracker, UserSessionTracker>();
  ```

  Add import at top of Program.cs if not present:
  ```csharp
  using His.Hope.IdentityService.Infrastructure.Services;
  ```
  (Already present at line 20: `using His.Hope.IdentityService.Infrastructure.Services;`)

- [ ] **Step 3: Verify build**

  Run: `dotnet build src/Services/IdentityService/IdentityService.Api/IdentityService.Api.csproj`
  Expected: Build succeeds

- [ ] **Step 4: Commit**

  ```bash
  git add src/Services/IdentityService/IdentityService.Infrastructure/Services/UserSessionTracker.cs src/Services/IdentityService/IdentityService.Api/Program.cs
  git commit -m "feat: add UserSessionTracker for tracking active BFF sessions per user"
  ```

---

### Task 3: Track sessions in BFF OidcSetup

**Files:**
- Modify: `src/Bff/His.Hope.Bff.Core/Authentication/OidcSetup.cs`

**Interfaces:**
- Consumes: `IConnectionMultiplexer` (already available), Redis SET operations
- Produces: Session added to `HisHope:user_sessions:{userId}` on every token validation

- [ ] **Step 1: Add SADD call in OnTokenValidated**

  In `OidcSetup.cs`, modify the `OnTokenValidated` event handler. After the session string is set successfully (after line 104), add:

  ```csharp
  // Track this session in the user's session set for cross-port logout
  var userSessionsKey = $"HisHope:user_sessions:{subjectId}";
  await db.SetAddAsync(userSessionsKey, sessionId);
  await db.KeyExpireAsync(userSessionsKey, TimeSpan.FromDays(7));
  ```

  The exact insertion point (after line 104, before the cookie append at line 106):
  ```csharp
  await db.StringSetAsync(
      $"session:{sessionId}",
      sessionJson,
      TimeSpan.FromHours(1));

  // NEW: Track this session for cross-port logout
  var userSessionsKey = $"HisHope:user_sessions:{subjectId}";
  await db.SetAddAsync(userSessionsKey, sessionId);
  await db.KeyExpireAsync(userSessionsKey, TimeSpan.FromDays(7));

  ctx.Response.Cookies.Append(SessionCookieName, sessionId, new CookieOptions
  ```

- [ ] **Step 2: Verify build**

  Run: `dotnet build src/Bff/His.Hope.Bff.Core/His.Hope.Bff.Core.csproj`
  Expected: Build succeeds

- [ ] **Step 3: Commit**

  ```bash
  git add src/Bff/His.Hope.Bff.Core/Authentication/OidcSetup.cs
  git commit -m "feat: track active sessions in Redis set for cross-port logout"
  ```

---

### Task 4: Revoke-all sessions on logout

**Files:**
- Modify: `src/Services/IdentityService/IdentityService.Api/Program.cs` (logout handler)

**Interfaces:**
- Consumes: `IConnectionMultiplexer`, `IUserSessionTracker`, `ITokenBlacklistService`
- Produces: All user sessions deleted + all tokens blacklisted on logout

- [ ] **Step 1: Modify logout handler to revoke-all sessions**

  In `Program.cs`, replace the logout handler (lines 509-545) with:

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
              var batch = db.CreateBatch();
              var tasks = new List<Task>(sessions.Length);
              foreach (var sid in sessions)
              {
                  tasks.Add(batch.KeyDeleteAsync($"session:{sid}"));
              }
              batch.Execute();
              await Task.WhenAll(tasks);
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

- [ ] **Step 2: Verify build**

  Run: `dotnet build src/Services/IdentityService/IdentityService.Api/IdentityService.Api.csproj`
  Expected: Build succeeds

- [ ] **Step 3: Commit**

  ```bash
  git add src/Services/IdentityService/IdentityService.Api/Program.cs
  git commit -m "feat: revoke all user sessions on logout for cross-port SSO"
  ```

---

### Task 5: BroadcastChannel API in Angular

**Files:**
- Modify: `src/Frontend/his-hope-app/src/app/core/services/auth.service.ts`

**Interfaces:**
- Consumes: `BroadcastChannel` API (browser-native, no npm dependency)
- Produces: Cross-tab logout notification within same browser

- [ ] **Step 1: Add BroadcastChannel to AuthService**

  In `auth.service.ts`, add the following:

  ```typescript
  private static readonly AUTH_CHANNEL = 'hishop_auth';

  constructor() {
    this.oidcSecurityService.checkAuth().pipe(take(1)).subscribe({
      next: ({ isAuthenticated }) => {
        if (isAuthenticated) this.loadUserFromOidc();
        this.checkAuthInit$.next();
        this.checkAuthInit$.complete();
      },
      error: () => {
        this.checkAuthInit$.next();
        this.checkAuthInit$.complete();
      },
    });

    // NEW: Listen for cross-tab logout events
    this.initBroadcastChannel();
  }

  // NEW
  private initBroadcastChannel(): void {
    const channel = new BroadcastChannel(AuthService.AUTH_CHANNEL);
    channel.onmessage = (event: MessageEvent) => {
      if (event.data?.type === 'LOGOUT') {
        this.currentUserSubject.next(null);
        this.permissionCache.clear();
        // Use Angular Router via injector to avoid circular dep
        import('@angular/router').then(({ Router }) => {
          const router = inject(Router);
          if (!router.url.includes('/auth/login')) {
            router.navigate(['/auth/login']);
          }
        });
      }
    };
  }
  ```

  **Important:** The `inject(Router)` inside a callback needs to be handled carefully. Since `AuthService` is `providedIn: 'root'`, we can inject `Router` in the constructor instead. Let me use a simpler approach:

  In the constructor, inject `Router`:
  ```typescript
  private router = inject(Router);
  ```

  Then in `initBroadcastChannel`:
  ```typescript
  private initBroadcastChannel(): void {
    const channel = new BroadcastChannel(AuthService.AUTH_CHANNEL);
    channel.onmessage = (event: MessageEvent) => {
      if (event.data?.type === 'LOGOUT') {
        this.currentUserSubject.next(null);
        this.permissionCache.clear();
        if (!this.router.url.includes('/auth/login')) {
          this.router.navigate(['/auth/login']);
        }
      }
    };
  }
  ```

- [ ] **Step 2: Broadcast on logout**

  Modify the `logout()` method to broadcast before clearing:

  ```typescript
  logout(): Observable<void> {
    // Broadcast to other tabs BEFORE calling API
    this.broadcastLogout();

    return this.http.post<void>(`${this.baseUrl}/logout`, {}, { withCredentials: true }).pipe(
      tap(() => {
        this.currentUserSubject.next(null);
        this.clearStoredAccessToken();
        this.permissionCache.clear();
      }),
      retry(1),
      catchError((error) => {
        this.currentUserSubject.next(null);
        this.clearStoredAccessToken();
        this.permissionCache.clear();
        return this.handleError(error);
      }),
    );
  }
  ```

  Modify the `oidcLogout()` method similarly:
  ```typescript
  oidcLogout(): void {
    this.broadcastLogout();
    this.oidcSecurityService.logoff().subscribe(() => {
      this.currentUserSubject.next(null);
      this.permissionCache.clear();
    });
  }
  ```

  Add the broadcast helper method:
  ```typescript
  private broadcastLogout(): void {
    try {
      const channel = new BroadcastChannel(AuthService.AUTH_CHANNEL);
      channel.postMessage({ type: 'LOGOUT' });
      channel.close();
    } catch {
      // BroadcastChannel not supported (Safari < 15.4) — silently ignore
    }
  }
  ```

- [ ] **Step 3: Add Router import and injection**

  Add to existing imports:
  ```typescript
  import { Router } from '@angular/router';
  ```

  Add to class fields:
  ```typescript
  private router = inject(Router);
  ```

- [ ] **Step 4: Verify the final file compiles**

  Run: `npx tsc --noEmit --project src/Frontend/his-hope-app/tsconfig.app.json`
  Or check: `cd src/Frontend/his-hope-app && npx ng build --configuration production 2>&1 | head -30`
  Expected: No TypeScript errors

- [ ] **Step 5: Commit**

  ```bash
  git add src/Frontend/his-hope-app/src/app/core/services/auth.service.ts
  git commit -m "feat: add BroadcastChannel API for cross-tab SSO logout notification"
  ```

---

### Task 6: E2E Test — Cross-Port SSO Logout

**Files:**
- Create: `tests/e2e/specs/auth/sso-logout.spec.ts`

**Note:** This test requires running multiple Angular apps on different ports. Adjust for local dev environment.

- [ ] **Step 1: Create E2E test**

  ```typescript
  import { test, expect } from '@playwright/test';

  test.describe('Cross-port SSO logout', () => {
    test('logging out on port 8081 invalidates session on port 8082', async ({ browser }) => {
      // Use a single context (same browser session, shares cookies across tabs/ports)
      const context = await browser.newContext();

      const page1 = await context.newPage();
      const page2 = await context.newPage();

      // Login on page1 (port 8081)
      await page1.goto('http://localhost:8081/auth/login');
      // Fill in login form (adjust selectors to match actual login page)
      await page1.fill('input[type="text"], input[name="username"]', 'testuser');
      await page1.fill('input[type="password"]', 'Test123!');
      await page1.click('button[type="submit"]');
      await page1.waitForURL('**/dashboard');
      await expect(page1.locator('text=Dashboard')).toBeVisible();

      // Open page2 (port 8082) — SSO cookie is shared within same browser context
      await page2.goto('http://localhost:8082/dashboard');
      await expect(page2.locator('text=Dashboard')).toBeVisible();

      // Logout on page1
      await page1.click('[data-testid="logout-button"], button:has-text("Logout")');
      await page1.waitForURL('**/auth/login');

      // Wait a moment for Redis/TTL propagation
      await page2.waitForTimeout(2000);

      // Navigate on page2 — should redirect to login (token blacklisted in Redis)
      await page2.goto('http://localhost:8082/dashboard');
      await page2.waitForURL('**/auth/login');
      await expect(page2.locator('text=Login')).toBeVisible();

      await context.close();
    });
  });
  ```

- [ ] **Step 2: Verify test runs**

  Run: `npx playwright test tests/e2e/specs/auth/sso-logout.spec.ts --workers=1`
  Expected: Test passes (may require auth setup)

- [ ] **Step 3: Commit**

  ```bash
  git add tests/e2e/specs/auth/sso-logout.spec.ts
  git commit -m "test: add E2E test for cross-port SSO logout"
  ```
