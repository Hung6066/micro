# ADR-014: Standardize Frontend Apps on admin-app Patterns

**Status:** Proposed
**Date:** 2026-07-30
**Author:** Pipeline Orchestrator / Frontend Standardization Refactor

## Context

His.Hope has three Angular applications and one shared library:

| App/Library | Path | Role |
|---|---|---|
| **admin-app** | `admin-app/` | Admin console (reference standard) |
| **his-hope-app** | `src/Frontend/his-hope-app/` | Primary clinical SPA with NgRx |
| **dashboard-app** | `dashboard-app/` | Operations dashboard with service-based state |
| **frontend-foundation** | `shared/frontend-foundation/` | Shared UI, auth, http, i18n library |

The admin-app was built as the cleanest implementation, using functional guards, foundation interceptors, flat `loadComponent` lazy loading, and foundation's error handling. The his-hope-app and dashboard-app have diverged in guards, interceptors, error handling, and routing patterns. Standardizing on admin-app patterns reduces duplication, simplifies onboarding, and ensures consistent behavior.

## Decision

### P0 — Guards: Migrate his-hope-app to Functional Guards

**his-hope-app currently has three class-based guards:**
- `AuthGuard` (19 lines) — injects `AuthService` + `OidcSecurityService`, implements `CanActivate`
- `RoleGuard` (74 lines) — checks `route.data.roles`, implements `CanActivate` + `CanActivateChild`
- `PermissionGuard` (74 lines) — checks `route.data.permissions`, implements `CanActivate` + `CanActivateChild`

**admin-app uses a single functional guard (15 lines):**
```typescript
export const authGuard = (): Observable<boolean | UrlTree> => {
  const authService = inject(AuthService);
  const router = inject(Router);
  return authService.checkAuth().pipe(
    switchMap(() => authService.isAuthenticated$),
    map((isAuth) => (isAuth ? true : router.parseUrl("/auth/login"))),
  );
};
```

**Decision:** Convert all three class-based guards to functional guards:

1. **authGuard** — Migrate directly to admin-app pattern. Replace `filter(isAuth => isAuth)` (which hangs navigation) with `map` returning `UrlTree` for unauthenticated case. This is the known PermissionGuard `take(1)` / `filter` gotcha (see `docs/knowledge/`).

2. **roleGuard** — Convert to functional guard:
   ```typescript
   export const roleGuard = (route: ActivatedRouteSnapshot, state: RouterStateSnapshot): Observable<boolean | UrlTree> => { ... }
   ```
   Check `route.data['roles']`, redirect to `/access-denied` on failure.

3. **permissionGuard** — Convert to functional guard using same pattern. Check `route.data['permissions']` via `AuthService.hasPermission()`.

**Rationale:** Functional guards are the Angular 15+ idiomatic pattern. They reduce class boilerplate (`@Injectable`, `CanActivate` interface, constructor injection) by 50-70%. Both admin-app and dashboard-app already use this pattern.

### P0 — App Config: Replace Custom Interceptors/Error Handler with Foundation

**his-hope-app currently registers:**
- `ErrorInterceptor` (133-line class-based interceptor)
- `GlobalErrorHandler` (167-line class-based error handler with NgRx integration)
- `csrfInterceptor` (19-line functional interceptor)
- `authInterceptor` (functional, wraps foundation)

**Foundation already exports:**
- `hisHopeErrorInterceptor` — retries idempotent GET/HEAD on transient failures, reports all terminal failures to `HisHopeErrorReportingService`
- `hisHopeCorrelationIdInterceptor` — stamps every request with correlation ID
- `HisHopeGlobalErrorHandler` — catches uncaught errors, reports them, shows toast
- `HisHopeErrorReportingService` — configurable error sink (signal-based, can wire to backend)
- `createHisHopeBearerTokenInterceptor()` — factory for auth token attachment

**Decision:** Replace his-hope-app's custom error handling with foundation equivalents:

1. **ErrorInterceptor → hisHopeErrorInterceptor + hisHopeCorrelationIdInterceptor** — Foundation's interceptor handles retry, reporting, and correlation IDs generically. The custom `ErrorInterceptor` duplicates this with audit logging that can be wired via `HisHopeErrorReportingService.configure()`.

