# Identity MFA Recovery Code Sign-In Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Let a user finish a pending IdentityService MFA login with one unused recovery code while keeping the enrolled TOTP factor enabled.

**Architecture:** Extend the existing `/api/v1/auth/mfa/verify` request and pending-MFA completion flow with an alternate recovery-code factor. Remove a matching recovery-code hash under a database transaction/row lock before completing sign-in. Add an alternate recovery form to the existing MFA page and use the existing browser script to submit it without storing or logging the code.

**Tech Stack:** .NET 10, ASP.NET Core minimal APIs, EF Core/Npgsql, ASP.NET Identity, vanilla JavaScript, xUnit integration tests.

**Spec:** `docs/superpowers/specs/2026-11-01-identity-mfa-recovery-code-design.md`

## Global Constraints

- Accept exactly one factor (`Code` or `RecoveryCode`) at the public verification endpoint.
- A recovery code is valid only for the active user bound to the live pending-MFA cookie and browser session.
- Consume a valid code once, atomically, before issuing the authenticated session.
- Preserve `UserMfa.IsEnabled` and the TOTP secret.
- Do not expose recovery-code values through API diagnostics, UI persistence, or logs.
- Do not change OpenShip, K3s, or account-wide MFA policy.

## Review Focus

- Two concurrent requests try the same code: exactly one may complete sign-in; test in Task 1.
- Invalid/reused code or a mismatched pending-session binding: no authenticated session and no recovery-code mutation; test in Task 1.
- TOTP remains usable after recovery sign-in and recovery code never disables enrollment; assert in Task 1.
- UI sends only the selected factor, clears the submitted value, and shows an actionable response; test in Task 2.
- A malformed request containing both factors is rejected without consuming either; test in Task 1.

---

### Task 1: Pending-session recovery completion

**Files:**
- Modify: `src/Services/IdentityService/IdentityService.Application/DTOs/AuthDtos.cs`
- Modify: `src/Services/IdentityService/IdentityService.Api/Endpoints/MfaEndpoints.cs`
- Modify: `src/Services/IdentityService/IdentityService.Api/Endpoints/IdentityOidcAuthorizeEndpoints.cs`
- Modify: `src/Services/IdentityService/IdentityService.Api/Services/OidcLoginCompletionService.cs`
- Test: `tests/IdentityService/IdentityService.IntegrationTests/AdaptiveMfaEndpointTests.cs`

**Interfaces:**
- `MfaVerifyRequest` gains nullable `RecoveryCode` while retaining `Code`.
- `OidcLoginCompletionService` exposes a pending recovery completion method returning the existing `PendingMfaCompletionResult` statuses.
- The public endpoint chooses one factor only and returns stable error codes for invalid TOTP, invalid recovery code, unauthorized pending session, and invalid request shape.
- Both MFA status and OIDC authorization accept the `recovery_code` authentication method as a completed MFA factor.

- [x] **Step 1: Write endpoint integration tests first**

Add tests through `POST IdentityApiRoutes.MfaVerify` using the existing pending-MFA session fixture:
- valid recovery code completes the bound session, returns the original OIDC redirect URL, consumes the hash, and leaves MFA enabled with its TOTP secret intact;
- after recovery sign-in, MFA status reports completion and the follow-up OIDC authorization does not redirect back to `/Account/Mfa`;
- two pending sessions submitting the same code concurrently produce exactly one successful completion;
- invalid and already-consumed codes fail and do not create an authenticated session;
- a request supplying both `code` and `recoveryCode` is rejected and leaves the code usable;
- a session-binding mismatch cannot consume the code;
- a valid TOTP still completes after recovery sign-in.

- [x] **Step 2: Run the focused tests and confirm the recovery cases fail**

Run: `dotnet test tests/IdentityService/IdentityService.IntegrationTests/IdentityService.IntegrationTests.csproj --filter FullyQualifiedName~AdaptiveMfaEndpointTests`
Expected: new recovery tests fail because pending verification accepts only TOTP.

- [x] **Step 3: Implement the request and endpoint factor selection**

In `MfaVerifyRequest`, add nullable `RecoveryCode`. In `MfaEndpoints`, reject requests that provide both or neither factor; dispatch recovery requests only when a live pending-MFA context is present. Preserve the existing authenticated-user verification behavior for TOTP and authenticated recovery/reset behavior.

- [x] **Step 4: Implement atomic pending recovery completion**

In `OidcLoginCompletionService`, validate the pending context, active user, and enabled MFA record. Hash the submitted code with `RecoveryCodeService`, remove exactly one matching hash inside a database transaction with a row lock, and commit before calling `CompletePendingMfaAsync(..., "recovery_code")`. Keep the TOTP secret and enrollment enabled. Return invalid-code for missing/reused codes without changing state.

- [x] **Step 5: Run the focused integration tests**

Run: `dotnet test tests/IdentityService/IdentityService.IntegrationTests/IdentityService.IntegrationTests.csproj --filter FullyQualifiedName~AdaptiveMfaEndpointTests`
Expected: all existing adaptive MFA tests and new recovery tests pass, including the concurrent single-use assertion.

### Task 2: Recovery-code UI and useful errors

**Files:**
- Modify: `src/Services/IdentityService/IdentityService.Api/Composition/IdentityServiceEndpointExtensions.cs`
- Modify: `src/Services/IdentityService/IdentityService.Api/wwwroot/js/identity-login.js`
- Test: `tests/IdentityService/IdentityService.IntegrationTests/VerificationPageTests.cs`

**Interfaces:**
- The rendered pending-MFA page provides a recovery-code input and submit action in the alternate verification section.
- The browser script submits `{ recoveryCode }` to the existing verification endpoint and never stores or logs its value.

- [x] **Step 1: Add rendered-page and script contract tests first**

Assert that `/Account/Mfa` includes an accessible recovery-code field and action. Assert that the served script submits `recoveryCode` to `IdentityApiRoutes.MfaVerify`, clears the field after submission, and maps stable `errorCode` values to actionable text without logging the factor value.

- [x] **Step 2: Run the focused page tests and confirm they fail**

Run: `dotnet test tests/IdentityService/IdentityService.IntegrationTests/IdentityService.IntegrationTests.csproj --filter FullyQualifiedName~VerificationPageTests`
Expected: new recovery UI assertions fail because the page currently exposes only TOTP.

- [x] **Step 3: Add the recovery-code form and submit behavior**

Add a recovery-code form beside the TOTP alternative in the rendered MFA template. Implement its submit handler in `identity-login.js`, submit only the recovery factor, clear the input on both success and failure, establish the browser session after success, and navigate to the server-provided safe return URL. Ensure `readProblem` handles stable API `errorCode` values.

- [x] **Step 4: Run the focused page tests**

Run: `dotnet test tests/IdentityService/IdentityService.IntegrationTests/IdentityService.IntegrationTests.csproj --filter FullyQualifiedName~VerificationPageTests`
Expected: all page rendering and script contract tests pass.

### Task 3: Full IdentityService verification

**Files:**
- No additional files unless Task 1 or 2 exposes a regression.

- [x] **Step 1: Run the complete IdentityService integration test project**

Run: `dotnet test tests/IdentityService/IdentityService.IntegrationTests/IdentityService.IntegrationTests.csproj`
Expected: pass with no failures; report any environment-blocked integration dependencies separately from code failures.

- [x] **Step 2: Review the final diff**

Check that no recovery-code literal, secret, unrelated worktree change, or TOTP reset behavior was introduced. Preserve the pre-existing dirty submodule `docs/design-references/open-design`.
