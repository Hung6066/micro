# Angular Security Upgrade — Design Spec

> **Date:** 2026-07-18
> **Status:** Draft
> **Approach:** Incremental Hardening (4 phases)
> **Priority:** Maximum — "nâng lên max toàn diện"

---

## Table of Contents

1. [Current Security Posture](#1-current-security-posture)
2. [Goal](#2-goal)
3. [Phase 1: HTTP Headers + CSP](#3-phase-1-http-headers--csp)
4. [Phase 2: Session + JWT Hardening](#4-phase-2-session--jwt-hardening)
5. [Phase 3: Audit Trail + Error Handling](#5-phase-3-audit-trail--error-handling)
6. [Phase 4: Dependency Scan + CI/CD Gate](#6-phase-4-dependency-scan--cicd-gate)
7. [Files Changed](#7-files-changed)
8. [Rollback Plan](#8-rollback-plan)
9. [Security Testing](#9-security-testing)

---

## 1. Current Security Posture

### 1.1 Existing (Good)

| Measure | Status |
|---------|--------|
| JWT stored in sessionStorage | ✅ |
| HttpOnly refresh cookies (`withCredentials: true`) | ✅ |
| Auth interceptor with Bearer token + refresh queue | ✅ |
| Route guards: AuthGuard, RoleGuard, PermissionGuard | ✅ |
| X-Frame-Options: DENY | ✅ |
| X-Content-Type-Options: nosniff | ✅ |
| X-XSS-Protection: 1; mode=block | ✅ |
| HSTS (max-age=31536000; includeSubDomains) | ✅ |
| Referrer-Policy: strict-origin-when-cross-origin | ✅ |
| Permissions-Policy (camera/mic/geo blocked) | ✅ |
| Global error handler with user feedback | ✅ |
| Error interceptor with retry for 503/504 | ✅ |
| Self-hosted Material Icons | ✅ |
| No `innerHTML` / `bypassSecurity` usage | ✅ |
| Server tokens hidden (`server_tokens off`) | ✅ |
| Hidden file access denied (`~ /\.` block) | ✅ |

### 1.2 Existing (Weak / Missing)

| Issue | Severity | Location |
|-------|----------|----------|
| CSP has `'unsafe-inline'` in script-src | **High** | `nginx.conf` line 24 |
| CSP references external Google Fonts | Medium | `nginx.conf` line 24 |
| CSP has no nonce, no `upgrade-insecure-requests` | Medium | `nginx.conf` line 24 |
| No Subresource Integrity (SRI) | **High** | `angular.json` build config |
| JWT decoded client-side from sessionStorage | **High** | `auth.service.ts` getStoredAccessToken |
| JWT permissions checked locally, not via API | Medium | `auth.service.ts` getUserPermissions |
| No session idle timeout | **High** | Missing entirely |
| No absolute session expiry | Medium | Missing entirely |
| `console.error` in production (info leakage) | Low | `auth.service.ts` handleError |
| Error messages show raw status/URLs | Low | `error.interceptor.ts`, `global-error-handler.ts` |
| No frontend audit trail | Medium | Missing entirely |
| No dependency vulnerability CI gate | **High** | Missing in pipeline |
| No CSP violation reporting | Medium | Missing entirely |
| Missing Cross-Origin-Opener-Policy | Medium | `nginx.conf` |
| Missing Cross-Origin-Resource-Policy | Medium | `nginx.conf` |
| Missing Cross-Origin-Embedder-Policy | Medium | `nginx.conf` |
| `environment.prod.ts` has hardcoded OTel URL | Low | `environment.prod.ts` |

---

## 2. Goal

Nâng cấp bảo mật Angular app His.Hope lên mức cao nhất — đạt các tiêu chuẩn:

- **OWASP ASVS Level 2** (Standard)
- **HIPAA Security Rule** (Administrative + Technical Safeguards)
- **Angular Security Best Practices** (Angular 19)

---

## 3. Phase 1: HTTP Headers + CSP

### 3.1 Content-Security-Policy (Strict)

#### 3.1.1 Nginx CSP Directive

Replace existing CSP in `nginx.conf` with nonce-based strict CSP:

```nginx
# Hiện tại
add_header Content-Security-Policy "default-src 'self'; script-src 'self' 'unsafe-inline'; style-src 'self' 'unsafe-inline' https://fonts.googleapis.com; font-src 'self' https://fonts.gstatic.com; img-src 'self' data:; connect-src 'self'; frame-ancestors 'none';" always;

# Mới
set $csp_nonce $request_id;
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
```

**Rationale:**
- **Nonce** (`$request_id`): thay thế `'unsafe-inline'` cho script-src — mỗi request nhận nonce duy nhất, script inline phải khớp nonce mới chạy
- **`strict-dynamic`**: cho phép script được nonce trusted tải script con — Angular lazy loading cần cơ chế này
- **`unsafe-inline` trong style-src**: Angular Material dùng inline styles cho dynamic theming — không thể thay bằng nonce
- **`upgrade-insecure-requests`**: tự động nâng HTTP lên HTTPS
- **`form-action 'self'`**: chống phishing form
- **`base-uri 'self'`**: chống base tag injection
- **`report-uri`**: ghi lại vi phạm CSP để debug và phát hiện tấn công

#### 3.1.2 Angular index.html Nonce Injection

Thêm nonce attribute vào `<script>` tags trong `index.html`:

```html
<!-- index.html sẽ được Angular inject script tags với nonce -->
<!-- Cần config Angular build để thêm nonce -->

<!-- Hiện tại: -->
<script src="runtime.abc123.js" type="module"></script>

<!-- Mới (nonce injection qua Angular post-build hoặc nginx sub_filter): -->
```

**Implementation approach:** Dùng `nginx sub_filter` để inject nonce vào HTML:

```nginx
sub_filter_once off;
sub_filter '<script' '<script nonce="$csp_nonce"';
```

### 3.2 Subresource Integrity (SRI)

Trong `angular.json`, bật SRI:

```json
"configurations": {
  "production": {
    "optimization": {
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

Angular CLI sẽ tự động thêm `integrity="sha256-..."` vào mọi `script` và `link` tag trong `index.html` khi build production.

### 3.3 Bổ sung Security Headers

Add vào `nginx.conf`:

```nginx
# Cross-Origin isolation
add_header Cross-Origin-Opener-Policy "same-origin" always;
add_header Cross-Origin-Resource-Policy "same-origin" always;
add_header Cross-Origin-Embedder-Policy "credentialless" always;

# Cache control cho SPA (ngăn lưu sensitive pages)
add_header Cache-Control "no-store, no-cache, must-revalidate" always;
```

### 3.4 Self-host Fonts (Verify)

Recent commit `feat(icons): self-host Material Icons` đã self-host icons. Font CSP chỉ cần `'self'`. Verify rằng Google Fonts không còn được load.

### 3.5 Strip Console Logs Production

Thêm vào `angular.json` build production:

```json
"configurations": {
  "production": {
    "optimization": {
      "scripts": true
    }
  }
}
```

Angular 19 production build mặc định đã remove `console.*` khi `optimization.scripts: true`.

### 3.6 CSP Report Endpoint

Backend cần thêm endpoint `POST /api/v1/security/csp-report` để nhận CSP violation reports. Endpoint này:
- Accept JSON body (the CSP report spec)
- Log vào audit trail
- Không trả về lỗi (luôn 204)

### 3.7 Files Changed (Phase 1)

| File | Change |
|------|--------|
| `src/Frontend/his-hope-app/nginx.conf` | CSP mới, headers mới, sub_filter |
| `src/Frontend/his-hope-app/angular.json` | Bật SRI |
| `src/Frontend/his-hope-app/src/index.html` | (có thể không đổi — nonce qua nginx) |

### 3.8 Rollback

```bash
# Revert nginx config
git checkout HEAD -- nginx.conf
# Restart nginx
docker compose restart frontend
```

---

## 4. Phase 2: Session + JWT Hardening

### 4.1 SessionService (Mới)

Tạo service quản lý session với idle timeout + absolute expiry.

**Interface:**

```typescript
// src/app/core/services/session.service.ts
interface SessionConfig {
  idleTimeoutMs: number;      // 900000 (15 phút)
  absoluteExpiryMs: number;   // 28800000 (8 giờ)
  warningBeforeMs: number;    // 60000 (60 giây cảnh báo)
}

@Injectable({ providedIn: 'root' })
export class SessionService {
  // Observables
  remainingTime$: Observable<number>;   // countdown đến khi force logout
  isWarning$: Observable<boolean>;       // đang trong warning period
  onExpired$: Observable<void>;          // emit khi session hết
  
  // Methods
  startTracking(): void;                  // gọi sau login
  resetIdleTimer(): void;                 // gọi mỗi lần user tương tác
  stopTracking(): void;                   // gọi khi logout
  getRemainingMs(): number;               // thời gian còn lại
}
```

**Implementation details:**

```typescript
private idleTimeout: number;
private absoluteExpiry: number;
private idleTimer: ReturnType<typeof setTimeout> | null = null;
private warningTimer: ReturnType<typeof setTimeout> | null = null;
private sessionStart: number = 0;
private lastActivity: number = 0;

// Lắng nghe user interaction events
private activity$ = merge(
  fromEvent(document, 'mousemove'),
  fromEvent(document, 'keydown'),
  fromEvent(document, 'click'),
  fromEvent(document, 'touchstart'),
  fromEvent(document, 'scroll'),
).pipe(
  throttleTime(1000),
  tap(() => this.resetIdleTimer()),
);
```

**Behavior:**
| Thời gian | Hành động |
|-----------|-----------|
| 12 phút idle | Show snackbar cảnh báo: "Phiên làm việc sắp hết (60s)" |
| 15 phút idle | Force logout → clear token → redirect /auth/login |
| 8 giờ từ login | Force logout (bất kể có hoạt động không) |

### 4.2 Memory-only JWT

Xóa sessionStorage storage, chuyển token vào memory (closure):

```typescript
// auth.service.ts — sửa
export class AuthService {
  private accessToken: string | null = null;  // chỉ trong RAM

  storeAccessToken(token: string): void {
    this.accessToken = token;
    // KHÔNG ghi vào sessionStorage nữa
  }

  getStoredAccessToken(): string | null {
    return this.accessToken;  // chỉ từ RAM
  }

  clearStoredAccessToken(): void {
    this.accessToken = null;
  }
}
```

**Hậu quả:**
- Reload tab → mất accessToken → gọi `/auth/refresh` với HttpOnly cookie
- Nếu refresh cookie còn hạn → tự động re-authenticate → nhận token mới
- Nếu refresh cookie hết → redirect login
- **Đây là behavior mong muốn** — security > convenience

### 4.3 JWT Permission Check via API

**Hiện tại:** `getUserPermissions()` decode JWT client-side từ sessionStorage và check local.

**Mới:** Permission check qua backend API với caching:

```typescript
// auth.service.ts — sửa
@Injectable({ providedIn: 'root' })
export class AuthService {
  private permissionCache = new Map<string, boolean>();
  private permissionCacheTimestamp = 0;
  private readonly PERMISSION_CACHE_TTL = 5 * 60 * 1000; // 5 phút

  hasPermission(permission: string): Observable<boolean> {
    // Check cache trước
    if (this.permissionCache.has(permission) && 
        Date.now() - this.permissionCacheTimestamp < this.PERMISSION_CACHE_TTL) {
      return of(this.permissionCache.get(permission)!);
    }
    
    // Gọi API backend
    return this.http.post<{ granted: boolean }>(
      `${this.baseUrl}/check-permission`,
      { permission },
      { withCredentials: true }
    ).pipe(
      map(res => res.granted),
      tap(granted => this.permissionCache.set(permission, granted)),
      catchError(() => of(false)),
    );
  }
}
```

**Backend endpoint cần thêm:** `POST /api/v1/auth/check-permission` — nhận `{ permission }`, trả `{ granted: boolean }`.

### 4.4 Sửa PermissionGuard

```typescript
// permission.guard.ts — sửa
private checkPermissions(route: ActivatedRouteSnapshot): Observable<boolean | UrlTree> {
  const requiredPermissions = route.data?.['permissions'] as string[] | undefined;
  
  if (!requiredPermissions?.length) {
    return this.authService.isLoggedIn().pipe(
      map(loggedIn => loggedIn || this.router.parseUrl('/auth/login')),
    );
  }

  // Check từng permission qua API thay vì decode local
  return forkJoin(
    requiredPermissions.map(p => this.authService.hasPermission(p))
  ).pipe(
    map(results => results.every(Boolean)),
    map(allowed => allowed || this.router.parseUrl('/access-denied')),
    catchError(() => of(this.router.parseUrl('/access-denied'))),
  );
}
```

### 4.5 Files Changed (Phase 2)

| File | Change |
|------|--------|
| `MỚI: src/app/core/services/session.service.ts` | Session timeout management |
| `src/app/core/services/auth.service.ts` | Memory-only token, API permission check |
| `src/app/core/guards/permission.guard.ts` | Sửa check permission qua API |
| `src/app/core/guards/role.guard.ts` | (kiểm tra — có thể giữ local nếu roles từ /me) |
| `src/app/core/interceptors/auth.interceptor.ts` | Đảm bảo refresh dùng memory token |
| `src/app/app.config.ts` | Provider cho SessionService |
| `src/app/features/auth/login/login.component.ts` | Gọi sessionService.startTracking() sau login |

### 4.6 Rollback

```bash
git checkout HEAD -- src/app/core/services/session.service.ts
git checkout HEAD -- src/app/core/services/auth.service.ts
# Remove new imports
```

---

## 5. Phase 3: Audit Trail + Error Handling

### 5.1 AuditService (Mới)

**Interface:**

```typescript
// src/app/core/services/audit.service.ts
type AuditAction = 
  | 'auth.login' | 'auth.logout' | 'auth.refresh'
  | 'data.view' | 'data.create' | 'data.update' | 'data.delete'
  | 'error.client' | 'error.server'
  | 'security.csp-violation'
  | 'navigation.change';

interface AuditEvent {
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

  log(action: AuditAction, details?: Record<string, unknown>): void;
  private flush(): void;  // gửi batch lên backend POST /api/v1/audit/events
}
```

**Data flow:**
1. App gọi `auditService.log('data.view', { patientId: '...' })`
2. Event được queue trong memory array
3. Khi đủ 10 events hoặc sau 30s → flush gửi `POST /api/v1/audit/events`
4. Nếu request thất bại → log + retry lần sau (không block app)

### 5.2 Audit Integration Points

Integrate audit vào các điểm:

| File | Audit event |
|------|-------------|
| `auth.service.ts` login() | `'auth.login'` — success/fail |
| `auth.service.ts` logout() | `'auth.logout'` — manual/idle/expiry |
| `error.interceptor.ts` | `'error.server'` — HTTP errors |
| `global-error-handler.ts` | `'error.client'` — client-side errors |
| `app.component.ts` | `'navigation.change'` — route changes |
| `*feature*.service.ts` | `'data.view'`, `'data.create'`, `'data.update'`, `'data.delete'` |

### 5.3 Error Info Sanitization

**Error Interceptor** — sanitize messages:

```typescript
// error.interceptor.ts — sửa
catchError((error: HttpErrorResponse) => {
  // Sanitize: không lộ path, method, stack
  const sanitizedMessage = this.sanitizeError(error);
  // Audit log gửi full context (cho backend)
  auditService.log('error.server', {
    status: error.status,
    url: req.url,
    correlationId: this.getCorrelationId(error),
    // KHÔNG gửi: error.message, error.stack, response body
  });
  // User chỉ thấy message đã sanitize
  this.showUserNotification(sanitizedMessage);
})
```

**Global Error Handler** — remove console.error trong production:

```typescript
// global-error-handler.ts — sửa
handleError(error: unknown): void {
  // Trong production, KHÔNG log ra console
  if (!environment.production) {
    console.error('[GlobalErrorHandler]', error);
  }
  // Audit log (gửi về backend an toàn)
  auditService.log('error.client', {
    type: error instanceof TypeError ? 'TypeError' : typeof error,
    correlationId: context.correlationId,
  });
  // User feedback
  this.showUserFeedback(error, context);
}
```

### 5.4 Navigation Audit

Trong `app.component.ts`:

```typescript
// app.component.ts
constructor() {
  this.router.events.pipe(
    filter(event => event instanceof NavigationEnd),
    withLatestFrom(this.authService.currentUser$),
    map(([event, user]) => ({
      action: 'navigation.change' as AuditAction,
      timestamp: Date.now(),
      userId: user?.id,
      details: { from: this.previousUrl, to: event.url },
    })),
  ).subscribe(event => this.auditService.log(event.action, event.details));
}
```

### 5.5 Files Changed (Phase 3)

| File | Change |
|------|--------|
| `MỚI: src/app/core/services/audit.service.ts` | Audit event queue + flush |
| `src/app/core/services/auth.service.ts` | Thêm audit calls |
| `src/app/core/interceptors/error.interceptor.ts` | Sanitize messages + audit |
| `src/app/core/errors/global-error-handler.ts` | Remove console.* production + audit |
| `src/app/app.component.ts` | Navigation audit |
| `src/app/app.config.ts` | Provider |

### 5.6 Rollback

```bash
git checkout HEAD -- src/app/core/services/audit.service.ts
git revert <error-interceptor-commit>
git revert <global-error-handler-commit>
```

---

## 6. Phase 4: Dependency Scan + CI/CD Gate

### 6.1 npm Audit CI Gate

Thêm vào `.github/workflows/ci.yml` (hoặc Tekton pipeline tương đương):

```yaml
- name: Security dependency audit
  run: |
    cd src/Frontend/his-hope-app
    npm audit --audit-level=high
  # Nếu có vulnerability high/critical → fail build
```

### 6.2 SRI Verification Gate

```yaml
- name: Verify Subresource Integrity
  run: |
    if grep -q 'integrity=' dist/his-hope-app/browser/en/index.html; then
      echo "✅ SRI present"
    else
      echo "❌ SRI missing — build failed"
      exit 1
    fi
```

### 6.3 ESLint Security Rules

Cập nhật `.eslintrc.json`:

```json
{
  "plugins": ["@angular-eslint", "no-secrets"],
  "rules": {
    "@angular-eslint/no-call-expression": "error",
    "@typescript-eslint/no-implied-eval": "error",
    "no-secrets/no-secrets": "error"
  }
}
```

### 6.4 Environment File Check

Add script check environment files không chứa secrets/hardcoded credentials:

```yaml
- name: Check for hardcoded secrets in environment
  run: |
    grep -rn "password\|secret\|key\|token" src/environments/ || true
```

### 6.5 Files Changed (Phase 4)

| File | Change |
|------|--------|
| `.github/workflows/ci.yml` (hoặc Tekton) | npm audit + SRI check step |
| `.eslintrc.json` | Security rules |

### 6.6 Rollback

```bash
git checkout HEAD -- .github/workflows/
git checkout HEAD -- .eslintrc.json
```

---

## 7. Files Changed (Summary)

### New Files

| # | File | Phase |
|---|------|-------|
| 1 | `src/app/core/services/session.service.ts` | P2 |
| 2 | `src/app/core/services/audit.service.ts` | P3 |

### Modified Files

| # | File | Phase |
|---|------|-------|
| 3 | `src/Frontend/his-hope-app/nginx.conf` | P1 |
| 4 | `src/Frontend/his-hope-app/angular.json` | P1 |
| 5 | `src/app/core/services/auth.service.ts` | P2, P3 |
| 6 | `src/app/core/guards/permission.guard.ts` | P2 |
| 7 | `src/app/core/interceptors/auth.interceptor.ts` | P2 |
| 8 | `src/app/core/interceptors/error.interceptor.ts` | P3 |
| 9 | `src/app/core/errors/global-error-handler.ts` | P3 |
| 10 | `src/app/app.component.ts` | P3 |
| 11 | `src/app/app.config.ts` | P2, P3 |
| 12 | `src/app/features/auth/login/login.component.ts` | P2 |
| 13 | `.eslintrc.json` | P4 |
| 14 | CI/CD config (`ci.yml` / Tekton) | P4 |

---

## 8. Rollback Plan

### Per-Phase Rollback

| Phase | Rollback Command | Impact |
|-------|-----------------|--------|
| P1 | `git checkout HEAD -- nginx.conf angular.json && docker compose restart frontend` | CSP fallback về `unsafe-inline` (như cũ) |
| P2 | `git checkout HEAD -- src/app/core/services/session.service.ts auth.service.ts permission.guard.ts auth.interceptor.ts` | Token về sessionStorage, session timeout tắt |
| P3 | `git checkout HEAD -- src/app/core/services/audit.service.ts error.interceptor.ts global-error-handler.ts app.component.ts` | Audit tắt, error messages lộ như cũ |
| P4 | `git checkout HEAD -- .github/ .eslintrc.json` | CI security gates tắt |

### Global Rollback

```bash
git revert <security-upgrade-commit>
```

---

## 9. Security Testing

### 9.1 Automated Tests

| Test | Tool | Phase |
|------|------|-------|
| CSP header presence & value | Playwright E2E | P1 |
| SRI integrity attribute check | Unit test (Jest) | P1 |
| Session idle timeout behavior | Jest (fake timers) | P2 |
| Memory-only token (token mất khi reload) | Playwright E2E | P2 |
| Permission guard fallback (API down) | Unit test | P2 |
| Audit event queuing & flush | Unit test | P3 |
| Error sanitization (no paths leaked) | Unit test | P3 |
| npm audit fails on vulnerability | CI mock | P4 |

### 9.2 Manual Verification

1. **CSP:** Mở browser console → verify không có CSP violation errors
2. **Headers:** `curl -I https://staging.his-hope.example.com | grep -i "content-security\|x-frame\|x-content"`
3. **Session:** Login → chờ 12 phút → verify warning snackbar → chờ 15 phút → verify force logout
4. **Audit:** Check backend `audit_events` table sau vài thao tác

---

## 10. Implementation Order

```
Phase 1 ──► Phase 2 ──► Phase 3 ──► Phase 4
  (nginx)      (TS)        (TS)        (CI)
     │           │           │           │
     ▼           ▼           ▼           ▼
  Deploy ✅   Deploy ✅   Deploy ✅   Deploy ✅
```

Mỗi phase deploy + verify độc lập trước khi sang phase tiếp theo.

---

*End of spec. Proceed to implementation plan.*
