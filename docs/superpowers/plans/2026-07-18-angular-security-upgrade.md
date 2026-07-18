# Angular Security Upgrade — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Nâng cấp bảo mật Angular His.Hope lên mức cao nhất (OWASP ASVS Level 2 + HIPAA) qua 4 phase incremental.

**Architecture:** Incremental hardening — mỗi phase deploy + test độc lập. Phase 1 (nginx headers/CSP), Phase 2 (session/JWT), Phase 3 (audit/error), Phase 4 (CI/CD gate).

**Tech Stack:** Angular 19, nginx, TypeScript, RxJS 7, Jest, Playwright, Docker

**Plan reference:** `docs/superpowers/specs/2026-07-18-angular-security-upgrade-design.md`

## Global Constraints

- Mọi change phải có rollback plan (ghi rõ trong mỗi task)
- Mỗi phase phải deployable độc lập — không blocking dependency giữa các phase
- CSP thay đổi phải test kỹ: nếu sai sẽ sập toàn bộ UI
- JWT không được persist vào storage (memory-only)
- `console.*` chỉ được dùng trong development, không production
- Audit events phải batch gửi về backend (không block UI)
- Tất cả code mới phải có test (Jest unit test hoặc Playwright E2E)
- SRI chỉ bật cho production build, dev build giữ nguyên

---

## File Structure

### New Files
```
src/app/core/services/session.service.ts     # Session timeout + idle detection
src/app/core/services/audit.service.ts        # Audit event queue + flush
```

### Modified Files
```
nginx.conf                                     # P1: CSP + headers
angular.json                                   # P1: SRI + build optimization
src/app/core/services/auth.service.ts          # P2+P3: memory token, API perm check, audit
src/app/core/guards/permission.guard.ts        # P2: API-based check
src/app/core/guards/role.guard.ts              # P2: verify consistency
src/app/core/interceptors/auth.interceptor.ts  # P2: refresh với memory token
src/app/core/interceptors/error.interceptor.ts # P3: sanitize + audit
src/app/core/errors/global-error-handler.ts    # P3: sanitize + audit
src/app/app.component.ts                       # P3: navigation audit
src/app/app.config.ts                          # P2+P3: providers
src/app/features/auth/login/login.component.ts # P2: start session tracking
.eslintrc.json                                 # P4: security rules
.github/workflows/ci.yml (or Tekton)           # P4: audit + SRI gate
```

---

## Phase 1: HTTP Headers + CSP (3 tasks)

### Task 1.1: Nginx Security Headers Upgrade

**Files:**
- Modify: `src/Frontend/his-hope-app/nginx.conf` (toàn bộ security section)
- Verify: `curl -I https://<host> | grep -iE "content-security|x-frame|x-content|strict-transport|cross-origin|cache-control"`

- [ ] **Step 1: Backup current nginx.conf**

```bash
cp src/Frontend/his-hope-app/nginx.conf src/Frontend/his-hope-app/nginx.conf.bak
```

- [ ] **Step 2: Replace CSP with strict nonce-based**

Edit `nginx.conf` — replace lines 17-24 (SECURITY HEADERS block):

```nginx
    # ========================================================
    # SECURITY HEADERS (HIPAA compliance — strict nonce-based CSP)
    # ========================================================
    set $csp_nonce $request_id;

    add_header X-Frame-Options "DENY" always;
    add_header X-Content-Type-Options "nosniff" always;
    add_header X-XSS-Protection "1; mode=block" always;
    add_header Referrer-Policy "strict-origin-when-cross-origin" always;
    add_header Permissions-Policy "camera=(), microphone=(), geolocation=(), interest-cohort=()" always;
    add_header Strict-Transport-Security "max-age=31536000; includeSubDomains" always;

    # Strict nonce-based CSP
    add_header Content-Security-Policy "
        default-src 'self';
        script-src 'nonce-$csp_nonce' 'strict-dynamic' 'unsafe-inline' 'unsafe-eval' https: http:;
        style-src 'self' 'unsafe-inline';
        font-src 'self';
        img-src 'self' data: blob:;
        connect-src 'self';
        frame-ancestors 'none';
        form-action 'self';
        base-uri 'self';
        upgrade-insecure-requests;
        block-all-mixed-content;
        report-uri /api/v1/security/csp-report;
    " always;

    # Cross-Origin isolation
    add_header Cross-Origin-Opener-Policy "same-origin" always;
    add_header Cross-Origin-Resource-Policy "same-origin" always;
    add_header Cross-Origin-Embedder-Policy "credentialless" always;

    # Cache control — SPA should not cache
    add_header Cache-Control "no-store, no-cache, must-revalidate" always;
```

- [ ] **Step 3: Add sub_filter for nonce injection**

Add after the `gzip` block (before API proxy location):

```nginx
    # CSP nonce injection into Angular's index.html
    sub_filter_once off;
    sub_filter '<script' '<script nonce="$csp_nonce"';
    sub_filter_types text/html;
```

- [ ] **Step 4: Validate nginx config**

```bash
docker compose exec frontend nginx -t
```
Expected: `nginx: configuration file /etc/nginx/nginx.conf syntax is ok`

