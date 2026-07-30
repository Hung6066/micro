# Task 3 brief — passkey-first server verification UI

Worktree: `D:\AI\micro-worktrees\passkey-first-adaptive-mfa`
Plan: `docs/superpowers/plans/2026-07-30-passkey-first-adaptive-mfa.md`

## Scope

Own only:

- `src/Services/IdentityService/IdentityService.Api/Composition/IdentityServiceEndpointExtensions.cs`
- `src/Services/IdentityService/IdentityService.Api/wwwroot/js/identity-login.js`
- `tests/IdentityService/IdentityService.IntegrationTests/VerificationPageTests.cs`

## Requirements

1. Server-rendered MFA page consumes server-derived methods endpoint and exposes `data-mfa-methods-endpoint`, `data-preferred-method`, stable IDs `passkey-mfa`, `native-passkey-mfa`, `alternate-methods`, `totp-form`.
2. Default UX is passkey-first. Mobile approval is top-level only when preferred method is `mobileApproval`; otherwise it is inside alternate methods. TOTP is fallback and hidden until alternate methods is selected.
3. Preserve His.Hope theme, accessible labels/status/error regions, safe return handling, and no client-supplied user ID.
4. JS must call ticket/session-bound endpoints only: browser passkey options + complete; native start/deep-link/bounded poll; TOTP six-digit pending endpoint. Handle 202, timeout, rejection, success and keep retry controls valid.
5. Add/update page contract tests for markup and stable IDs, plus JS behavior tests if existing test style supports it. Run focused VerificationPageTests.

## Constraints

- Do not change backend endpoint semantics in this task.
- Do not add speculative UI framework dependencies.
- Do not revert unrelated changes; you are not alone in the codebase.

## Deliverables

Commit with message `feat: render passkey-first OIDC verification page` and report at `.superpowers/sdd/2026-07-30-passkey-first-adaptive-mfa/task-3-report.md` with changed files and exact verification.
