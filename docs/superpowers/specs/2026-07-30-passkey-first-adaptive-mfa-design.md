# Passkey-first adaptive MFA design

## Goal

Make the OIDC verification page behave like a passkey-first sign-in flow. The preceding login step already establishes the user email and a pending OIDC authentication context, so the verification page must not ask for the email again.

The order of preference is:

1. Device passkey for the current browser/device.
2. His.Hope mobile approval when the sign-in is from an unfamiliar device or the user chooses mobile approval.
3. TOTP authenticator as the final fallback.

The browser must not invoke WebAuthn automatically on page load because browsers require a user gesture. The passkey action is therefore the visually primary, focused action and starts immediately after the user clicks it.

## User experience

### Primary path

The OIDC login page creates or continues the pending login context with the email. If the primary credential step succeeds and the account requires MFA, Identity Service redirects to the verification page with the pending context preserved in the server-side session.

The verification page shows:

- `Continue with device passkey` as the primary action.
- `Approve in His.Hope mobile app` as a primary alternative when the server marks the device unfamiliar, otherwise inside `Use another method`.
- `Use another method` as a secondary action.
- TOTP only inside the alternate-method panel, labelled as a fallback.

Passkey cancellation, unavailable passkey, expired challenge, or a rejected assertion must return the user to the same page with an actionable error and without destroying the pending OIDC context.

### Adaptive device path

Identity Service evaluates the pending request using a server-issued device binding/trust record, not a client-provided boolean. A new or unfamiliar device gets a `mobileApproval` preferred method. A recognized device with an enrolled passkey gets `passkey` preferred method. The decision is advisory for presentation; the server still validates every factor and never trusts a frontend-selected method.

Mobile approval creates a short-lived, single-use ticket bound to the pending user and OIDC request. The browser polls the ticket over the existing session. The mobile app receives the deep link, obtains the native passkey assertion, and completes the ticket. Approval then completes the pending MFA session and redirects to the original OIDC return URL.

### Fallback path

The alternate-method panel must expose only methods available for the pending user/session. TOTP is shown only when MFA is enrolled. If mobile approval times out, is rejected, or the device does not support native passkeys, the user can choose TOTP without restarting OIDC authorization.

## Backend contract

Add a server-rendered verification model or JSON endpoint that returns:

```json
{
  "userId": "server-derived-user-id",
  "preferredMethod": "passkey|mobileApproval|totp",
  "availableMethods": ["passkey", "mobileApproval", "totp"],
  "isUnfamiliarDevice": true,
  "returnUrl": "/connect/authorize?..."
}
```

The response must be derived from the pending authentication session. `userId`, `returnUrl`, and unfamiliar-device state must not be accepted from an untrusted browser payload.

Existing passkey endpoints remain the cryptographic boundary:

- Primary sign-in: `/api/v1/auth/passkeys/authenticate/options` and `/complete`.
- Browser MFA passkey: `/api/v1/auth/passkeys/mfa/options` and `/complete`.
- Mobile approval: `/api/v1/auth/passkeys/mfa/native/start`, `/poll`, `/options`, and `/complete`.
- TOTP: the pending MFA verification endpoint, completed only after the server validates the session and code.

All completion paths must call the same OIDC MFA completion service so the resulting session/token has consistent `amr` values and preserves the original redirect/state/PKCE data.

## Frontend and mobile boundaries

Angular owns presentation, method selection, WebAuthn browser calls, polling, errors, and accessibility. It does not decide whether a device is trusted and does not manufacture user IDs or return URLs.

Native mobile owns the platform Credential Manager/AuthenticationServices interaction and submits the assertion to the ticket-bound native completion endpoint. It does not receive or store the browser OIDC token. Shared foundation exposes the method state, loading, retry, and error primitives so Angular and mobile use the same semantic contract.

## Security rules

- Every challenge and approval ticket is short-lived, single-use, and Redis-backed for multi-replica operation.
- Bind MFA completion to the pending OIDC session, user, client, redirect URI, nonce, and PKCE state.
- Do not treat a client-provided `isUnfamiliarDevice` or `preferredMethod` as authoritative.
- Do not log TOTP values, passkey assertions, approval tickets, or full OIDC return URLs.
- Rate-limit passkey, mobile approval, and TOTP attempts independently.
- Reject replay, stale tickets, mismatched users, mismatched sessions, and invalid redirect URLs.

## Verification criteria

1. A user with an enrolled passkey sees passkey as the primary action and can complete OIDC without TOTP.
2. A new device sees mobile approval as the preferred alternative and can complete through the native mobile app.
3. Mobile rejection/timeout exposes TOTP without restarting the OIDC request.
4. A user without a passkey sees only the available methods and can use TOTP when enrolled.
5. All successful methods redirect to the original OIDC callback with valid state and PKCE handling.
6. Angular and native mobile tests cover success, cancellation, unavailable authenticator, timeout, replay, and session mismatch.