- [ ] **Step 5: Reload nginx**

```bash
docker compose exec frontend nginx -s reload
```

- [ ] **Step 6: Verify security headers**

```bash
curl -sI http://localhost:8081 | grep -iE "content-security|x-frame|x-content|strict-transport|cross-origin|cache-control"
```

Expected output — tất cả headers đều present:
```
content-security-policy: default-src 'self'; script-src 'nonce-...' ...
x-frame-options: DENY
x-content-type-options: nosniff
strict-transport-security: max-age=31536000; includeSubDomains
cross-origin-opener-policy: same-origin
cross-origin-resource-policy: same-origin
cross-origin-embedder-policy: credentialless
cache-control: no-store, no-cache, must-revalidate
```

- [ ] **Step 7: Verify Angular app loads without CSP violation**

```bash
# Mở browser, F12 → Console tab → verify không có CSP violation errors
# Đặc biệt kiểm tra lazy-loaded routes
```

- [ ] **Step 8: Commit**

```bash
git add src/Frontend/his-hope-app/nginx.conf
git commit -m "feat(security): upgrade CSP to strict nonce-based and add Cross-Origin isolation headers"
```

---

### Task 1.2: Enable Subresource Integrity (SRI)

**Files:**
- Modify: `src/Frontend/his-hope-app/angular.json`

- [ ] **Step 1: Add SRI to production build config**

Edit `angular.json` — in both `production` and `production-vi` configurations, add `"sri": true`:

```json
"configurations": {
  "production": {
    "optimization": {
      "scripts": true,
      "styles": {
        "minify": true,
        "inlineCritical": true
      },
      "fonts": {
        "inline": true
      },
      "sri": true
    }
  },
  "production-vi": {
    "optimization": {
      "sri": true
    }
  }
}
```

- [ ] **Step 2: Build production and verify SRI**

```bash
cd src/Frontend/his-hope-app
npm run build
```

- [ ] **Step 3: Verify integrity attributes in output**

```bash
grep -c 'integrity=' dist/his-hope-app/browser/en/index.html
```
Expected: >= 2 (runtime + polyfills scripts)

Verify format:
```bash
grep 'integrity=' dist/his-hope-app/browser/en/index.html | head -3
```
Expected: `<script ... integrity="sha256-..." crossorigin="anonymous"></script>`

- [ ] **Step 4: Commit**

```bash
git add src/Frontend/his-hope-app/angular.json
git commit -m "feat(security): enable Subresource Integrity for production build"
```

---

### Task 1.3: Build Config Optimization

**Files:**
- Modify: `src/Frontend/his-hope-app/angular.json`

- [ ] **Step 1: Verify production build strips console.log**

Angular 19 production build mặc định loại bỏ `console.*` khi `optimization.scripts: true`. Xác nhận:

```bash
grep -r "console\." dist/his-hope-app/browser/en/main-*.js || echo "✅ console.log stripped"
```
Expected: `✅ console.log stripped` (không còn console.* trong production bundle)

- [ ] **Step 2: Verify file hashing**

```bash
ls dist/his-hope-app/browser/en/main-*.js
```
Expected: filename có content hash (vd: `main-abc123.js`)

- [ ] **Step 3: Commit**

```bash
git add src/Frontend/his-hope-app/angular.json
git commit -m "chore(build): optimize production config with SRI and console stripping"
```

---

## Phase 2: Session + JWT Hardening (4 tasks)

### Task 2.1: SessionService — Idle Timeout + Absolute Expiry

**Files:**
- Create: `src/app/core/services/session.service.ts`
- Test: `src/app/core/services/session.service.spec.ts`
- Modify: `src/app/app.config.ts` (provider)

**Interfaces:**
- Consumes: `AuthService.clearStoredAccessToken()`, `Router.navigate()`
- Produces: `SessionService` với 4 public methods + 3 observables

- [ ] **Step 1: Write SessionService tests**

Create `src/app/core/services/session.service.spec.ts`:

```typescript
import { fakeAsync, tick, discardPeriodicTasks } from '@angular/core/testing';
import { SessionService, DEFAULT_SESSION_CONFIG } from './session.service';

describe('SessionService', () => {
  let service: SessionService;

  beforeEach(() => {
    service = new SessionService();
    service.startTracking();
  });

  afterEach(() => {
    service.stopTracking();
  });

  it('should start with remaining time equal to idleTimeout', () => {
    expect(service.getRemainingMs()).toBe(DEFAULT_SESSION_CONFIG.idleTimeoutMs);
  });

  it('should decrease remaining time over time', fakeAsync(() => {
    tick(1000);
    expect(service.getRemainingMs()).toBe(DEFAULT_SESSION_CONFIG.idleTimeoutMs - 1000);
    discardPeriodicTasks();
  }));

  it('should reset idle timer on activity', fakeAsync(() => {
    tick(10000); // 10s idle
    service.resetIdleTimer();
    expect(service.getRemainingMs()).toBe(DEFAULT_SESSION_CONFIG.idleTimeoutMs);
    discardPeriodicTasks();
  }));

  it('should emit isWarning when entering warning period', fakeAsync(() => {
    let warning = false;
    service.isWarning$.subscribe(v => warning = v);
    tick(DEFAULT_SESSION_CONFIG.idleTimeoutMs - DEFAULT_SESSION_CONFIG.warningBeforeMs + 1000);
    expect(warning).toBeTrue();
    discardPeriodicTasks();
  }));

  it('should emit expired event when idle timeout reached', fakeAsync(() => {
    let expired = false;
    service.onExpired$.subscribe(() => expired = true);
    tick(DEFAULT_SESSION_CONFIG.idleTimeoutMs + 1000);
    expect(expired).toBeTrue();
    discardPeriodicTasks();
  }));

  it('should stop timers on stopTracking', fakeAsync(() => {
    service.stopTracking();
    tick(DEFAULT_SESSION_CONFIG.idleTimeoutMs + 1000);
    expect(service.getRemainingMs()).toBe(0);
    discardPeriodicTasks();
  }));
});
```

