# Task 3 report — passkey-first server verification UI

Date: 2026-07-30

Scope completed:

- `src/Services/IdentityService/IdentityService.Api/Composition/IdentityServiceEndpointExtensions.cs`
- `src/Services/IdentityService/IdentityService.Api/wwwroot/js/identity-login.js`
- `tests/IdentityService/IdentityService.IntegrationTests/VerificationPageTests.cs`

Summary:

- Replaced the server-rendered OIDC verification page with passkey-first progressive disclosure driven by the pending-session MFA method contract.
- Exposed stable MFA page IDs and server-derived `data-mfa-methods-endpoint` / `data-preferred-method` attributes.
- Moved MFA JavaScript onto session-bound and ticket-bound calls only:
  - browser passkey: `/api/v1/auth/passkeys/mfa/options` → `/api/v1/auth/passkeys/mfa/complete`
  - native mobile approval: `/api/v1/auth/passkeys/mfa/native/start` → bounded poll of `/api/v1/auth/passkeys/mfa/native/poll`
  - TOTP fallback: `/api/v1/auth/mfa/verify` with only the six-digit code
- Hardened the shared script endpoint so `/api/v1/auth/identity-login.js` resolves correctly in the integration host as well as normal runtime layouts.
- Added page contract coverage for mobile-primary unfamiliar-device rendering, passkey-primary trusted-device rendering, and the shared JS endpoint contract.

Exact verification:

- `rtk dotnet test D:\AI\micro-worktrees\passkey-first-adaptive-mfa\tests\IdentityService\IdentityService.IntegrationTests\IdentityService.IntegrationTests.csproj --filter FullyQualifiedName~VerificationPageTests`
  - PASS — 3 tests passed, 0 warnings

Notes:

- The report and commit include only Task 3-owned files plus this report.
