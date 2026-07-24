### Task 5: BroadcastChannel API in Angular

**Project:** His.Hope — Cross-Port SSO Logout

**Files:**
- Modify: `src/Frontend/his-hope-app/src/app/core/services/auth.service.ts`

**Context:** When user logs out on one Angular tab/port, other same-browser tabs need to know immediately. We use the browser-native `BroadcastChannel` API (zero npm dependencies, supported in Chrome, Firefox, Safari 15.4+, Edge).

**Implementation (exact code):**

Step 1: Add import for Router at the top:
```typescript
import { Router } from '@angular/router';
```

Step 2: Add Router injection and BroadcastChannel channel name to the class fields (after existing field declarations):
```typescript
  private router = inject(Router);
  private static readonly AUTH_CHANNEL = 'hishop_auth';
```

Step 3: In the constructor, add `initBroadcastChannel()` call at the end, after the existing subscription block:
```typescript
    // Listen for cross-tab logout events from other tabs
    this.initBroadcastChannel();
```

Step 4: Modify the `logout()` method to broadcast before calling API. Change the method to add broadcast call at the beginning:
```typescript
  /** @deprecated Use oidcLogout() for OIDC-based logout */
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

Step 5: Modify the `oidcLogout()` method to broadcast:
```typescript
  oidcLogout(): void {
    this.broadcastLogout();
    this.oidcSecurityService.logoff().subscribe(() => {
      this.currentUserSubject.next(null);
      this.permissionCache.clear();
    });
  }
```

Step 6: Add these two new methods at the end of the class (before the closing `}`):
```typescript
  private initBroadcastChannel(): void {
    try {
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
    } catch {
      // BroadcastChannel not supported (Safari < 15.4) — silently ignore
    }
  }

  private broadcastLogout(): void {
    try {
      const channel = new BroadcastChannel(AuthService.AUTH_CHANNEL);
      channel.postMessage({ type: 'LOGOUT' });
      channel.close();
    } catch {
      // BroadcastChannel not supported
    }
  }
```

**Steps:**
1. Make all 6 changes above to auth.service.ts
2. Verify TypeScript compilation: `cd src/Frontend/his-hope-app && npx ng build --configuration production 2>&1 | Select-Object -Last 20` (or just `npx tsc --noEmit -p tsconfig.app.json`)
3. Stage only auth.service.ts: `git add src/Frontend/his-hope-app/src/app/core/services/auth.service.ts`
4. Commit: `git commit -m "feat: add BroadcastChannel API for cross-tab SSO logout notification"`
5. Write report: `D:\AI\micro\.superpowers\sdd\task-5-report.md`

**CRITICAL:** Do NOT stage or commit any other files.

**Return:** DONE + commit hash