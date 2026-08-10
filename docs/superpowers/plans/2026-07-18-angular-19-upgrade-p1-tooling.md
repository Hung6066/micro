# Phase 1: Tooling & Dependencies — Angular 19 Upgrade

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Upgrade Angular CLI, Core, Material, Build, TypeScript, and NgRx from 17.x to 19.x without changing any application code. Build must stay green.

**Architecture:** Incremental tooling upgrade preserving all existing NgModule structure, applying Angular CLI migrations automatically, then switching build from Webpack to esbuild. Each task ends with `ng build` and `ng test` passing.

**Tech Stack:** Angular 19.x, TypeScript 5.6, esbuild (application builder), NgRx 19.x, Karma 6.4 (unchanged in P1)

## Global Constraints

- Angular version: exactly 19.x (latest patch)
- TypeScript: 5.6.x
- NgRx: 19.x (latest patch)
- Build builder: `@angular-devkit/build-angular:application` (esbuild)
- Target: ES2023, Module: ES2023, moduleResolution: bundler
- No application code changes (only config + dependencies)
- `ng build --configuration production` must pass after each task
- `ng test` must pass after each task
- `ng lint` must pass after each task
- Working directory: `D:\AI\micro\src\Frontend\his-hope-app`

---

### Task 1: Snapshot baseline + create branch

**Files:**
- No file changes — baseline snapshot only

- [ ] **Step 1: Create upgrade branch**

```pwsh
git checkout -b chore/angular-19-upgrade-p1
```

Expected: Switched to new branch `chore/angular-19-upgrade-p1`

- [ ] **Step 2: Record baseline build stats**

```pwsh
npm run build -- --configuration production
```

Expected: Build succeeds. Note the output sizes from the console log.

- [ ] **Step 3: Run baseline tests**

```pwsh
npm test -- --no-watch --browsers ChromeHeadless
```

Expected: All tests pass. Note the count (e.g., "Executed 45 of 45 SUCCESS").

- [ ] **Step 4: Snapshot dependencies**

```pwsh
npm ls --depth=0
```

Expected: Lists all current versions. Copy output for reference.

- [ ] **Step 5: Commit baseline snapshot**

```pwsh
git add -A && git commit -m "chore: snapshot baseline before Angular 19 upgrade"
```

---

### Task 2: Angular CLI 17 → 19 + Core packages

