# Phase 5: Testing & Optimization — Angular 19 Upgrade

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Modernize test infrastructure (Karma→Jest), replace Moment.js with date-fns, enable zoneless, optimize bundle, complete remaining inject() and signal migrations. This is the final phase.

**Working directory:** `D:\AI\micro\src\Frontend\his-hope-app`

## Global Constraints

- Angular 19.2, esbuild, standalone, M3 design (from P1-4)
- All 451 tests must pass after each batch
- Build must pass after each batch
- No regressions in auth, routing, i18n
- Vietnamese locale fully supported

---

## Task 1: Branch + Snapshot

- [ ] `git checkout main && git pull && git checkout -b chore/angular-19-upgrade-p5`
- [ ] Baseline: `npm run build -- --configuration production && npm test -- --no-watch --browsers ChromeHeadless`
- [ ] Commit: `"chore: snapshot baseline before Phase 5 optimization"`

---

## Task 2: Karma → Jest Migration

Agent: @qa + @dotnet

Replace Karma test runner with Jest.

- [ ] **Step 1: Install Jest dependencies**
```pwsh
npm install --save-dev jest @types/jest jest-preset-angular
```

- [ ] **Step 2: Create jest.config.js at project root**
```javascript
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
};
```

- [ ] **Step 3: Create setup-jest.ts**
```typescript
import { setupZoneTestEnv } from 'jest-preset-angular/setup-env/zone';
setupZoneTestEnv();
```

- [ ] **Step 4: Update angular.json — test builder**
Change from `@angular-devkit/build-angular:karma` to `@angular-devkit/build-angular:jest`.
Remove `karmaConfig` option. Add Jest options if needed.

- [ ] **Step 5: Update package.json scripts**
```json
"test": "jest",
"test:watch": "jest --watch",
"test:coverage": "jest --coverage"
```

- [ ] **Step 6: Remove Karma dependencies**
```pwsh
npm uninstall karma karma-chrome-launcher karma-coverage karma-jasmine karma-jasmine-html-reporter jasmine-core @types/jasmine
```

- [ ] **Step 7: Delete karma.conf.js**

- [ ] **Step 8: Update Stryker config for Jest runner**
Read `stryker.conf.json`. Change `"testRunner": "karma"` → `"testRunner": "jest"`, add `"jest": { "projectType": "custom", "configFile": "jest.config.js" }`.

- [ ] **Step 9: Fix any Jest-specific test failures**
Many Angular tests use `fakeAsync`/`tick`/`flush` — Jest handles these differently. Common fixes:
- `TestBed.configureTestingModule` — same API
- `fakeAsync` — works but needs `zone.js/testing` import in setup
- `spyOn` — works natively in Jest

- [ ] **Step 10: Run tests**
```pwsh
npx jest --no-cache
```
Expected: All 451 pass. Fix any failures.

- [ ] **Step 11: Commit**
```pwsh
git add -A && git commit -m "test: migrate from Karma to Jest test runner"
```

---

## Task 3: Moment.js → date-fns

Agent: @dotnet

- [ ] **Step 1: Install date-fns**
```pwsh
npm install date-fns
npm uninstall moment @angular/material-moment-adapter
```

- [ ] **Step 2: Create custom DateAdapter**
Create `src/app/core/services/date-fns-adapter.ts` implementing `DateAdapter<Date>` from `@angular/material/core` using date-fns. Support Vietnamese locale via `date-fns/locale/vi`.

```typescript
import { Injectable } from '@angular/core';
import { DateAdapter } from '@angular/material/core';
import { format, parse, isValid, addDays, addMonths, addYears, differenceInCalendarDays, differenceInMonths, differenceInYears } from 'date-fns';
import { vi } from 'date-fns/locale';

@Injectable()
export class DateFnsAdapter extends DateAdapter<Date> {
  // Implement all required methods using date-fns
  // format(), parse(), isValid(), addCalendarDays(), etc.
  // Use vi locale for Vietnamese support
}
```

- [ ] **Step 3: Register adapter in app.config.ts**
```typescript
import { DateAdapter, MAT_DATE_LOCALE } from '@angular/material/core';
import { DateFnsAdapter } from '@core/services/date-fns-adapter';

// In providers array:
{ provide: DateAdapter, useClass: DateFnsAdapter },
{ provide: MAT_DATE_LOCALE, useValue: 'vi-VN' },
```

- [ ] **Step 4: Replace all moment() calls in application code**

Files to migrate (date logic):
- `src/app/features/appointments/appointment-form/appointment-form.component.ts` — `import moment from 'moment'` → `import { format, parse } from 'date-fns'`
- `src/app/features/appointments/appointment-detail/appointment-detail.component.ts`
- `src/app/features/appointments/appointment-list/appointment-list.component.ts`
- `src/app/core/models/patient.model.ts` — age calculation
- `src/app/features/patients/patient-form/patient-form.component.ts`
- `src/app/features/patients/patient-detail/patient-detail.component.ts`
- `src/app/features/dashboard/dashboard.component.ts`
- `src/app/core/services/mock/mock-data.ts` — all date generation

Pattern:
```typescript
// BEFORE
import moment from 'moment';
moment(date).format('DD/MM/YYYY')
moment().diff(moment(dob), 'years')

// AFTER
import { format, differenceInYears } from 'date-fns';
import { vi } from 'date-fns/locale';
format(new Date(date), 'dd/MM/yyyy', { locale: vi })
differenceInYears(new Date(), new Date(dob))
```

