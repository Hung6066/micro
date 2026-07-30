# Task 2 brief — server-derived MFA methods and completion binding

Worktree: `D:\AI\micro-worktrees\passkey-first-adaptive-mfa`
Plan: `docs/superpowers/plans/2026-07-30-passkey-first-adaptive-mfa.md`

## Scope

Implement only Task 2. Own these files/modules:

- `src/Services/IdentityService/IdentityService.Api/Endpoints/PasskeyEndpoints.cs`
- `src/Services/IdentityService/IdentityService.Api/Endpoints/MfaEndpoints.cs`
- `src/Services/IdentityService/IdentityService.Api/Services/OidcLoginCompletionService.cs`
- `tests/IdentityService/IdentityService.IntegrationTests/AdaptiveMfaEndpointTests.cs`

## Requirements

1. Add `GET /api/v1/auth/mfa/methods`, authenticated by the live pending browser/session binding, not ordinary bearer-only authorization.
2. Return server-derived `preferredMethod`, `availableMethods`, `isUnfamiliarDevice`, and a safe redirect handle. Never trust a client user ID or return secrets/full untrusted URL.
3. Query actual passkey enrollment and TOTP enrollment; determine device state from the server-side binding created by Task 1.
4. Ensure passkey browser/native options and completion, mobile approval completion, and TOTP verification all resolve the same pending user/session context before completing. Client-supplied user IDs must not override it.
5. Preserve existing response/error conventions: invalid/absent pending context is 401; user/session mismatch is 409 where the existing endpoint contract requires it.
6. Add focused integration tests for valid model, client ID ignored, pending mismatch/expiry/replay, and all completion paths. Run the focused tests and record exact command/output.

## Constraints

- Do not modify Angular/mobile UI in this task.
- Do not weaken MFA or create a second source of truth.
- Do not revert unrelated changes; you are not alone in the codebase.
- Use existing service abstractions and Clean Architecture patterns.

## Deliverables

- Production code and tests committed with message `feat: expose server-derived MFA methods`.
- Report at `.superpowers/sdd/2026-07-30-passkey-first-adaptive-mfa/task-2-report.md` listing changed files, command, output, and concerns.
