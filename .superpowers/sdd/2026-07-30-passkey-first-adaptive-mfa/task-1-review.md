# Task 1 review

Verdict: NEEDS_FIX

## Findings

1. High: `IsUnfamiliarDevice` is effectively hard-wired to the unfamiliar path, so the real service cannot satisfy Task 1's "recognized device prefers passkey" behavior even though the pure policy test passes. `CompletePrimaryAsync` persists the result of `IsUnfamiliarDevice(context, user)` into every `PendingMfaContext`, but this checkout does not write `User.TrustedDeviceToken` or the `hishop_trusted_device` cookie anywhere else. That means the method returns `true` for every user with an empty token and for every request without the cookie. See `src/Services/IdentityService/IdentityService.Api/Services/OidcLoginCompletionService.cs:81-87` and `:209-216`, plus `src/Services/IdentityService/IdentityService.Domain/Entities/User.cs:42`.

2. High: The new pending-context "session binding" is still only a client-side double-cookie check, not a bind to an existing server session as the brief requires. `CompletePrimaryAsync` generates a random session ID, stores it inside the protected `hishop_oidc_mfa` payload, and also mirrors it into `hishop_oidc_mfa_session`; `TryGetPendingMfaContext` then only compares those two client-held values. Replaying the cookie pair on another client still satisfies the check because nothing is tied to a server-side session record or an existing authenticated cookie. See `src/Services/IdentityService/IdentityService.Api/Services/OidcLoginCompletionService.cs:81-103` and `:141-152`.

3. Medium: Test coverage does not lock the pending-session contract that Task 1 was supposed to establish. `AdaptiveMfaMethodTests` only exercises `AdaptiveMfaMethodPolicy.Resolve(...)`; there is no coverage for `TryGetPendingMfaContext`, expiry/session mismatch handling, or the `401`/`409` branches in `/api/v1/auth/passkeys/mfa/complete`. Both regressions above can ship while the focused test command still reports green. See `tests/IdentityService/IdentityService.IntegrationTests/AdaptiveMfaMethodTests.cs:7-36` and `src/Services/IdentityService/IdentityService.Api/Endpoints/PasskeyEndpoints.cs:190-229`.

## Verification

- Re-ran `dotnet test tests/IdentityService/IdentityService.IntegrationTests/IdentityService.IntegrationTests.csproj --filter FullyQualifiedName~AdaptiveMfaMethodTests` on July 30, 2026: 3 tests passed.