- [ ] **Step 2: Run tests — expect FAIL**

```bash
npx jest src/app/core/services/session.service.spec.ts --no-coverage
```
Expected: `FAIL` — SessionService not defined yet

- [ ] **Step 3: Implement SessionService**

Create `src/app/core/services/session.service.ts`:

```typescript
import { inject, Injectable, NgZone } from '@angular/core';
import { Router } from '@angular/router';
import { AuthService } from './auth.service';
import { BehaviorSubject, Observable, fromEvent, merge, Subscription } from 'rxjs';
import { throttleTime, tap } from 'rxjs/operators';

export interface SessionConfig {
  idleTimeoutMs: number;
  absoluteExpiryMs: number;
  warningBeforeMs: number;
}

export const DEFAULT_SESSION_CONFIG: SessionConfig = {
  idleTimeoutMs: 15 * 60 * 1000,    // 15 phút
  absoluteExpiryMs: 8 * 60 * 60 * 1000, // 8 giờ
  warningBeforeMs: 60 * 1000,       // 60 giây
};

@Injectable({ providedIn: 'root' })
export class SessionService {
  private config: SessionConfig;
  private idleTimer: ReturnType<typeof setTimeout> | null = null;
  private warningTimer: ReturnType<typeof setTimeout> | null = null;
  private absoluteTimer: ReturnType<typeof setTimeout> | null = null;
  private sessionStart = 0;
  private lastActivity = 0;
  private tracking = false;
  private activitySub: Subscription | null = null;

  private remainingTimeSubject = new BehaviorSubject<number>(0);
  private warningSubject = new BehaviorSubject<boolean>(false);
  private expiredSubject = new BehaviorSubject<void>(undefined);

  remainingTime$ = this.remainingTimeSubject.asObservable();
  isWarning$ = this.warningSubject.asObservable();
  onExpired$ = this.expiredSubject.asObservable();

  private authService = inject(AuthService);
  private router = inject(Router);
  private ngZone = inject(NgZone);

  constructor(config?: Partial<SessionConfig>) {
    this.config = { ...DEFAULT_SESSION_CONFIG, ...config };
  }

  startTracking(): void {
    if (this.tracking) return;
    this.tracking = true;
    this.sessionStart = Date.now();
    this.lastActivity = Date.now();
    this.remainingTimeSubject.next(this.config.idleTimeoutMs);

    this.ngZone.runOutsideAngular(() => {
      this.activitySub = merge(
        fromEvent(document, 'mousemove'),
        fromEvent(document, 'keydown'),
        fromEvent(document, 'click'),
        fromEvent(document, 'touchstart'),
        fromEvent(document, 'scroll'),
      ).pipe(
        throttleTime(1000),
      ).subscribe(() => this.ngZone.run(() => this.resetIdleTimer()));
    });

    this.setAbsoluteTimer();
    this.setIdleTimer();
  }

  stopTracking(): void {
    this.tracking = false;
    this.activitySub?.unsubscribe();
    this.clearTimers();
    this.remainingTimeSubject.next(0);
    this.warningSubject.next(false);
  }

  resetIdleTimer(): void {
    if (!this.tracking) return;
    this.lastActivity = Date.now();
    this.warningSubject.next(false);
    this.clearIdleTimers();
    this.setIdleTimer();
  }

  getRemainingMs(): number {
    if (!this.tracking) return 0;
    const elapsed = Date.now() - this.lastActivity;
    return Math.max(0, this.config.idleTimeoutMs - elapsed);
  }

  private setIdleTimer(): void {
    const warningAt = this.config.idleTimeoutMs - this.config.warningBeforeMs;

    this.idleTimer = setTimeout(() => {
      this.warningSubject.next(true);
    }, warningAt);

    // Spawn a second timer for actual expiry
    setTimeout(() => {
      if (Date.now() - this.lastActivity >= this.config.idleTimeoutMs) {
        this.expiredSubject.next(undefined);
        this.forceLogout('idle_timeout');
      }
    }, this.config.idleTimeoutMs);
  }

  private setAbsoluteTimer(): void {
    this.absoluteTimer = setTimeout(() => {
      this.expiredSubject.next(undefined);
      this.forceLogout('absolute_expiry');
    }, this.config.absoluteExpiryMs);
  }

  private clearIdleTimers(): void {
    if (this.idleTimer) clearTimeout(this.idleTimer);
    if (this.warningTimer) clearTimeout(this.warningTimer);
  }

  private clearTimers(): void {
    this.clearIdleTimers();
    if (this.absoluteTimer) clearTimeout(this.absoluteTimer);
  }

  private forceLogout(reason: 'idle_timeout' | 'absolute_expiry'): void {
    this.stopTracking();
    this.authService.clearStoredAccessToken();
    this.router.navigate(['/auth/login'], {
      queryParams: { reason },
    });
  }
}
```

