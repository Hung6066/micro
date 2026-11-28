# Identity MFA Recovery Code Sign-In

## Intent

Allow an administrator who cannot use the authenticator app to complete the existing pending MFA login with one unused recovery code. This restores access without disabling or replacing the enrolled TOTP secret.

## Goals

- Accept either a six-digit TOTP or one recovery code at `POST /api/v1/auth/mfa/verify`, never both.
- Consume a valid recovery code exactly once, atomically, only for the user bound to the pending MFA session.
- Complete sign-in with the `recovery_code` authentication method while preserving the TOTP enrollment.
- Treat `recovery_code` as a completed MFA method in the follow-up OIDC authorization and MFA status checks.
- Add a recovery-code option to the MFA page and show actionable API error codes instead of the generic 4xx title.
- Verify behavior through the agreed integration-test seam: the public MFA verification endpoint.

## Non-goals

- Reset or disable TOTP enrollment during recovery sign-in.
- Change recovery-code generation, hashing, storage, or account-wide MFA policy.
- Log, display, or persist recovery-code values in diagnostics or browser storage.
- Change OpenShip, K3s policy, or deployment scope.

## Approaches

1. **Recovery code as an alternate factor on the existing verification endpoint (selected).** The endpoint consumes a code once and completes the bound pending login. It reuses the existing recovery-code store and keeps the TOTP enrollment intact. This is the smallest supported path for the current login page.
2. **Reset MFA during pending login.** This would disable the existing enrollment and require a new enrollment flow before returning to the client. It changes account state more broadly and is not needed to restore the current session.
3. **Keep recovery limited to authenticated settings.** This cannot help an administrator who is blocked at the pending-MFA page.

## Security and behavior

- Require a valid pending-MFA cookie and active user before checking either factor.
- Lock the user's MFA row while checking and removing a recovery-code hash, then commit before completing the sign-in. Invalid or previously consumed codes do not change the row or create an authenticated session.
- Include `recovery_code` in the authentication-method claims so downstream policy can distinguish it from TOTP.
- OIDC authorization and MFA status checks must recognize `recovery_code` as a completed factor; otherwise a successful recovery would immediately restart MFA.
- Keep request/response diagnostics free of factor values. The UI may show stable `errorCode` values such as invalid recovery code or expired MFA session.

## Verification

Integration tests through `POST /api/v1/auth/mfa/verify` will prove: a valid recovery code completes the same pending session and is consumed once; reuse and invalid codes fail without establishing a session; TOTP verification remains supported.

## Rollback

Revert the IdentityService source and UI change. Existing TOTP and authenticated recovery/reset behavior remain unchanged; codes consumed by successful recovery sign-ins remain consumed.
