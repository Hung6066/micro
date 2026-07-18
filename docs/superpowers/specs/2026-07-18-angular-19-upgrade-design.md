# Design Spec: Angular 19 Comprehensive Upgrade

**Date:** 2026-07-18  
**Status:** Draft — Awaiting user review  
**Author:** Lead System Architect  
**Target:** Angular 17.3.x → Angular 19.x  
**Strategy:** Foundation-First Incremental (Phased)

---

## 1. Executive Summary

Comprehensive upgrade of the His.Hope Angular frontend from Angular 17.3.x (NgModule-based, Webpack build, Karma tests, Material 2) to Angular 19.x (standalone components, esbuild, Jest, Material 3). The upgrade follows an incremental, 5-phase approach spanning ~17-23 days, with security verification at every phase.

### Key Decisions

| Decision | Choice | Rationale |
|---|---|---|
| Target version | **Angular 19** | Current stable, zoneless stable, M3 stable |
| Strategy | **Foundation-First Incremental** | Lowest risk, each phase independently verifiable |
| State management | **Keep NgRx, upgrade to v19** | Preserve existing patterns, minimize logic changes |
| Build system | **Webpack → esbuild/Vite (application builder)** | Faster builds, smaller bundles |
| Test framework | **Karma → Jest** | Modern, faster, better Stryker integration |
| Design system | **Material 2 → Material 3 tokens** | Design consistency, accessibility |
| Date library | **Moment.js → date-fns** | Smaller bundle, tree-shakeable |

### Scope

- 10 feature modules (Auth, Dashboard, Patients, Appointments, Clinical, Pharmacy, Lab, Billing, Admin, Reports)
- 3 NgRx stores (auth, patients, error)
- 10 core services + mock layer
- 3 guards, 2 interceptors, 2 directives
- 5 shared components
- i18n pipeline (English + Vietnamese)
- RUM monitoring (OpenTelemetry)
- nginx/Docker deployment

---

## 2. Architecture Overview

### Current State

```
Angular 17.3.x + TypeScript 5.4
├── Build: Webpack (@angular-devkit/build-angular:browser)
├── Bootstrap: platformBrowserDynamic().bootstrapModule(AppModule)
├── Modules: 1 AppModule + 10 Feature NgModules + 1 SharedModule
├── Standalone: 2 components + 2 directives (partial)
├── State: NgRx 17.2.x (StoreModule.forRoot, EffectsModule.forRoot)
├── UI: Angular Material 2 (M2), custom clinical theme
├── Test: Karma 6.4 + Jasmine 5.1 + Stryker 9.6
├── Dates: Moment.js + @angular/material-moment-adapter
├── Auth: @auth0/angular-jwt, class-based interceptors/guards
└── i18n: Angular built-in (English + Vietnamese xlf)
```

### Target State

```
Angular 19.x + TypeScript 5.6+
├── Build: esbuild/Vite (@angular-devkit/build-angular:application)
├── Bootstrap: bootstrapApplication(AppComponent, appConfig)
├── Modules: 0 NgModules — all standalone
├── Standalone: 100% components/directives/pipes
├── State: NgRx 19.x (provideStore, createActionGroup, functional effects)
├── UI: Angular Material 3 (M3), token-based clinical theme
├── Test: Jest + Jasmine + Stryker (Jest runner)
├── Dates: date-fns + custom DateAdapter
├── Auth: @auth0/angular-jwt, functional interceptors/guards
├── Template: @if/@for/@switch, @defer, inject(), signal inputs
└── Optional: Zoneless change detection
```

### Directory Structure (post-upgrade)

```
src/app/
├── app.config.ts              ← NEW: ApplicationConfig providers
├── app.routes.ts              ← NEW: top-level route config
├── app.component.ts           ← standalone: true
├── core/
│   ├── guards/                ← functional guards (auth, permission, role)
│   ├── interceptors/           ← functional interceptors (auth, error)
│   ├── directives/             ← standalone (unchanged)
│   ├── models/                 ← unchanged
│   └── services/               ← inject(), signal-based where beneficial
├── features/
│   ├── auth/                   ← standalone, auth.routes.ts
│   ├── dashboard/              ← standalone, dashboard.routes.ts
│   ├── patients/               ← standalone, patients.routes.ts
│   ├── appointments/           ← standalone
│   ├── clinical/               ← standalone
│   ├── pharmacy/               ← standalone
│   ├── lab/                    ← standalone
│   ├── billing/                ← standalone
│   ├── admin/                  ← standalone
│   └── reports/                ← standalone
├── shared/
│   └── components/             ← standalone, no SharedModule
├── store/
│   ├── auth/                   ← NgRx 19 (createActionGroup, functional effects)
│   ├── patients/               ← NgRx 19
│   └── error/                  ← NgRx 19
└── monitoring/                 ← unchanged
```