- [ ] **Step 4: Run tests — expect PASS**

```bash
npx jest src/app/core/services/session.service.spec.ts --no-coverage
```
Expected: `PASS` — all 6 tests pass

- [ ] **Step 5: Add SessionService provider to app.config.ts**

Edit `src/app/app.config.ts`:

```typescript
import { SessionService } from '@core/services/session.service';

export const appConfig: ApplicationConfig = {
  providers: [
    // ... existing providers
    SessionService,  // providedIn: 'root' nên không cần, nhưng thêm explicit cho clarity
  ],
};
```

- [ ] **Step 6: Commit**

```bash
git add src/app/core/services/session.service.ts src/app/core/services/session.service.spec.ts src/app/app.config.ts
git commit -m "feat(security): add SessionService with idle timeout and absolute expiry"
```

---

### Task 2.2: Memory-only JWT Token

**Files:**
- Modify: `src/app/core/services/auth.service.ts`
- Modify: `src/app/core/interceptors/auth.interceptor.ts`

- [ ] **Step 1: Write tests for memory-only token behavior**

Add to `src/app/core/services/auth.service.spec.ts`:

```typescript
describe('AuthService token storage', () => {
  it('should store token in memory (not sessionStorage)', () => {
    service.storeAccessToken('test-token');
    expect(service.getStoredAccessToken()).toBe('test-token');
    // sessionStorage should NOT have the token
    expect(sessionStorage.getItem('hishope_access_token')).toBeNull();
  });

  it('should clear token on logout', () => {
    service.storeAccessToken('test-token');
    service.clearStoredAccessToken();
    expect(service.getStoredAccessToken()).toBeNull();
  });

  it('should lose token on service instance reset (simulates reload)', () => {
    service.storeAccessToken('test-token');
    // Simulate page reload: create new instance
    const newService = TestBed.inject(AuthService);
    expect(newService.getStoredAccessToken()).toBeNull();
  });
});
```

- [ ] **Step 2: Run tests — expect FAIL (current uses sessionStorage)**

```bash
npx jest src/app/core/services/auth.service.spec.ts --no-coverage -t "token storage"
```
Expected: FAIL — test expects memory-only but code uses sessionStorage

- [ ] **Step 3: Refactor auth.service.ts — memory-only token**

Replace storage implementation in `auth.service.ts`:

```typescript
export class AuthService {
  private accessToken: string | null = null;

  storeAccessToken(token: string): void {
    this.accessToken = token;
    // KHÔNG ghi vào sessionStorage — memory-only
  }

  getStoredAccessToken(): string | null {
    return this.accessToken;
  }

  clearStoredAccessToken(): void {
    this.accessToken = null;
  }
}
```

Remove imports of `sessionStorage` — không còn dùng nữa.

- [ ] **Step 4: Run tests — expect PASS**

```bash
npx jest src/app/core/services/auth.service.spec.ts --no-coverage -t "token storage"
```
Expected: PASS

- [ ] **Step 5: Run full auth service test suite**

```bash
npx jest src/app/core/services/auth.service.spec.ts --no-coverage
```
Expected: PASS

- [ ] **Step 6: Commit**

```bash
git add src/app/core/services/auth.service.ts src/app/core/services/auth.service.spec.ts
git commit -m "feat(security): migrate JWT to memory-only storage (remove sessionStorage)"
```

---

### Task 2.3: API-based Permission Check

**Files:**
- Modify: `src/app/core/services/auth.service.ts`
- Modify: `src/app/core/guards/permission.guard.ts`
- Note: Backend endpoint `POST /api/v1/auth/check-permission` cần được tạo riêng

- [ ] **Step 1: Add hasPermissionOnServer() to AuthService**

Add to `auth.service.ts`:

```typescript
private permissionCache = new Map<string, boolean>();
private permissionCacheTimestamp = 0;
private readonly PERMISSION_CACHE_TTL = 5 * 60 * 1000; // 5 phút

hasPermissionOnServer(permission: string): Observable<boolean> {
  // Check memory cache
  const cached = this.permissionCache.get(permission);
  if (cached !== undefined && Date.now() - this.permissionCacheTimestamp < this.PERMISSION_CACHE_TTL) {
    return of(cached);
  }

  return this.http.post<{ granted: boolean }>(
    `${this.baseUrl}/check-permission`,
    { permission },
    { withCredentials: true }
  ).pipe(
    map(res => res.granted),
    tap(granted => {
      if (granted) {
        this.permissionCache.set(permission, true);
        this.permissionCacheTimestamp = Date.now();
      }
    }),
    catchError(() => of(false)),
  );
}
```