- [ ] **Step 5: Update mock data generators** for date-fns

- [ ] **Step 6: Build + test**
```pwsh
npm run build -- --configuration production && npx jest --no-cache
```

- [ ] **Step 7: Verify zero moment imports**
```pwsh
rg "from 'moment'" src/app/
```
Expected: Empty.

- [ ] **Step 8: Commit**

---

## Task 4: Zoneless Change Detection

Agent: @dotnet

- [ ] **Step 1: Enable zoneless in app.config.ts**
```typescript
import { provideZonelessChangeDetection } from '@angular/core';

// Add to providers:
provideZonelessChangeDetection(),
```

- [ ] **Step 2: Remove zone.js from angular.json polyfills**
In `angular.json`, under `build.options.polyfills`, remove `"zone.js"`.

- [ ] **Step 3: Remove zone.js from package.json**
```pwsh
npm uninstall zone.js
```

- [ ] **Step 4: Remove ChangeDetectorRef.markForCheck() calls**
Find all components with `this.cdr.markForCheck()` and remove them. In zoneless, signals and async pipe auto-trigger change detection.

```pwsh
rg "markForCheck\(\)" src/app/ --files-with-matches
```

For each file found, remove the `cdr` injection and `markForCheck()` calls. If the component uses `OnPush` + manual markForCheck, convert to signal-based state instead.

- [ ] **Step 5: Update AppComponent**
Currently: `this.cdr.markForCheck()` in subscription callbacks. Replace with signal-based approach or remove markForCheck.

- [ ] **Step 6: Build + test**
```pwsh
npm run build -- --configuration production && npx jest --no-cache
```

- [ ] **Step 7: Commit**

---

## Task 5: inject() Migration (deferred from P3)

Agent: @dotnet

Complete the inject() migration that was partially done in P3. ~62 files remain.

**Mechanical pattern:**
```typescript
// BEFORE
constructor(
  private store: Store,
  private service: PatientService,
  private fb: FormBuilder,
) {}

// AFTER
private store = inject(Store);
private patientService = inject(PatientService);
private fb = inject(FormBuilder);
```

**Handle edge cases:**
1. `FormBuilder` — inject normally
2. `ActivatedRoute` — inject normally
3. `MAT_DIALOG_DATA` — `private data = inject(MAT_DIALOG_DATA)` 
4. Guard constructors — move to inject()
5. Interceptor constructors — use `inject(Injector)` for lazy injection
6. Services that extend other services — careful with `super()` calls

- [ ] **Step 1: Find all remaining constructor DI files**
```pwsh
rg "constructor\(" src/app/ --files-with-matches | Where-Object { $_ -notmatch "spec.ts" -and $_ -notmatch "mock" }
```

- [ ] **Step 2: Migrate batch by batch (5-10 files each, test after each)**
  - Services (10 files)
  - Guards + Interceptors (5 files)
  - Shared components (5 files)
  - Feature components (40+ files)

- [ ] **Step 3: Final build + test**
- [ ] **Step 4: Commit**

---

## Task 6: Signal Inputs/Outputs (deferred from P3)

Agent: @dotnet

Convert remaining `@Input()/@Output()` to `input()/output()` where safe.

- [ ] **Step 1: Convert shared components** (5 files)
- [ ] **Step 2: Convert dialog components** (10 files: patient workspace dialogs + admin dialogs)
- [ ] **Step 3: Convert feature component simple inputs** (no setters/getters, no two-way binding)
- [ ] **Step 4: Update TS accesses: `this.prop` → `this.prop()` for signal inputs**
- [ ] **Step 5: Build + test**
- [ ] **Step 6: Commit**

---

## Task 7: Bundle Size & Performance

Agent: @dotnet

- [ ] **Step 1: Run source-map-explorer**
```pwsh
npx source-map-explorer dist/browser/*.js
```
Identify largest chunks.

- [ ] **Step 2: Tree-shaking audit**
- Remove unused imports
- Lazy-load route preloading strategy if not already

- [ ] **Step 3: NgOptimizedImage for all `<img>` tags**
- Add `width`/`height` attributes
- Add `ngSrc` instead of `src`
- Add `priority` for above-fold images

- [ ] **Step 4: Update budgets in angular.json**
Current: `maximumWarning: "2mb", maximumError: "3mb"`. After optimization, set tighter budgets.

- [ ] **Step 5: Build + compare bundle size with baseline**

- [ ] **Step 6: Commit**

---

## Task 8: Lighthouse Audit

Agent: @check-ui

- [ ] Run Lighthouse on production build
- [ ] Target: Performance ≥ 90, Accessibility ≥ 95, Best Practices ≥ 95
- [ ] Fix any issues

---

## Task 9: Final Cleanup & Verification

- [ ] `npm run build -- --configuration production` — SUCCESS
- [ ] `npm run build -- --configuration production-vi` — SUCCESS
- [ ] `npm test` (Jest) — 451+ PASS
- [ ] `npm run lint` — no errors
- [ ] `npm audit --audit-level=high` — 0 HIGH/CRITICAL (watch for known CVEs)
- [ ] Zero `from 'moment'` imports
- [ ] Zero `karma` dependencies
- [ ] Zone.js removed
- [ ] Zero `this.cdr.markForCheck()` calls
- [ ] All inject() migrated
- [ ] Vietnamese i18n works
- [ ] Docker build passes

- [ ] Push + merge to main