---

## 3. Phase 1: Tooling & Dependencies (3-4 days)

**Goal:** Upgrade all tooling without touching application code. Build must stay green.

### 3.1 Steps

| Step | Action | Duration |
|---|---|---|
| 1.1 | `ng update @angular/cli@19 @angular/core@19` + migration schematics | 1 day |
| 1.2 | `ng update @angular/material@19 @angular/cdk@19` + theme API check | 0.5 day |
| 1.3 | Build system: browser builder → application builder (esbuild) | 1 day |
| 1.4 | TypeScript ~5.4 → 5.6+ (ES2023, bundler moduleResolution, isolatedModules) | 0.5 day |
| 1.5 | `ng update @ngrx/store@19 @ngrx/effects@19 @ngrx/entity@19` | 0.5 day |
| 1.6 | Update auxiliary deps: @auth0/angular-jwt, @opentelemetry/*, web-vitals | 0.5 day |

### 3.2 Breaking Changes to Handle

| Package | Change | Mitigation |
|---|---|---|
| @angular/core | ngComponentOutlet bindings | Check directive usage |
| @angular/common | NgOptimizedImage width/height required | Audit all `<img>` tags |
| @angular/material | M2 deprecated | Keep M2 theme in Phase 1, migrate in Phase 4 |
| @angular/build | browser→application builder | Rewrite angular.json build section |
| TypeScript 5.6 | useDefineForClassFields false conflict | Handle class property decorators |

### 3.3 Verification

- `ng build --configuration production` passes
- `ng build --configuration production-vi` passes (i18n)
- `ng test --no-watch` all pass
- `ng lint` no new errors
- Bundle size not increased >10%
- App functional: login, all routes, auth flow
- Store devtools operational
- RUM traces sent to OTLP endpoint
- Docker build successful

---

## 4. Phase 2: Standalone Migration (5-7 days)

**Goal:** Eliminate all NgModules. 100% standalone. This is the highest-risk phase.

### 4.1 Migration Strategy: Bottom-Up

```
Shared components (no dependencies)
  → Core directives/services (already standalone)
    → Feature modules (simple → complex)
      → AppModule → bootstrapApplication
        → Delete SharedModule + AppModule
```

### 4.2 Feature Migration Order

| Batch | Modules | Components | Complexity |
|---|---|---|---|
| Batch 1 | Auth, Reports, Dashboard | 3 + 1 + 1 | Low |
| Batch 2 | Lab, Billing, Pharmacy, Clinical | 3 + 3 + 6 + 2 | Medium |
| Batch 3 | Appointments, Admin, Patients | 3 + 5 + 4 + 5 dialogs | High |

### 4.3 Migration Pattern (per module)

**Before:**
```
auth.module.ts          — NgModule with declarations + imports
auth-routing.module.ts  — RouterModule.forChild(routes)
login.component.ts      — @Component without standalone
```

**After:**
```
auth.routes.ts          — Routes array (export const)
login.component.ts      — @Component({ standalone: true, imports: [...] })
```
App routing: `{ path: 'auth', loadChildren: () => import('./auth.routes').then(m => m.AUTH_ROUTES) }`

### 4.4 Critical Changes

#### AppModule → bootstrapApplication

Create `app.config.ts`:
```typescript
export const appConfig: ApplicationConfig = {
  providers: [
    provideRouter(routes),
    provideHttpClient(withInterceptors([authInterceptor, errorInterceptor])),
    provideAnimations(),
    provideStore({ auth: authReducer, patients: patientsReducer, error: errorReducer }),
    provideEffects([AuthEffects, PatientsEffects]),
    provideStoreDevtools({ maxAge: 25 }),
    { provide: ErrorHandler, useClass: GlobalErrorHandler },
  ]
};
```

Update `main.ts`:
```typescript
bootstrapApplication(AppComponent, appConfig).catch(err => console.error(err));
```

#### SharedModule → Self-Importing Components

Each component imports exactly the Material modules it needs. SharedModule is deleted.

#### Guards & Interceptors

- Guards: Keep class-based initially, optional migration to functional (`CanActivateFn`)
- Interceptors: `HTTP_INTERCEPTORS` → `withInterceptors([...])` with functional interceptors

#### Directives

`HasPermissionDirective` and `HasRoleDirective` are already standalone — no changes needed.

### 4.5 Verification

- `ng build --configuration production` passes
- `ng test` all pass (update TestBed to import standalone components)
- All 26+ routes lazy-load correctly
- Auth flow: login → navigate → logout
- PermissionGuard redirects to /access-denied for unauthorized
- RoleGuard redirects for non-admin
- *hasPermission and *hasRole directives functional
- Store devtools display correct state
- i18n: Vietnamese renders correctly
- Zero `*.module.ts` in features/ and shared/
- SharedModule and AppModule deleted

---

## 5. Phase 3: Template Modernization (3-4 days)

**Goal:** Replace legacy template syntax with Angular 19 modern patterns.

### 5.1 Steps

| Step | Action | Duration |
|---|---|---|
| 3.1 | Control flow: *ngIf→@if/@else, *ngFor→@for/@empty, *ngSwitch→@switch | 1.5 days |
| 3.2 | Deferrable views: @defer for heavy components | 1 day |
| 3.3 | DI: constructor → inject() | 0.5 day |
| 3.4 | Signals: @Input→input(), @Output→output(), two-way→model() | 1 day |

### 5.2 Migration Patterns

**Control Flow:**
```html
<!-- Before -->
<div *ngIf="user$ | async as user; else loading">...</div>
<ng-template #loading><app-spinner></app-spinner></ng-template>

<!-- After -->
@if (user$ | async; as user) { ... }
@else { <app-spinner></app-spinner> }

<!-- Before -->
<div *ngFor="let p of patients; trackBy: trackById">...</div>

<!-- After -->
@for (p of patients; track p.id) { ... } @empty { <app-empty-state></app-empty-state> }
```

**Deferrable Views:**
```html
@defer (on viewport) {
  <app-dashboard-stats [stats]="stats()"></app-dashboard-stats>
} @loading (minimum 200ms) {
  <app-spinner message="Loading..."></app-spinner>
} @placeholder {
  <div class="skeleton-card"></div>
} @error {
  <app-empty-state icon="error" title="Failed to load"></app-empty-state>
}
```

**DI:**
```typescript
// Before: constructor(private store: Store, private service: PatientService) {}
// After:
private store = inject(Store);
private patientService = inject(PatientService);
```

**Signals:**
```typescript
// Before: @Input() patient!: Patient; @Output() edit = new EventEmitter<string>();
// After:
patient = input.required<Patient>();
edit = output<string>();
```

### 5.3 Defer Targets

| Location | Component | Trigger |
|---|---|---|
| Dashboard | `<app-dashboard-stats>` | on viewport |
| Dashboard | Bento grid row 2+ | on viewport |
| Patient workspace | Tab contents | on interaction |
| Admin | `<app-audit-logs>` | on viewport |
| Reports | Charts | on viewport |
| Appointments | Calendar view | on viewport |

### 5.4 Verification

- Zero `*ngIf`, `*ngFor`, `*ngSwitch` remaining (`grep -r` confirms)
- @empty blocks render on empty lists
- @defer blocks lazy-load on trigger
- inject() has no circular dependencies
- Signal inputs receive correct values
- output() emits correctly
- No unused constructors
- Bundle size decreased (control flow lighter than structural directives)

---

## 6. Phase 4: Design System — Material 3 (3-4 days)

**Goal:** Migrate from Material 2 theme to Material 3 token system with clinical design.

### 6.1 Steps

| Step | Action | Duration |
|---|---|---|
| 4.1 | M3 theme engine setup (clinical palette, typography, density tokens) | 1 day |
| 4.2 | Component migration (M2 mixins → M3 tokens) | 1 day |
| 4.3 | CSS custom properties → M3 sys.color tokens | 0.5 day |
| 4.4 | Accessibility audit (WCAG 2.1 AA) | 1 day |

### 6.2 M3 Clinical Palette

```
Primary:     #2F6B4A (clinical green, preserved from original)
Secondary:   #5B8C5A (soft sage)
Tertiary:    #3E6B8C (information blue, new for badges)
Error:       #C25450 (clinical alert red)
Neutral:     #767676
```

Theme type: light, density scale: -2 (compact for clinical workstations)

### 6.3 Component Migration Table

| M2 Pattern | M3 Replacement | Usage |
|---|---|---|
| `mat-raised-button` | `mat-filled-button` | Primary actions |
| `mat-stroked-button` | `mat-outlined-button` | Secondary actions |
| `mat-button` (no variant) | `mat-button` (text) | Tertiary actions |
| `mat-card` (flat custom) | `appearance="outlined"` | Lists, dashboard |
| `mat-form-field fill` | `appearance="outlined"` | All forms |
| Custom status badges | M3 sys.color tokens | Status/pill badges |

### 6.4 Accessibility Requirements (WCAG 2.1 AA)

| Requirement | Target |
|---|---|
| Color contrast (text) | ≥ 4.5:1 |
| Color contrast (large) | ≥ 3:1 |
| Focus indicators | 2px visible outline |
| Touch targets | ≥ 44×44px |
| Screen reader | aria-label on all icon buttons |
| Skip-to-content | Functional |
| Reduced motion | @media (prefers-reduced-motion) |

### 6.5 Verification

- Theme compiles without errors
- Consistent colors across all components
- Button variants correct (filled/outlined/text)
- Form fields styled as M3 outlined
- Cards styled as M3
- Status badges visually distinct
- Color contrast ≥ 4.5:1 (axe DevTools)
- Focus rings visible on all interactive elements
- Keyboard navigation: Tab, Enter, Escape, Arrow keys
- Screen reader reads correct labels
- Responsive: mobile, tablet, desktop
- No M2 theme imports remaining

---

## 7. Phase 5: Testing & Optimization (3-4 days)

**Goal:** Modernize test infrastructure, replace Moment.js, experiment with zoneless, optimize bundle/Lighthouse.

### 7.1 Steps

| Step | Action | Duration |
|---|---|---|
| 5.1 | Karma → Jest migration (jest-preset-angular, config, fix tests) | 1.5 days |
| 5.2 | Moment.js → date-fns (remove moment, custom DateAdapter, migrate all date logic) | 1 day |
| 5.3 | Zoneless change detection (provideZonelessChangeDetection, remove zone.js) | 0.5 day |
| 5.4 | Bundle size optimization + Lighthouse audit | 1 day |

### 7.2 Jest Configuration

```javascript
// jest.config.js
module.exports = {
  preset: 'jest-preset-angular',
  setupFilesAfterSetup: ['<rootDir>/setup-jest.ts'],
  testPathIgnorePatterns: ['/node_modules/', '/dist/'],
  moduleNameMapper: {
    '@core/(.*)': '<rootDir>/src/app/core/$1',
    '@shared/(.*)': '<rootDir>/src/app/shared/$1',
    '@features/(.*)': '<rootDir>/src/app/features/$1',
    '@env/(.*)': '<rootDir>/src/environments/$1',
    '@store/(.*)': '<rootDir>/src/app/store/$1',
    '@testing/(.*)': '<rootDir>/src/app/testing/$1',
  },
  coverageThreshold: { global: { branches: 75, functions: 80, lines: 85, statements: 85 } },
};
```

Stryker config updated to use Jest runner.

### 7.3 Moment.js → date-fns

- Remove: `moment`, `@angular/material-moment-adapter`
- Add: `date-fns` + custom `DateFnsDateAdapter` implementing `DateAdapter<Date>`
- Supports Vietnamese locale via `date-fns/locale/vi`
- Files requiring date migration: ~6 files, ~40 total date calls
- Pattern: `moment(d).format('DD/MM/YYYY')` → `format(new Date(d), 'dd/MM/yyyy', { locale: vi })`

### 7.4 Zoneless (Experimental)

- `provideZonelessChangeDetection()` in appConfig
- Remove `zone.js` from polyfills
- Migrate OnPush components to signal-based state (eliminate `markForCheck()` calls)
- Fix timing issues: `setTimeout` → Angular's `whenStable`

### 7.5 Bundle & Performance Targets

| Metric | Current (est.) | Target |
|---|---|---|
| main.js | ~180KB | < 80KB |
| vendor.js | ~450KB | < 250KB |
| polyfills.js | ~35KB | < 5KB (zoneless) |
| styles.css | ~65KB | < 40KB |
| **Total initial** | **~730KB** | **< 400KB** |

Optimizations:
1. Self-host Material Icons (remove Google Fonts CDN)
2. `NgOptimizedImage` on all images
3. `withPreloading(PreloadAllModules)` for route preloading
4. Inline critical CSS, minify everything

Lighthouse targets: Performance ≥ 95, Accessibility ≥ 95, Best Practices ≥ 95

### 7.6 Verification

- `ng test` (Jest): all pass, coverage ≥ 85%
- Stryker: mutation score ≥ 55%
- Zero imports from `moment` in codebase
- Dates display correctly in Vietnamese (Thứ 2, 18/07/2026)
- Date picker functional with date-fns adapter
- Zoneless: app runs without zone.js
- UI updates smooth, no flicker, no timing issues
- Source-map-explorer: no unused deps
- Initial bundle < 400KB gzipped
- Lighthouse: Performance ≥ 95, Accessibility ≥ 95
- Docker build successful
- Production build runs on nginx container

---

## 8. Security — Cross-Cutting (Continuous)

Security verification at EVERY phase exit gate. No phase merges with security regressions.

### 8.1 Security Architecture (Post-Upgrade)

```
Transport:    HTTPS + HSTS + mTLS (Linkerd) — unchanged
Auth:         JWT Bearer (RSA-signed) + HttpOnly refresh cookie
Identity:     Functional authInterceptor → Bearer token on all requests
AuthZ:        Functional authGuard, permissionGuard, roleGuard
UI:           *hasPermission, *hasRole structural directives
HTTP:         ErrorInterceptor → 401 refresh, 403 deny
Trace:        X-Correlation-ID header on all requests
Headers:      CSP, X-Frame-Options, X-Content-Type, HSTS, Referrer-Policy
Session:      Token in sessionStorage only (never localStorage)
```

### 8.2 Risk Matrix

| Phase | Risk | Severity | Mitigation |
|---|---|---|---|
| P1 | Supply chain CVE in new deps | High | npm audit + socket.dev scan |
| P2 | Functional guards/interceptors behavior differ from class | **Critical** | Exhaustive auth regression tests |
| P2 | `provideHttpClient()` misconfigured — missing interceptor | **Critical** | Verify Bearer token on every auth request |
| P2 | Lazy route missing guard | **Critical** | Test all 26 routes with/without auth |
| P3 | `@if` breaks `*hasPermission` directive | **Critical** | Verify all permission-gated UI elements |
| P4 | CSP blocks M3 inline styles | Medium | Update CSP in nginx.conf |
| P5 | Zoneless breaks token refresh queue timing | High | Signal-based refresh state |
| P5 | Self-host fonts — CSP font-src missing | Low | Update nginx CSP |

### 8.3 Per-Phase Security Verification

**Phase 1 — Dependency Audit:**
- `npm audit --audit-level=high`: 0 HIGH/CRITICAL
- `npm audit signatures`: all verified
- Socket.dev scan: no malware/supply chain risk

**Phase 2 — Auth Migration (Critical):**
- Login → JWT → sessionStorage ✓
- AuthInterceptor: Bearer on all requests (except auth endpoints) ✓
- 401 → token refresh → retry success ✓
- Concurrent 401 → single refresh, queue others ✓
- Refresh fail → clear token → redirect login ✓
- Expired token → refresh → retry → correct response ✓
- AuthGuard: not logged in → /auth/login ✓
- PermissionGuard: missing perm → /access-denied ✓
- RoleGuard: not admin → /access-denied ✓
- *hasPermission: shows/hides per permission ✓
- *hasRole: shows/hides per role ✓
- X-Correlation-ID on every request ✓
- No request sent without token when auth required ✓
- Verify via Playwright E2E + manual auth flow test

**Phase 3 — Template Security:**
- No XSS vectors in @if/@for blocks
- Trusted Types CSP enforced (no console errors)
- DomSanitizer bypass only when necessary
- @defer blocks do not expose sensitive data

**Phase 4 — CSP & Headers:**
- CSP allows M3 inline styles
- HSTS: max-age=31536000; includeSubDomains
- X-Frame-Options: DENY
- X-Content-Type-Options: nosniff
- Referrer-Policy: strict-origin-when-cross-origin
- Permissions-Policy: restrict unused APIs

**Phase 5 — Final Audit:**
- Token refresh works in zoneless mode
- No race condition between refresh and navigation
- Session timeout → auto logout
- No GET requests mutating state (CSRF)
- Rate limiting: 429 responses handled
- Audit log: login/logout events captured
- `npm audit --production`: 0 vulnerabilities
- OWASP ZAP scan: no HIGH/CRITICAL alerts

### 8.4 nginx.conf CSP Update (Phase 4)

```nginx
add_header Content-Security-Policy "
  default-src 'self';
  script-src 'self';
  style-src 'self' 'unsafe-inline';    # M3 requires inline styles
  img-src 'self' data:;
  font-src 'self' data:;               # Self-hosted fonts
  connect-src 'self' https://*;
  frame-ancestors 'none';
  base-uri 'self';
  form-action 'self';
  require-trusted-types-for 'script';
" always;
```

---

## 9. Risk Assessment & Mitigation

| Risk | Severity | Likelihood | Mitigation |
|---|---|---|---|
| Build breakage from NgModule removal | High | Medium | Feature-by-feature, not all-at-once |
| Lazy-loading route regression | High | Medium | E2E smoke test every route after each batch |
| NgRx selector/store breakage | Medium | Low | Unit tests on reducers/selectors before/after |
| Test infrastructure broken (Karma→Jest) | High | Medium | Parallel Jest setup before code changes |
| i18n translations lost during template rewrites | Medium | Low | Audit all @if/@for templates for i18n markers |
| Auth guard/interceptor functional migration | **Critical** | Medium | Exhaustive security regression suite |
| `*hasPermission` with @if control flow | High | Low | Verify all permission-gated UI manually |
| Bundle size regression | Low | Low | Budget checks in CI |
| Zoneless timing issues | Medium | Medium | Thorough manual testing of async flows |
| M3 theme visual regression | Low | Low | Visual snapshots comparison |

---

## 10. Timeline & Milestones

| Phase | Duration | Cumulative | Key Deliverable |
|---|---|---|---|
| P1: Tooling & Dependencies | 3-4 days | Day 4 | Build green on Angular 19 + esbuild |
| P2: Standalone Migration | 5-7 days | Day 11 | 0 NgModules, all standalone |
| P3: Template Modernization | 3-4 days | Day 15 | Modern control flow + inject() |
| P4: Design System (M3) | 3-4 days | Day 19 | Material 3 clinical theme |
| P5: Testing & Optimization | 3-4 days | Day 23 | Jest, date-fns, zoneless, <400KB bundle |
| **Total** | **17-23 days** | | |

Each phase produces one PR, merged to `develop` after verification.

Branch naming: `chore/angular-19-upgrade-p{1-5}`

---

## 11. Appendix: File Inventory

### Files to Create
- `src/app/app.config.ts`
- `src/app/app.routes.ts`
- `src/app/features/*/feature-name.routes.ts` (per module)
- `src/app/core/services/date-fns-adapter.ts`
- `jest.config.js`
- `setup-jest.ts`

### Files to Delete
- `src/app/app.module.ts`
- `src/app/app-routing.module.ts`
- `src/app/shared/shared.module.ts`
- `src/app/features/*/feature-name.module.ts` (10 files)
- `src/app/features/*/feature-name-routing.module.ts` (10 files)
- `karma.conf.js`

### Files to Significantly Modify
- `src/index.html`
- `src/main.ts`
- `src/styles/styles.scss`
- `src/styles/_theme.scss`
- `angular.json`
- `tsconfig.json`
- `tsconfig.app.json`
- `package.json`
- `Dockerfile`
- `nginx.conf`
- `stryker.conf.json`
- All component `.ts` files (standalone + inject + signal inputs)
- All component `.html` files (control flow syntax)
- All service `.ts` files (inject)
- All guard `.ts` files (functional)
- All interceptor `.ts` files (functional)
- All model `.ts` files with date logic

### Files with No/Minor Changes
- `src/app/core/models/*.ts` (interfaces unchanged)
- `src/app/core/services/mock/*` (mock data generators, date replacements only)
- `src/app/monitoring/*` (RUM unchanged)
- `src/app/testing/*` (update mock state providers)
- `src/environments/*` (unchanged)
- `src/locale/*` (unchanged)
- `proxy.conf.json` (unchanged)