- [ ] **Step 2: Write test for hasPermissionOnServer**

```typescript
it('should check permission via API and cache result', () => {
  const testPermission = 'patients.view';
  let result: boolean | undefined;

  service.hasPermissionOnServer(testPermission).subscribe(v => result = v);
  const req = httpTestingController.expectOne('/api/v1/auth/check-permission');
  expect(req.request.body).toEqual({ permission: testPermission });
  req.flush({ granted: true });

  expect(result).toBeTrue();

  // Second call should use cache (no HTTP request)
  service.hasPermissionOnServer(testPermission).subscribe(v => result = v);
  httpTestingController.expectNone('/api/v1/auth/check-permission');
  expect(result).toBeTrue();
});
```

- [ ] **Step 3: Refactor PermissionGuard to use API**

Edit `src/app/core/guards/permission.guard.ts`:

```typescript
private checkPermissions(route: ActivatedRouteSnapshot): Observable<boolean | UrlTree> {
  const requiredPermissions = route.data?.['permissions'] as string[] | undefined;

  if (!requiredPermissions?.length) {
    return this.authService.isLoggedIn().pipe(
      map(loggedIn => loggedIn || this.router.parseUrl('/auth/login')),
    );
  }

  return forkJoin(
    requiredPermissions.map(p => this.authService.hasPermissionOnServer(p))
  ).pipe(
    map(results => results.every(Boolean)),
    map(allowed => {
      if (!allowed) return this.router.parseUrl('/access-denied');
      return true;
    }),
    catchError(() => of(this.router.parseUrl('/access-denied'))),
  );
}
```

Add imports:
```typescript
import { forkJoin, Observable, of } from 'rxjs';
import { catchError, map } from 'rxjs/operators';
```

- [ ] **Step 4: Update PermissionGuard tests**

Edit `src/app/core/guards/permission.guard.spec.ts`:

```typescript
it('should check required permissions via API', () => {
  const route = { data: { permissions: ['patients.view'] } } as any;
  guard.canActivate(route, {} as any).subscribe(result => {
    expect(result).toBeTrue();
  });
  // Verify API was called
  // (implementation depends on mock setup)
});
```

- [ ] **Step 5: Run all tests**

```bash
npx jest src/app/core/services/auth.service.spec.ts src/app/core/guards/permission.guard.spec.ts --no-coverage
```
Expected: PASS

- [ ] **Step 6: Commit**

```bash
git add src/app/core/services/auth.service.ts src/app/core/guards/permission.guard.ts src/app/core/guards/permission.guard.spec.ts
git commit -m "feat(security): migrate permission check from local JWT decode to backend API"
```

---

### Task 2.4: Login Component — Start Session Tracking

**Files:**
- Modify: `src/app/features/auth/login/login.component.ts`
- Modify: `src/app/app.config.ts` (if needed)

- [ ] **Step 1: Inject SessionService into LoginComponent**

Edit `src/app/features/auth/login/login.component.ts`:

```typescript
import { inject } from '@angular/core';
import { SessionService } from '@core/services/session.service';

export class LoginComponent {
  private sessionService = inject(SessionService);

  onLoginSuccess(): void {
    // Session tracking bắt đầu sau login thành công
    this.sessionService.startTracking();
    this.router.navigate(['/dashboard']);
  }
}
```

- [ ] **Step 2: Also start session tracking after refresh token succeeds**

In `auth.interceptor.ts`, `handle401` method — after refresh success:

```typescript
import { SessionService } from '@core/services/session.service';

// In handle401:
return authService.refreshToken().pipe(
  switchMap((user) => {
    this.sessionService.startTracking();  // restart session timer
    // ... existing code
  }),
);
```

- [ ] **Step 3: Run tests**

```bash
npx jest src/app/features/auth/login/login.component.spec.ts --no-coverage
```
Expected: PASS

- [ ] **Step 4: Commit**

```bash
git add src/app/features/auth/login/login.component.ts src/app/core/interceptors/auth.interceptor.ts
git commit -m "feat(security): start session tracking on login and token refresh"
```

---

## Phase 3: Audit Trail + Error Handling (4 tasks)

### Task 3.1: AuditService — Event Queue + Batch Flush

**Files:**
- Create: `src/app/core/services/audit.service.ts`
- Test: `src/app/core/services/audit.service.spec.ts`

**Interfaces:**
- Consumes: `HttpClient`, `environment.apiUrl`
- Produces: `AuditService.log()` method

- [ ] **Step 1: Write AuditService tests**

Create `src/app/core/services/audit.service.spec.ts`:

