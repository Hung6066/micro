# Frontend Standardization Patterns

> Auto-generated from ADR-014. Captures the canonical patterns for His.Hope Angular apps.

## Guards — Always Functional

All apps MUST use functional guards (not class-based `@Injectable()` guards).

### authGuard (Authentication Check)

```typescript
import { inject } from "@angular/core";
import { Router, UrlTree } from "@angular/router";
import { Observable, map, switchMap } from "rxjs";
import { AuthService } from "../services/auth.service";

export const authGuard = (): Observable<boolean | UrlTree> => {
  const authService = inject(AuthService);
  const router = inject(Router);
  return authService.checkAuth().pipe(
    switchMap(() => authService.isAuthenticated$),
    map((isAuth) => (isAuth ? true : router.parseUrl("/auth/login"))),
  );
};
```

**Critical rule:** Guards must emit on EVERY path. Using `filter(isAuth => isAuth)` without a fallback emission causes hung navigation. Always use `map` with `UrlTree` for the unauthenticated case.

### roleGuard (Role Check)

Read `route.data['roles']`, redirect to `/access-denied` if user lacks required role.

### permissionGuard (Permission Check)

Read `route.data['permissions']`, redirect to `/access-denied` if user lacks required permissions.

## Interceptors — Always Foundation

Apps MUST use foundation interceptors, not custom implementations:
- `hisHopeCorrelationIdInterceptor` — correlation ID stamping
- `hisHopeErrorInterceptor` — retry + error reporting
- `hisHopeInternationalizationInterceptor` — locale header
- `createHisHopeBearerTokenInterceptor()` — auth token attachment

## Error Handling — Always Foundation

Apps MUST register `HisHopeGlobalErrorHandler` as the `ErrorHandler`:
```typescript
{ provide: ErrorHandler, useClass: HisHopeGlobalErrorHandler }
```

For NgRx-integrated apps, wire `HisHopeErrorReportingService.configure()` to bridge foundation errors to the store.

## Route Loading — Prefer `loadComponent`

Use flat `loadComponent` for top-level routes. Reserve `loadChildren` only for feature-internal child routing.

```typescript
{
  path: "patients",
  canActivate: [authGuard, permissionGuard],
  data: { permissions: ["patients.view"] },
  loadComponent: () => import("./features/patients/patients-page.component").then(m => m.PatientsPageComponent),
}
```

## Component Templates — Always External

No inline templates or styles in feature components. Use `templateUrl` and `styleUrls`.

## i18n — Always Foundation

No hardcoded user-facing strings. Use `HisHopeI18nService` + `translate` pipe.

## Known Gotchas

1. **PermissionGuard `filter` hang**: The class-based `AuthGuard` in his-hope-app uses `filter(isAuth => isAuth)` — this causes the router navigation to hang forever when unauthenticated because `filter` doesn't emit. Must use `map` with `UrlTree`.

2. **take(1) in guards**: Guards that subscribe with `take(1)` may resolve before auth state is hydrated. Use `switchMap` to wait for state.
