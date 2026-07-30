# Task 1 review fix

Verdict: NEEDS_FIX

## Findings

1. High: the pending MFA flow still is not bound to an existing server session, and the unfamiliar-device fallback is now optimistic. `/Account/Login` calls `CompletePrimaryAsync` before any `hishop_sid` is minted (`src/Services/IdentityService/IdentityService.Api/Composition/IdentityServiceEndpointExtensions.cs:1758-1781`), so `GetOrCreatePendingSessionId` falls back to a new opaque token instead of an existing server session (`src/Services/IdentityService/IdentityService.Api/Services/OidcLoginCompletionService.cs:265-270`). On the same no-session path, `HasRecognizedBrowserSessionAsync` returns `true` (`src/Services/IdentityService/IdentityService.Api/Services/OidcLoginCompletionService.cs:288-292`), and the new focused test locks that behavior in (`tests/IdentityService/IdentityService.IntegrationTests/AdaptiveMfaMethodTests.cs:53-76`). The result is that a fresh browser with no trusted-device token is classified as familiar rather than unfamiliar, which leaves the original session-binding concern unresolved and weakens the adaptive MFA decision.

## Verification

- `rtk dotnet test tests/IdentityService/IdentityService.IntegrationTests/IdentityService.IntegrationTests.csproj --filter FullyQualifiedName~AdaptiveMfaMethod` -> 8 passed on July 30, 2026.
- The focused suite now covers pending-context mismatch/expiry and `/api/v1/auth/passkeys/mfa/complete` `401` and `409` branches.