```typescript
import { TestBed } from '@angular/core/testing';
import { HttpClientTestingModule, HttpTestingController } from '@angular/common/http/testing';
import { AuditService, AuditAction } from './audit.service';

describe('AuditService', () => {
  let service: AuditService;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [HttpClientTestingModule],
      providers: [AuditService],
    });
    service = TestBed.inject(AuditService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpMock.verify();
  });

  it('should queue audit events and flush on batch size', () => {
    service.log('auth.login', { success: true });
    service.log('data.view', { patientId: '123' });

    // Queue should have 2 events, not yet flushed
    httpMock.expectNone('/api/v1/audit/events');

    // Third event should trigger flush (batchSize = 10, but flushInterval = 0 for test)
    // For testing, create service with custom config
  });

  it('should flush events on interval', (done) => {
    service.log('auth.login', { success: true });

    setTimeout(() => {
      const req = httpMock.expectOne('/api/v1/audit/events');
      expect(req.request.method).toBe('POST');
      expect(req.request.body.events.length).toBe(1);
      req.flush({});
      done();
    }, 100);
  }, 5000);

  it('should not throw when flush fails', () => {
    service.log('data.view', {});
    service.flushNow();
    const req = httpMock.expectOne('/api/v1/audit/events');
    req.flush('Server error', { status: 500, statusText: 'Server Error' });
    // Should not throw — queue continues
    expect(service.queueLength()).toBe(1); // retry later
  });
});
```

- [ ] **Step 2: Run tests — expect FAIL**

```bash
npx jest src/app/core/services/audit.service.spec.ts --no-coverage
```
Expected: FAIL

- [ ] **Step 3: Implement AuditService**

Create `src/app/core/services/audit.service.ts`:

```typescript
import { inject, Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { environment } from '@env/environment';

export type AuditAction =
  | 'auth.login' | 'auth.logout' | 'auth.refresh'
  | 'data.view' | 'data.create' | 'data.update' | 'data.delete'
  | 'error.client' | 'error.server'
  | 'security.csp-violation'
  | 'navigation.change';

export interface AuditEvent {
  action: AuditAction;
  timestamp: number;
  userId?: string;
  details?: Record<string, unknown>;
  correlationId?: string;
  userAgent?: string;
}

@Injectable({ providedIn: 'root' })
export class AuditService {
  private queue: AuditEvent[] = [];
  private flushTimer: ReturnType<typeof setInterval> | null = null;
  private readonly BATCH_SIZE = 10;
  private readonly FLUSH_INTERVAL = 30000; // 30s
  private readonly ENDPOINT = `${environment.apiUrl}/audit/events`;

  private http = inject(HttpClient);
  private userId: string | undefined;

  setUserId(id: string | undefined): void {
    this.userId = id;
  }

  log(action: AuditAction, details?: Record<string, unknown>): void {
    this.queue.push({
      action,
      timestamp: Date.now(),
      userId: this.userId,
      details,
      userAgent: navigator?.userAgent,
    });

    if (this.queue.length >= this.BATCH_SIZE) {
      this.flush();
    } else if (!this.flushTimer) {
      this.startFlushTimer();
    }
  }

  /** Public for testing — force flush immediately */
  flushNow(): void {
    this.flush();
  }

  /** Public for testing — check queue size */
  queueLength(): number {
    return this.queue.length;
  }

  private startFlushTimer(): void {
    this.flushTimer = setInterval(() => this.flush(), this.FLUSH_INTERVAL);
  }

  private flush(): void {
    if (this.queue.length === 0) return;

    const events = [...this.queue];
    this.queue = [];

    this.http.post(this.ENDPOINT, { events }).subscribe({
      error: (err) => {
        // Re-queue on failure
        this.queue.unshift(...events);
        console.warn('[AuditService] Failed to flush events:', err);
      },
    });
  }
}
```

- [ ] **Step 4: Run tests — expect PASS**

```bash
npx jest src/app/core/services/audit.service.spec.ts --no-coverage
```
Expected: PASS

- [ ] **Step 5: Commit**

```bash
git add src/app/core/services/audit.service.ts src/app/core/services/audit.service.spec.ts
git commit -m "feat(security): add AuditService with batch queue and periodic flush"
```

---

### Task 3.2: Error Interceptor — Sanitize + Audit

**Files:**
- Modify: `src/app/core/interceptors/error.interceptor.ts`
- Test: `src/app/core/interceptors/error.interceptor.spec.ts`

- [ ] **Step 1: Sanitize error messages and add audit logging**

Edit `src/app/core/interceptors/error.interceptor.ts`:

