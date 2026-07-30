# Task 4 brief — reusable Angular adaptive MFA state

Worktree: `D:\AI\micro-worktrees\passkey-first-adaptive-mfa`
Plan: `docs/superpowers/plans/2026-07-30-passkey-first-adaptive-mfa.md`

## Scope

Own only:

- `shared/frontend-foundation/src/security/his-hope-adaptive-mfa.ts` (create)
- `shared/frontend-foundation/src/index.ts`
- `admin-app/src/app/features/auth/login.component.ts` (only initiating login state)
- `admin-app/src/app/features/auth/adaptive-mfa.component.spec.ts` (create)

## Requirements

1. Export reusable types and pure state transitions from frontend-foundation:
   `HisHopeMfaMethod = 'passkey' | 'mobileApproval' | 'totp'` and state with preferred, available, unfamiliarDevice, alternateMethodsOpen.
2. Default state is passkey-first when available; unfamiliar device with mobile approval prefers mobile; TOTP-only remains valid; alternate disclosure is explicit and deterministic.
3. No HTTP/WebAuthn/router dependency in the shared state module.
4. Export it through the foundation public index, with tests for default method, mobile-first unfamiliar, alternate disclosure, and TOTP-only.
5. Wire only the initiating admin login state without duplicating server-rendered MFA UI or trusting client return URLs. Preserve existing shared foundation i18n patterns for labels if login component needs text.
6. Run focused Angular tests plus foundation/admin builds and record exact results.

## Constraints

- Do not modify server-rendered IdentityService MFA page in this task.
- Do not import raw shared source through app path hacks.
- Do not revert unrelated user changes; you are not alone in the codebase.

## Deliverables

Commit `feat: add reusable adaptive MFA UI state` and report at `.superpowers/sdd/2026-07-30-passkey-first-adaptive-mfa/task-4-report.md`.
