# Task 6 Report

## Scope

- Shared frontend runtime contract/service
- Shared mobile runtime contract/service
- Angular bootstrap config for admin, dashboard, and his-hope-app
- Mobile runtime config files
- Focused runtime tests only

## Implementation

- PASS: Added shared frontend runtime validation for required `oidcAuthority`, absolute origins, and HTTPS-only production mode.
- PASS: Added shared mobile runtime validation for `apiOrigin`, `oidcAuthority`, TLS policy, and native redirect URI generation.
- PASS: Moved admin, dashboard, and his-hope-app OIDC/localization bootstrap config to the shared frontend runtime service.
- PASS: Updated mobile runtime bootstrap to consume the shared mobile runtime service and injected runtime contract.
- PASS: Extended mobile runtime bootstrap with `oidcAuthority` and explicit `production` signal.
- PASS: Added a Capacitor build-flavor guard that rejects insecure production Android scheme configuration.
- PASS: Added focused tests for missing authority, invalid origin, HTTP production rejection, and app-specific redirect URI behavior.

## Verification

- PASS: `rtk npm --workspace @his-hope/frontend-foundation run build`
- PASS: `rtk npm --workspace @his-hope/mobile-foundation run build`
- PASS: `rtk npm --workspace admin-app run build`
- PASS: `rtk npm --workspace dashboard-app run build`
- PASS: `rtk npm --workspace his-hope-app run build`
- PASS: `rtk npm --workspace @his-hope/mobile-app run build`
- PASS: `rtk npm --workspace @his-hope/mobile-foundation run test`
- PASS: `rtk npm --workspace @his-hope/frontend-foundation run test -- --watch=false --browsers=ChromeHeadless --include=src/lib/runtime/runtime-config.service.spec.ts`
- PASS: `rtk npm --workspace admin-app run test -- --watch=false --browsers=ChromeHeadless --include=src/app/app.config.spec.ts`
- PASS: `rtk npm --workspace dashboard-app run test -- --watch=false --browsers=ChromeHeadless --include=src/app/app.config.spec.ts`
- PASS: `rtk npm --workspace his-hope-app run test -- src/app/app.config.spec.ts --runInBand`
- PASS: `rtk npm --workspace @his-hope/mobile-app run test -- --watch=false --browsers=ChromeHeadless --include=src/app/core/mobile-runtime.spec.ts`

## Warnings observed during verification

- PASS_WITH_WARNING: `admin-app` production build exceeded the configured initial bundle warning budget by 28.46 kB.
- PASS_WITH_WARNING: `admin-app`, `his-hope-app`, and `mobile-app` production builds reported existing CommonJS optimization-bailout warnings.

## Notes

- I preserved unrelated dirty worktree changes and limited edits to Task 6 runtime paths plus the minimum foundation public exports required for app consumption.