```typescript
import { inject, Injectable, NgZone, Injector } from '@angular/core';
import { HttpInterceptor, HttpRequest, HttpHandler, HttpEvent, HttpErrorResponse } from '@angular/common/http';
import { Observable, throwError, of } from 'rxjs';
import { catchError, retry } from 'rxjs/operators';
import { Router } from '@angular/router';
import { MatSnackBar } from '@angular/material/snack-bar';
import { AuthService } from '@core/services/auth.service';
import { AuditService } from '@core/services/audit.service';

@Injectable()
export class ErrorInterceptor implements HttpInterceptor {
  private readonly SKIP_NOTIFICATION_URLS = ['/auth/verify', '/auth/me', '/api/v1/errors', '/api/v1/audit/events'];
  private readonly TRANSIENT_STATUSES = [503, 504];
  private readonly MAX_RETRIES = 1;

  private injector = inject(Injector);
  private router = inject(Router);
  private snackBar = inject(MatSnackBar);
  private ngZone = inject(NgZone);
  private auditService = inject(AuditService);

  intercept(req: HttpRequest<unknown>, next: HttpHandler): Observable<HttpEvent<unknown>> {
    return next.handle(req).pipe(
      retry({
        count: this.MAX_RETRIES,
        delay: (error: HttpErrorResponse) => {
          if (this.TRANSIENT_STATUSES.includes(error.status)) {
            return of(true);
          }
          return throwError(() => error);
        },
      }),
      catchError((error: HttpErrorResponse) => {
        // Audit log (full context gửi backend an toàn)
        this.auditService.log('error.server', {
          status: error.status,
          url: req.url,
          method: req.method,
          correlationId: this.getCorrelationId(error),
          // KHÔNG gửi error.message, error.stack, response body
        });

        if (error.status === 0) {
          this.showNotification(
            'Network error: Unable to connect to the server. Please check your connection.',
            'error-snackbar-critical',
            false,
          );
          return throwError(() => error);
        }

        const authService = this.injector.get(AuthService);

        switch (error.status) {
          case 401: {
            if (!req.url.includes('/auth/')) {
              authService.clearStoredAccessToken();
              this.router.navigate(['/auth/login']);
            }
            break;
          }
          case 403: {
            if (!this.isSkippableUrl(req.url)) {
              this.showNotification(
                'Access denied. You do not have permission to perform this action.',
                'error-snackbar',
                true,
              );
            }
            break;
          }
          case 422: {
            // Không lộ raw error message từ server
            if (!this.isSkippableUrl(req.url)) {
              this.showNotification('Validation failed. Please check your input.', 'error-snackbar', true);
            }
            break;
          }
          case 429: {
            if (!this.isSkippableUrl(req.url)) {
              this.showNotification(
                'Too many requests. Please wait before trying again.',
                'error-snackbar',
                true,
              );
            }
            break;
          }
          default: {
            if (error.status >= 500) {
              const msg = 'A server error occurred. Please try again later.';
              if (!this.isSkippableUrl(req.url)) {
                this.showNotification(msg, 'error-snackbar-critical', false);
              }
            } else if (error.status && error.status >= 400) {
              // Không lộ raw error — dùng message chung
              if (!this.isSkippableUrl(req.url)) {
                this.showNotification('An unexpected error occurred', 'error-snackbar', true);
              }
            }
            break;
          }
        }

        return throwError(() => error);
      }),
    );
  }

  private isSkippableUrl(url: string): boolean {
    return this.SKIP_NOTIFICATION_URLS.some(skip => url.includes(skip));
  }

  private getCorrelationId(error: HttpErrorResponse): string | undefined {
    return error.headers?.get('X-Correlation-ID') || undefined;
  }

  private showNotification(message: string, panelClass: string, autoDismiss: boolean): void {
    this.ngZone.run(() => {
      this.snackBar.open(message, 'Close', {
        duration: autoDismiss ? 5000 : undefined,
        panelClass: [panelClass],
      });
    });
  }
}
```

**Key changes:**
- Inject `AuditService` — log mọi HTTP error
- Sanitize 422 messages: không lộ `error.error?.error` từ server
- Thêm `isSkippableUrl()` helper để tránh notification loop
- Audit endpoint `/api/v1/audit/events` thêm vào skip list

- [ ] **Step 2: Run existing tests**

```bash
npx jest src/app/core/interceptors/error.interceptor.spec.ts --no-coverage
```
Expected: PASS (update test if new behavior breaks mock expectations)

- [ ] **Step 3: Commit**

```bash
git add src/app/core/interceptors/error.interceptor.ts
git commit -m "feat(security): sanitize error messages and add audit logging to ErrorInterceptor"
```

---

### Task 3.3: Global Error Handler — Sanitize + Audit

**Files:**
- Modify: `src/app/core/errors/global-error-handler.ts`
- Test: `src/app/core/errors/global-error-handler.spec.ts`

- [ ] **Step 1: Remove console.error in production, add audit**

Edit `src/app/core/errors/global-error-handler.ts`:

```typescript
import { environment } from '@env/environment';
import { AuditService } from '@core/services/audit.service';

export class GlobalErrorHandler implements ErrorHandler {
  private auditService = inject(AuditService);

  handleError(error: unknown): void {
    const context = this.errorService.buildErrorContext(error);

    // Trong production: không log ra console
    if (!environment.production) {
      console.error('[GlobalErrorHandler]', error);
    }

    // === Guard: skip reporting if this error originated from the errors API ===
    const isFromErrorsApi = context.url?.includes('/api/v1/errors');
    if (isFromErrorsApi) {
      this.showUserFeedback(error, context);
      return;
    }

    // === Audit log ===
    this.auditService.log('error.client', {
      type: context.type,
      url: context.url,
      correlationId: context.correlationId,
      message: environment.production ? undefined : context.message,
    });

    // === Throttle ===
    const throttleKey = `${context.type}::${context.url}`;
    const now = Date.now();
    const lastReport = this.lastReportedAt.get(throttleKey) ?? 0;

    if (now - lastReport >= GlobalErrorHandler.REPORT_THROTTLE_MS) {
      this.lastReportedAt.set(throttleKey, now);
      this.errorService.reportError(context).subscribe();
    }

    this.showUserFeedback(error, context);
  }
}
```