2. **GlobalErrorHandler → HisHopeGlobalErrorHandler** — Foundation's handler is simpler (37 lines vs 167). The NgRx dispatch (`captureError`, `clearError`) can be wired via `HisHopeErrorReportingService.configure()`.

3. **csrfInterceptor → Remove** — In OIDC flows with `angular-auth-oidc-client`, CSRF is handled differently. Verify no endpoints require the custom CSRF cookie before removal.

4. **authInterceptor → Keep** — Already wraps `createHisHopeBearerTokenInterceptor` from foundation. This is the correct pattern.

### P0 — Dashboard Component: Extract Inline Template/Styles + Use Foundation Metric Cards

**Current state:** `dashboard.component.ts` is 792 lines with:
- 262-line inline Angular template
- 340-line inline SCSS styles
- 189-line TypeScript class
- 6 raw `mat-card` stat cards with hardcoded Vietnamese strings

**Foundation provides:** `HisHopeMetricCardComponent` (66 lines) with `icon`, `label`, `value`, `link`, `action`, `tone` inputs.

**Decision:**
1. Extract template → `dashboard.component.html`
2. Extract styles → `dashboard.component.scss`
3. Replace 6 stat cards with `hh-metric-card` components
4. Keep all other features (search, recent patients, encounters table, appointments table)

### P1 — Route Loading: Standardize on `loadComponent`

**his-hope-app currently uses `loadChildren` with barrel-exported route arrays:**
- Each feature domain has a `*.routes.ts` file exporting a named routes constant
- `loadChildren: () => import('@features/patients/patients.routes').then(m => m.PATIENT_ROUTES)`

**admin-app uses flat `loadComponent`:**
- `loadComponent: () => import('./features/clients/clients-page.component').then(m => m.ClientsPageComponent)`

**Decision:** Migrate his-hope-app routes to `loadComponent` where possible:
- Each top-level route loads its page component directly
- Keep feature-internal child routes (e.g., `/patients/:id/workspace`) within the feature component's routing
- Auth routes (login, callback, silent-refresh) migrate to flat `loadComponent`
- This removes 8+ separate route files (one per feature domain)

### P1 — i18n: Align Vietnamese Strings with Foundation

**Dashboard has hardcoded Vietnamese strings:** "Xin chào", "Tổng quan bệnh viện", "Tổng bệnh nhân", "Lịch hẹn hôm nay", etc.

**Foundation provides:** `HisHopeI18nService` with `vi-VN` and `en` dictionaries, `HisHopeTranslatePipe`, `HisHopeLocalizationApiService`.

**Decision:** Replace hardcoded strings with foundation's i18n:
1. Add dashboard-specific translation keys to foundation dictionaries if not already present
2. Use `HisHopeTranslatePipe` in templates
3. Register `HisHopeI18nService` + `HisHopeLocalizationApiService` in his-hope-app config

### P1 — Error Handling: Standardize Foundation Reporting

**Decision:** Use `HisHopeErrorReportingService.configure()` to bridge foundation's error reporting with his-hope-app's NgRx store for backward compatibility.

### P2 — Component Naming

**Decision:** Standardize on:
- Selector prefix: `app-` for app-specific components, `hh-` for foundation components
- Component class: PascalCase (already followed)
- File names: kebab-case (already followed)
- No change needed — both apps already follow this convention.

## Consequences

### Positive
- ~300 lines of duplicated guard/interceptor/error-handler code removed
- Consistent patterns across all 3 apps → easier onboarding and code review
- Dashboard maintainability significantly improved (792-line monolith → separated files)
- Localization-ready dashboard (currently hardcoded Vietnamese)
- Foundation library gets more usage → better tested, fewer edge-case bugs

### Negative
- Guard migration is risky — auth flow must be verified end-to-end
- Error handler replacement may miss NgRx-specific behavior (store dispatch)
- csrfInterceptor removal requires verification that no endpoints depend on CSRF cookie
- Dashboard template extraction may introduce visual regressions

### Neutral
- All unit tests must be updated to match new patterns
- Route data keys must align with functional guard expectations

## Related
- admin-app (reference standard — DO NOT MODIFY)
- frontend-foundation shared library
- dashboard-app (already partially aligned)
- Known gotcha: `docs/knowledge/` — PermissionGuard `take(1)` / `filter` pattern causes hung navigation
