# Task 3 report — passkey-first server verification UI

Date: 2026-07-30

Scope completed:

- `src/Services/IdentityService/IdentityService.Api/Composition/IdentityServiceEndpointExtensions.cs`
- `src/Services/IdentityService/IdentityService.Api/Endpoints/PasskeyEndpoints.cs`
- `src/Services/IdentityService/IdentityService.Api/wwwroot/js/identity-login.js`
- `tests/IdentityService/IdentityService.IntegrationTests/VerificationPageTests.cs`

Summary:

- Replaced the server-rendered OIDC verification page with passkey-first progressive disclosure driven by the pending-session MFA method contract.
- Exposed stable MFA page IDs and server-derived `data-mfa-methods-endpoint` / `data-preferred-method` attributes.
- Hardened the native mobile launch so the browser preserves the user click gesture by pre-opening a blank window synchronously, then navigating it after `/mfa/native/start`, with same-tab fallback if the popup is blocked.
- Aligned native polling with the server ticket lifetime:
  - server ticket lifetime remains 5 minutes
  - client poll deadline now derives from the server lifetime with a 15-second safety buffer
  - poll backoff is bounded and long-lived enough for a real mobile handoff instead of timing out after ~30 seconds
- Moved MFA JavaScript onto session-bound and ticket-bound calls only:
  - browser passkey: `/api/v1/auth/passkeys/mfa/options` → `/api/v1/auth/passkeys/mfa/complete`
  - native mobile approval: `/api/v1/auth/passkeys/mfa/native/start` → bounded poll of `/api/v1/auth/passkeys/mfa/native/poll`
  - TOTP fallback: `/api/v1/auth/mfa/verify` with only the six-digit code
- Taught the native poll endpoint to respond cleanly for pending / approved / rejected / expired ticket states so the browser can show accurate recovery guidance.
- Hardened the shared script endpoint so `/api/v1/auth/identity-login.js` resolves correctly in the integration host as well as normal runtime layouts.
- Added focused verification-page coverage for:
  - mobile-primary unfamiliar-device rendering
  - passkey-primary trusted-device rendering
  - shared JS deep-link launch contract
  - shared JS native poll timeout/status contract
  - native poll endpoint pending / approved / rejected / expired behavior

Exact verification:

- `rtk dotnet test D:\AI\micro-worktrees\passkey-first-adaptive-mfa\tests\IdentityService\IdentityService.IntegrationTests\IdentityService.IntegrationTests.csproj --filter FullyQualifiedName~VerificationPageTests`
  - PASS — 9 tests passed, 0 failed

Notes:

- The report and commit include only Task 3-owned files plus this report.