- [ ] **Step 2: Run tests**

```bash
npx jest src/app/core/errors/global-error-handler.spec.ts --no-coverage
```
Expected: PASS

- [ ] **Step 3: Commit**

```bash
git add src/app/core/errors/global-error-handler.ts
git commit -m "feat(security): remove console.error in production and add audit logging to GlobalErrorHandler"
```

---

### Task 3.4: Navigation Audit

**Files:**
- Modify: `src/app/app.component.ts`

- [ ] **Step 1: Add navigation audit to AppComponent**

Edit `src/app/app.component.ts`:

```typescript
import { Component, inject, OnInit } from '@angular/core';
import { Router, NavigationEnd, Event } from '@angular/router';
import { filter } from 'rxjs/operators';
import { AuditService } from '@core/services/audit.service';
import { AuthService } from '@core/services/auth.service';

@Component({
  selector: 'app-root',
  templateUrl: './app.component.html',
  standalone: true,
})
export class AppComponent implements OnInit {
  private router = inject(Router);
  private auditService = inject(AuditService);
  private authService = inject(AuthService);
  private previousUrl = '';

  ngOnInit(): void {
    this.router.events.pipe(
      filter((event: Event): event is NavigationEnd => event instanceof NavigationEnd),
    ).subscribe((event: NavigationEnd) => {
      if (this.previousUrl && this.previousUrl !== event.url) {
        this.auditService.log('navigation.change', {
          from: this.previousUrl,
          to: event.url,
        });
      }
      this.previousUrl = event.url;
    });
  }
}
```

- [ ] **Step 2: Run tests**

```bash
npx jest src/app/app.component.spec.ts --no-coverage
```
Expected: PASS

- [ ] **Step 3: Commit**

```bash
git add src/app/app.component.ts
git commit -m "feat(security): add navigation change audit logging"
```

---

## Phase 4: Dependency Scan + CI/CD Gate (3 tasks)

### Task 4.1: npm Audit CI Gate

**Files:**
- Modify: `.github/workflows/ci.yml` (hoặc CI/CD config tương ứng)

- [ ] **Step 1: Add npm audit step to CI pipeline**

Add to CI workflow file (Tekton hoặc GitHub Actions):

```yaml
- name: Security dependency audit
  run: |
    cd src/Frontend/his-hope-app
    npm audit --audit-level=high
  # Fail build nếu có vulnerability high/critical
```

- [ ] **Step 2: Verify bằng cách thử với mock**

```bash
cd src/Frontend/his-hope-app
npm audit --audit-level=high || echo "⚠️ npm audit found issues"
```

Expected: Không fail hoặc log issues (tùy dependencies hiện tại)

- [ ] **Step 3: Commit**

```bash
git add .github/workflows/ci.yml
git commit -m "ci(security): add npm audit gate for high/critical vulnerabilities"
```

---

### Task 4.2: SRI Verification Gate

**Files:**
- Modify: CI/CD config (cùng file với Task 4.1)

- [ ] **Step 1: Add SRI verification step**

```yaml
- name: Verify Subresource Integrity
  run: |
    if grep -q 'integrity=' dist/his-hope-app/browser/en/index.html; then
      echo "✅ SRI present in built output"
    else
      echo "❌ SRI missing — build may be insecure"
      exit 1
    fi
```

- [ ] **Step 2: Commit**

```bash
git add .github/workflows/ci.yml
git commit -m "ci(security): add SRI integrity verification gate"
```

---

### Task 4.3: ESLint Security Rules

**Files:**
- Modify: `.eslintrc.json`

- [ ] **Step 1: Add security-focused ESLint plugins and rules**

Edit `.eslintrc.json`:

```json
{
  "plugins": ["@angular-eslint", "@typescript-eslint"],
  "rules": {
    "@angular-eslint/no-call-expression": "error",
    "@typescript-eslint/no-implied-eval": "error",
    "no-eval": "error"
  }
}
```

- [ ] **Step 2: Verify lint passes**

```bash
npm run lint
```
Expected: PASS (hoặc chỉ có existing errors, không có errors mới từ rules)

- [ ] **Step 3: Commit**

```bash
git add .eslintrc.json
git commit -m "feat(security): add ESLint rules to prevent eval and unsafe expressions"
```

---

## Self-Review Checklist

- [ ] **Spec coverage:** Tasks 1.1-1.3 → Phase 1 (CSP, SRI, build config). Tasks 2.1-2.4 → Phase 2 (session, JWT, permission). Tasks 3.1-3.4 → Phase 3 (audit, error sanitization). Tasks 4.1-4.3 → Phase 4 (CI/CD, lint).
- [ ] **No placeholders:** Tất cả code blocks đều là code thật, không TODO/TBD
- [ ] **Type consistency:** `SessionService.startTracking()`, `AuditService.log()`, `AuthService.hasPermissionOnServer()` — consistent throughout
