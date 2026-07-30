# Task 4 report — reusable adaptive MFA UI state

Date: July 30, 2026

## Scope delivered

- Added reusable pure adaptive MFA state in `shared/frontend-foundation/src/security/his-hope-adaptive-mfa.ts`.
- Re-exported the module from `shared/frontend-foundation/src/index.ts`.
- Updated `admin-app/src/app/features/auth/login.component.ts` only for login initiation:
  - creates the shared passkey-first initiating state,
  - uses the preferred method only to choose the sign-in button icon,
  - delegates return URL handling to `AuthService.login(...)`, which routes through `HisHopeAuthCoordinator.login(...)` and its centralized safe-return-url filter,
  - now passes the requested `returnUrl` into automatic `trySsoLogin(...)` polling as well, so background SSO initiation uses the same shared safe coordinator path as manual login,
  - does not render or own MFA challenge UI or alternate-method selection.
- Added focused state/login coverage in `admin-app/src/app/features/auth/adaptive-mfa.component.spec.ts`.

No backend, mobile, or server-rendered Identity Service MFA page files were modified for this task.

## Implementation notes

- Root cause for the review failure: `startLogin()` already passed the requested `returnUrl` into `AuthService.login(...)`, but `ngOnInit()` polling called `AuthService.trySsoLogin()` without that argument, so automatic SSO initiation dropped the requested return URL before the shared coordinator could persist its sanitized value.
- Fix: `LoginComponent` now reads the requested return URL through one local getter and passes that same value to both `AuthService.login(...)` and `AuthService.trySsoLogin(...)`.

- `HisHopeMfaMethod` is exported as `'passkey' | 'mobileApproval' | 'totp'`.
- `createHisHopeAdaptiveMfaState(...)` normalizes available methods to deterministic `passkey -> mobileApproval -> totp` order.
- Preferred method rules:
  - passkey-first by default when passkey is available,
  - mobile approval first for unfamiliar devices when available,
  - TOTP-only availability remains valid.
- Alternate disclosure is explicit and deterministic through `setHisHopeAdaptiveMfaAlternateMethodsOpen(...)`.
- Alternate methods are derived through `getHisHopeAdaptiveMfaAlternateMethods(...)`.
- The shared state module has no HTTP, WebAuthn, Angular router, browser challenge, or UI dependencies.

## Verification

### Default focused Angular spec command

Command:

`rtk npm --workspace admin-app test -- --watch=false --browsers=ChromeHeadless --include src/app/features/auth/adaptive-mfa.component.spec.ts`

Result:

- FAIL before specs executed.
- ChromeHeadless could not start because the GPU process exited repeatedly:
  - `GPU process exited unexpectedly: exit_code=-1073741790`
  - `GPU process isn't usable. Goodbye.`

### Focused Angular spec with temporary no-GPU launcher

Temporary config:

`C:\tmp\karma-task4-angular-no-gpu.cjs`

Command:

`rtk npm --workspace admin-app test -- --watch=false --karma-config=C:\tmp\karma-task4-angular-no-gpu.cjs --browsers=ChromeHeadlessNoGpu --include src/app/features/auth/adaptive-mfa.component.spec.ts`

Result:

- PASS
- `TOTAL: 5 SUCCESS`

Covered behavior:

- default passkey-first preference,
- mobile approval preference on unfamiliar device,
- explicit alternate-method disclosure,
- TOTP-only availability,
- manual login initiation delegates return URL to the shared safe login path,
- automatic SSO polling preserves the requested return URL through `trySsoLogin(...)`.

### Shared foundation build

Command:

`rtk npm --workspace @his-hope/frontend-foundation run build`

Result:

- PASS
- package built to `shared/frontend-foundation/dist`
- warnings:
  - conflicting export condition for `"."` in package manifest,
  - `scripts` and `devDependencies` removed from packed manifest by `ng-packagr`.

### Admin app build

Command:

`rtk npm --workspace admin-app run build`

Result:

- PASS on serial rerun after the foundation package build completed.
- output written to `admin-app/dist/admin-app`
- warnings:
  - initial bundle exceeded the 900 kB warning budget by 266.10 kB,
  - `qrcode` is CommonJS and causes an optimization bailout.

Note: an earlier parallel admin build attempt failed while the foundation package was being rebuilt and `@his-hope/frontend-foundation` could not be resolved. The serial rerun above is the final admin build gate for this task.

## Final status

- Task 4 implementation: PASS
- Task 4 review fix: PASS
- Focused Angular spec: PASS with temporary no-GPU Karma launcher
- Shared foundation build: PASS
- Admin app build: PASS