**Files:**
- Modify: `package.json` (all @angular/* deps: 17.x → 19.x)
- Modify: `angular.json` (CLI schema migration if any)
- Modify: `tsconfig.json` (possible new defaults)
- Modify: `src/main.ts` (if migration touches it)

**Interfaces:**
- Produces: All @angular/* packages at 19.x. Build passing on browser builder (not yet application builder).

- [ ] **Step 1: Run Angular CLI update to v19**

```pwsh
npx ng update @angular/cli@19 @angular/core@19 --allow-dirty --force
```

Expected: Migration runs. Watch for errors in console output. May prompt for confirmation on breaking changes.

- [ ] **Step 2: Run remaining Angular package updates**

```pwsh
npx ng update @angular/animations@19 @angular/common@19 @angular/compiler@19 @angular/forms@19 @angular/platform-browser@19 @angular/platform-browser-dynamic@19 @angular/router@19 @angular/localize@19 --allow-dirty --force
```

Expected: All Angular packages updated to 19.x in package.json.

- [ ] **Step 3: Update @angular-devkit/build-angular to v19**

```pwsh
npx ng update @angular-devkit/build-angular@19 --allow-dirty --force
```

Expected: Build-angular updated. Check angular.json for any schema changes.

- [ ] **Step 4: Update @angular/compiler-cli**

```pwsh
npm install --save-dev @angular/compiler-cli@^19.0.0
```

Expected: Compiler CLI at 19.x in devDependencies.

- [ ] **Step 5: Verify package.json versions**

Read `package.json`. Verify these exact ranges:
- `@angular/core: ^19.0.0`
- `@angular/cli: ^19.0.0`
- `@angular/material: ^17.3.0` (still old — Task 3 updates this)
- `@angular-devkit/build-angular: ^19.0.0`
- All other @angular/*: `^19.0.0`

- [ ] **Step 6: Build with old browser builder (still webpack)**

```pwsh
npm run build -- --configuration production
```

Expected: Build succeeds. If there are TypeScript errors about Angular 19 API changes, note them for later tasks (we are NOT fixing app code yet). If build fails with migration-related errors, fix config only.

- [ ] **Step 7: Run tests (still Karma)**

```pwsh
npm test -- --no-watch --browsers ChromeHeadless
```

Expected: Tests pass. If any fail due to Angular 19 TestBed changes, note for Phase 2.

- [ ] **Step 8: Run lint**

```pwsh
npm run lint
```

Expected: No new errors.

- [ ] **Step 9: Commit**

```pwsh
git add package.json package-lock.json angular.json tsconfig.json tsconfig.spec.json && git commit -m "chore: upgrade Angular CLI and core packages to v19"
```

---

### Task 3: Angular Material + CDK 17 → 19

**Files:**
- Modify: `package.json` (@angular/material, @angular/cdk, @angular/material-moment-adapter: 17.x → 19.x)

- [ ] **Step 1: Update Material and CDK**

```pwsh
npx ng update @angular/material@19 @angular/cdk@19 --allow-dirty --force
```

Expected: Material and CDK updated. May show deprecation warnings about M2 theme API (accept — we migrate in Phase 4).

- [ ] **Step 2: Update material-moment-adapter**

```pwsh
npm install @angular/material-moment-adapter@^19.0.0
```

Expected: Moment adapter at 19.x.

- [ ] **Step 3: Verify build with Material 19**

```pwsh
npm run build -- --configuration production
```

Expected: Build succeeds. All Material components still compile with M2 theme. Watch for deprecation warnings — these are acceptable.

- [ ] **Step 4: Visual smoke test (manual)**

Run `npm start`. Open browser at http://localhost:4200.
- Login page renders correctly
- Material form fields, buttons, cards render
- No visual breakage from M2 deprecation

- [ ] **Step 5: Run tests**

```pwsh
npm test -- --no-watch --browsers ChromeHeadless
```

Expected: All pass.

- [ ] **Step 6: Commit**

```pwsh
git add package.json package-lock.json && git commit -m "chore: upgrade Angular Material and CDK to v19"
```

---

### Task 4: Build System — Webpack → esbuild (Application Builder)

**Files:**
- Modify: `angular.json` (builder: browser → application, update config structure)
- Modify: `package.json` (remove browser-specific polyfills if any)
- Modify: `tsconfig.json` (isolatedModules, moduleResolution)

**Interfaces:**
- Consumes: Angular 19 CLI (from Task 2)
- Produces: esbuild-based build with `@angular-devkit/build-angular:application`. Zero Webpack.

- [ ] **Step 1: Switch builder in angular.json**

Read `angular.json` line 22: `"builder": "@angular-devkit/build-angular:browser"`

Edit to:
```json
"builder": "@angular-devkit/build-angular:application",
```

- [ ] **Step 2: Update build options for application builder**

The `application` builder has a different options schema. Replace the `build.options` section.

Read `angular.json` lines 23-35. Replace with:

```json
"options": {
  "outputPath": "dist",
  "index": "src/index.html",
  "browser": "src/main.ts",
  "polyfills": ["zone.js"],
  "tsConfig": "tsconfig.json",
  "assets": ["src/favicon.ico", "src/assets"],
  "styles": [
    "@angular/material/prebuilt-themes/indigo-pink.css",
    "src/styles/styles.scss"
  ],
  "scripts": []
},
```

Key changes: `"main"` → `"browser"`. The rest stays the same.

- [ ] **Step 3: Update test builder for esbuild**

Read `angular.json` line 74: `"builder": "@angular-devkit/build-angular:karma"`

The karma builder still works. However, update the polyfills for the test target. Read lines 76-77:

Change from:
```json
"polyfills": ["zone.js", "zone.js/testing"],
```
To:
```json
"polyfills": ["zone.js"],
```

The `zone.js/testing` polyfill is no longer needed in Angular 19. The test environment auto-configures it.

- [ ] **Step 4: Update tsconfig.json for esbuild compatibility**

Read `tsconfig.json`. esbuild requires:
- `isolatedModules: true`
- `moduleResolution: "bundler"` (replaces `"node"`)

Edit `tsconfig.json` lines 17-19:
```json
"moduleResolution": "bundler",
"isolatedModules": true,
```

Remove line 17 `"moduleResolution": "node"` and replace. Add `"isolatedModules": true` after `importHelpers`.

The full compilerOptions block should now include:
```json
"compilerOptions": {
  "baseUrl": "./",
  "outDir": "./dist/out-tsc",
  "forceConsistentCasingInFileNames": true,
  "strict": true,
  "noImplicitOverride": true,
  "noPropertyAccessFromIndexSignature": true,
  "noImplicitReturns": true,
  "noFallthroughCasesInSwitch": true,
  "skipLibCheck": true,
  "sourceMap": true,
  "declaration": false,
  "downlevelIteration": true,
  "experimentalDecorators": true,
  "importHelpers": true,
  "moduleResolution": "bundler",
  "isolatedModules": true,
  "target": "ES2023",
  "module": "ES2023",
  "useDefineForClassFields": false,
  "lib": ["ES2023", "dom"],
  "paths": {
    "@core/*": ["src/app/core/*"],
    "@shared/*": ["src/app/shared/*"],
    "@features/*": ["src/app/features/*"],
    "@env/*": ["src/environments/*"],
    "@store/*": ["src/app/store/*"],
    "@testing/*": ["src/app/testing/*"]
  }
},
```

Note: `target` and `module` changed from `ES2022` to `ES2023`.

- [ ] **Step 5: Update production configuration budgets**

The `application` builder produces different chunk names. Update the budgets in `angular.json` lines 38-40 and 45-47.

At both locations, replace:
```json
"budgets": [
  { "type": "initial", "maximumWarning": "1mb", "maximumError": "2mb" }
],
```
With:
```json
"budgets": [
  { "type": "initial", "maximumWarning": "500kb", "maximumError": "1mb" },
  { "type": "anyComponentStyle", "maximumWarning": "10kb", "maximumError": "20kb" }
],
```

Tighter budgets since esbuild produces smaller output.

- [ ] **Step 6: Update serve builder for application builder**

Read `angular.json` lines 62-72. The serve builder stays as `@angular-devkit/build-angular:dev-server`, but the `buildTarget` references need to use the new build config.

No changes needed — the target names stay the same (`his-hope-app:build:production`).

But add `"prebundle"` option for faster dev:

```json
"serve": {
  "builder": "@angular-devkit/build-angular:dev-server",
  "configurations": {
    "production": { "buildTarget": "his-hope-app:build:production" },
    "development": { "buildTarget": "his-hope-app:build:development" }
  },
  "defaultConfiguration": "development",
  "options": {
    "proxyConfig": "proxy.conf.json"
  }
},
```

- [ ] **Step 7: Add production optimization settings to application builder**

The `application` builder has different optimization keys. Update the `production` configuration to include full optimization:

```json
"production": {
  "budgets": [
    { "type": "initial", "maximumWarning": "500kb", "maximumError": "1mb" },
    { "type": "anyComponentStyle", "maximumWarning": "10kb", "maximumError": "20kb" }
  ],
  "outputHashing": "all",
  "localize": ["en"],
  "optimization": {
    "scripts": true,
    "styles": {
      "minify": true,
      "inlineCritical": true
    },
    "fonts": {
      "inline": true
    }
  }
},
```

- [ ] **Step 8: First build with esbuild**

```pwsh
npm run build -- --configuration production
```

Expected: Build succeeds with esbuild. If there are errors about `isolatedModules`, fix any `const enum` or namespace imports in application code. If build fails with esbuild-specific errors, report them for fixing.

- [ ] **Step 9: Compare bundle sizes with baseline**

```pwsh
npm run build -- --configuration production
```

Note: esbuild output is typically 20-40% smaller. Compare with Task 1 baseline numbers.

- [ ] **Step 10: Run tests**

```pwsh
npm test -- --no-watch --browsers ChromeHeadless
```

Expected: All pass.

- [ ] **Step 11: Commit**

```pwsh
git add angular.json tsconfig.json && git commit -m "chore: switch build from webpack to esbuild application builder"
```

---

### Task 5: TypeScript 5.4 → 5.6+

**Files:**
- Modify: `package.json` (typescript: ~5.4.0 → ~5.6.0)
- Modify: `tsconfig.json` (already done in Task 4, verify)
- Modify: `tsconfig.spec.json` (update types if needed)

- [ ] **Step 1: Install TypeScript 5.6**

```pwsh
npm install --save-dev typescript@~5.6.0
```

Expected: TypeScript updated to 5.6.x in devDependencies.

- [ ] **Step 2: Update tsconfig.spec.json**

Read `tsconfig.spec.json`. It currently has `"types": ["jasmine"]`. TypeScript 5.6 may need the jasmine types updated.

```pwsh
npm install --save-dev @types/jasmine@~5.1.0
```

(Verify this is already installed from package.json — it is, at ~5.1.0. No change needed.)

- [ ] **Step 3: Run TypeScript compiler check**

```pwsh
npx tsc --noEmit
```

Expected: TypeScript compiles all files without errors. If new strict errors appear, note them:
- Possible errors with `useDefineForClassFields: false` and class property decorators — acceptable, we handle in Phase 2
- Possible `noPropertyAccessFromIndexSignature` new violations — fix if trivial

- [ ] **Step 4: Verify build**

```pwsh
npm run build -- --configuration production
```

Expected: Build succeeds.

- [ ] **Step 5: Verify tests**

```pwsh
npm test -- --no-watch --browsers ChromeHeadless
```

Expected: All pass.

- [ ] **Step 6: Commit**

```pwsh
git add package.json package-lock.json && git commit -m "chore: upgrade TypeScript to 5.6"
```

---

### Task 6: NgRx 17 → 19

**Files:**
- Modify: `package.json` (@ngrx/*: 17.2.0 → 19.x)

**Interfaces:**
- Produces: NgRx 19.x packages installed. Store still using `StoreModule.forRoot()` (migration to `provideStore()` in Phase 2).

- [ ] **Step 1: Update NgRx packages**

```pwsh
npm install @ngrx/store@^19.0.0 @ngrx/effects@^19.0.0 @ngrx/entity@^19.0.0 @ngrx/store-devtools@^19.0.0
```

Expected: All NgRx packages at 19.x.

- [ ] **Step 2: Verify NgRx changelog for breaking changes**

Key things to check from NgRx 17→19:
- `createAction` still works (deprecated in favor of `createActionGroup`, but not removed)
- `StoreModule.forRoot()` still works (deprecated in favor of `provideStore()`, but not removed)
- Class-based effects still work (deprecated in favor of functional `createEffect`, but not removed)

We do NOT migrate the store patterns in Phase 1 — only ensure runtime compatibility.

- [ ] **Step 3: Build verification**

```pwsh
npm run build -- --configuration production
```

Expected: Build succeeds. NgRx actions/reducers/effects compile without errors.

- [ ] **Step 4: Test verification**

```pwsh
npm test -- --no-watch --browsers ChromeHeadless
```

Expected: All tests pass. MockStore from NgRx 19 should be backward-compatible.

- [ ] **Step 5: Commit**

```pwsh
git add package.json package-lock.json && git commit -m "chore: upgrade NgRx to v19"
```

---

### Task 7: Auxiliary Dependencies + Final Verification

**Files:**
- Modify: `package.json` (@auth0/angular-jwt, @opentelemetry/*, web-vitals — verify compatible)

- [ ] **Step 1: Check @auth0/angular-jwt compatibility**

```pwsh
npm ls @auth0/angular-jwt
```

Expected: Currently `^5.2.0`. Check if this version works with Angular 19.

```pwsh
npm install @auth0/angular-jwt@latest
```

If latest is > 5.2.0, install it. If it's already latest, no change.

- [ ] **Step 2: Update OpenTelemetry packages**

```pwsh
npm install @opentelemetry/api@latest @opentelemetry/exporter-trace-otlp-http@latest @opentelemetry/sdk-trace-base@latest @opentelemetry/sdk-trace-web@latest
```

Expected: Latest compatible versions.

- [ ] **Step 3: Update web-vitals**

```pwsh
npm install web-vitals@latest
```

Expected: Latest version (5.x or 6.x, check compatibility).

- [ ] **Step 4: Full production build**

```pwsh
npm run build -- --configuration production
```

Expected: Build succeeds.

- [ ] **Step 5: Vietnamese locale build**

```pwsh
npm run build -- --configuration production-vi
```

Expected: Vietnamese build succeeds. Output goes to `dist/vi/`.

- [ ] **Step 6: Full test suite**

```pwsh
npm test -- --no-watch --browsers ChromeHeadless
```

Expected: All tests pass.

- [ ] **Step 7: Lint check**

```pwsh
npm run lint
```

Expected: No errors.

- [ ] **Step 8: Security audit**

```pwsh
npm audit --audit-level=high
```

Expected: 0 HIGH or CRITICAL vulnerabilities. If any exist, assess and document.

- [ ] **Step 9: Docker build verification**

Read the Dockerfile at `D:\AI\micro\src\Frontend\his-hope-app\Dockerfile`. Verify it will work with the new build output path `dist/`.

Note: The Dockerfile copies from `dist/en/` (English locale) and `dist/vi/` (Vietnamese). The `application` builder output structure may differ. If Docker build fails, we handle it now:

```dockerfile
# The application builder outputs browser files at dist/browser/
# If the nginx config references a different path, update it.
```

The Dockerfile at `D:\AI\micro\src\Frontend\his-hope-app\Dockerfile` should be verified but NOT modified unless build fails.

- [ ] **Step 10: Run `npm start` and smoke test**

```pwsh
npm start
```

Open browser at http://localhost:4200:
- App loads without errors
- Login form renders
- All routes accessible
- No console errors

- [ ] **Step 11: Final commit**

```pwsh
git add package.json package-lock.json && git commit -m "chore: update auxiliary dependencies, finalize Phase 1 tooling upgrade"
```

---

### Task 8: Phase 1 Verification Checklist

Run all these before merging:

- [ ] `npm run build -- --configuration production` — passes
- [ ] `npm run build -- --configuration production-vi` — passes (i18n)
- [ ] `npm test -- --no-watch --browsers ChromeHeadless` — all pass
- [ ] `npm run lint` — no errors
- [ ] `npm audit --audit-level=high` — 0 HIGH/CRITICAL
- [ ] Build output in `dist/` — verify structure
- [ ] `npm start` — app loads, login works, routes render
- [ ] Store DevTools shows state in browser
- [ ] RUM traces appear in OTLP collector
- [ ] Docker build succeeds (if applicable)

- [ ] **Push branch**

```pwsh
git push origin chore/angular-19-upgrade-p1
```

---

### Phase 1 Completion Criteria

- All @angular/* packages at 19.x
- Build uses esbuild (`@angular-devkit/build-angular:application`)
- TypeScript 5.6+ with `moduleResolution: bundler`
- NgRx 19.x installed (store patterns NOT migrated yet)
- Zero application code changes
- All tests pass
- Both English and Vietnamese builds succeed
- Security audit clean
