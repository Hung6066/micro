# Passkey-first Adaptive MFA Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make the existing OIDC verification page passkey-first, prefer mobile approval for unfamiliar devices, and keep TOTP as an explicit fallback without losing the pending OIDC session.

**Architecture:** Keep cryptographic verification and method availability in Identity Service. Add one server-derived verification model for the pending OIDC session, then let the server-rendered page and Angular/mobile clients consume the same method contract. All successful factors call the existing OIDC completion service so redirect URI, state, nonce, PKCE, session cookies, and `amr` handling remain centralized.

**Tech Stack:** ASP.NET Core Minimal APIs, Identity Service, Redis, Fido2NetLib, server-rendered HTML/JavaScript, Angular 21, shared frontend foundation, native Android Credential Manager, native iOS AuthenticationServices.

## Global Constraints

- The verification page receives identity from the server-side pending OIDC/MFA session; it never asks for email again.
- Browser WebAuthn starts only from a user gesture; do not call `navigator.credentials.get` on page load.
- The frontend cannot decide `isUnfamiliarDevice`, `preferredMethod`, `userId`, or `returnUrl`.
- MFA challenges and mobile tickets are short-lived, single-use, Redis-backed, and bound to the pending user/session.
- TOTP is shown only as an explicit fallback when it is enrolled.
- Do not log TOTP values, WebAuthn assertions, approval tickets, or full OIDC return URLs.
- Preserve the existing PKCE/state/nonce and redirect validation path.
- Every task must keep the shared Angular/mobile security capability boundary; app-specific code may only compose the capability.

---

## File Map

### Backend and server-rendered identity UI

- Modify `src/Services/IdentityService/IdentityService.Api/Endpoints/PasskeyEndpoints.cs`: expose the server-derived MFA method model and enforce pending-session binding for factor completion.
- Modify `src/Services/IdentityService/IdentityService.Api/Composition/IdentityServiceEndpointExtensions.cs`: render the new passkey-first verification page and method panel from the pending session.
- Modify `src/Services/IdentityService/IdentityService.Api/wwwroot/js/identity-login.js`: implement method selection, passkey gesture flow, mobile approval polling, and TOTP fallback without duplicating OIDC completion.
- Modify `src/Services/IdentityService/IdentityService.Api/Services/OidcLoginCompletionService.cs`: expose the smallest pending-session/device-trust queries needed by the endpoint; keep completion centralized.
- Create or modify backend tests beside Identity Service endpoints/services for method ordering, unavailable factors, unfamiliar-device preference, replay, and session mismatch.

### Shared frontend and Angular

- Create `shared/frontend-foundation/src/security/his-hope-adaptive-mfa.ts`: typed method state and reusable method-selection state machine; no browser or app-specific navigation.
- Export it from `shared/frontend-foundation/src/index.ts`.
- Modify `admin-app/src/app/features/auth/login.component.ts` and any callback/verification component used by the OIDC flow to consume the method contract and render passkey-first actions.
- Add Angular tests beside the auth feature for default selection, alternate-method expansion, cancellation, timeout, and TOTP fallback.

### Mobile

- Modify the shared mobile security capability seam under `shared/mobile-foundation` if required by the existing package layout; expose a ticket-bound `approveMfa(ticket)` operation.
- Modify `mobile-app/src/app` only to compose the shared native bridge and display the approval/denied/expired states.
- Keep Android implementation in the native plugin/bridge using Credential Manager and iOS implementation in the AuthenticationServices bridge; do not put platform cryptography or ticket validation in mobile page components.
- Add native/mobile tests or device/emulator validation scripts for approve, reject, timeout, and missing native capability.

---

## Task 1: Lock the pending-session contract with failing backend tests

**Files:**
- Create: `tests/IdentityService/IdentityService.IntegrationTests/AdaptiveMfaMethodTests.cs`.
- Inspect/modify: `src/Services/IdentityService/IdentityService.Api/Services/OidcLoginCompletionService.cs`.
- Inspect: `src/Services/IdentityService/IdentityService.Api/Endpoints/PasskeyEndpoints.cs`.

**Interfaces:**
- Produce `AdaptiveMfaMethods` with `PreferredMethod`, `AvailableMethods`, `IsUnfamiliarDevice`, and server-derived `UserId`/`ReturnUrl` accessors for the pending context.
- Produce `TryGetPendingMfaContext(HttpContext)` returning a nullable context containing the pending user ID, original return URL, and device classification.

- [ ] **Step 1: Write failing tests** for these exact cases:

```csharp
[Fact]
public void Recognized_device_with_passkey_prefers_passkey()
{
    var result = AdaptiveMfaMethodPolicy.Resolve(
        hasPasskey: true, hasMobileApproval: true, hasTotp: true, unfamiliarDevice: false);

    result.PreferredMethod.Should().Be("passkey");
    result.AvailableMethods.Should().BeEquivalentTo("passkey", "mobileApproval", "totp");
}

[Fact]
public void Unfamiliar_device_prefers_mobile_approval()
{
    var result = AdaptiveMfaMethodPolicy.Resolve(
        hasPasskey: true, hasMobileApproval: true, hasTotp: true, unfamiliarDevice: true);

    result.PreferredMethod.Should().Be("mobileApproval");
}

[Fact]
public void Totp_is_available_only_when_enrolled()
{
    var result = AdaptiveMfaMethodPolicy.Resolve(
        hasPasskey: false, hasMobileApproval: false, hasTotp: false, unfamiliarDevice: false);

    result.AvailableMethods.Should().BeEmpty();
}
```

- [ ] **Step 2: Run the focused tests and confirm failure.**

Run:

```powershell
dotnet test tests/IdentityService/IdentityService.IntegrationTests/IdentityService.IntegrationTests.csproj --filter FullyQualifiedName~AdaptiveMfaMethodTests
```

Expected: FAIL because the policy/context contract is not implemented.

- [ ] **Step 3: Implement the smallest deterministic policy.**

Use this ordering:

```csharp
var available = new List<string>();
if (hasPasskey) available.Add("passkey");
if (hasMobileApproval) available.Add("mobileApproval");
if (hasTotp) available.Add("totp");
var preferred = unfamiliarDevice && hasMobileApproval
    ? "mobileApproval"
    : hasPasskey ? "passkey"
    : hasMobileApproval ? "mobileApproval"
    : hasTotp ? "totp" : null;
```

Bind the pending context to the existing server session and reject missing/mismatched context with `401` or `409`, not a client-provided user ID.

- [ ] **Step 4: Run the focused tests and confirm pass.**

- [ ] **Step 5: Commit.**

```powershell
git add src/Services/IdentityService
git commit -m "feat: define adaptive MFA method policy"
```

## Task 2: Add the server-derived verification model and enforce completion binding

**Files:**
- Modify: `src/Services/IdentityService/IdentityService.Api/Endpoints/PasskeyEndpoints.cs`.
- Modify: `src/Services/IdentityService/IdentityService.Api/Endpoints/MfaEndpoints.cs`.
- Modify: `src/Services/IdentityService/IdentityService.Api/Services/OidcLoginCompletionService.cs`.
- Test: `tests/IdentityService/IdentityService.IntegrationTests/AdaptiveMfaEndpointTests.cs`.

**Interfaces:**
- Add `GET /api/v1/auth/mfa/methods` (authenticated by the pending browser session, not ordinary bearer-only authorization) returning the server-derived method model.
- Ensure `/mfa/options`, `/mfa/native/start`, `/mfa/complete`, `/mfa/native/complete`, and TOTP verification all compare the pending user/session before completing.

- [ ] **Step 1: Write failing endpoint tests** asserting that a valid pending session returns the model, a client-supplied user ID is ignored, and a mismatched pending session returns `401`.

- [ ] **Step 2: Run focused tests and confirm failure.**

- [ ] **Step 3: Implement `GET /api/v1/auth/mfa/methods`.**

The endpoint must:

1. Read the pending user and original OIDC request from `OidcLoginCompletionService`.
2. Query passkey enrollment and TOTP enrollment.
3. Determine unfamiliar-device state from the server-side device binding/trust record.
4. Return `preferredMethod`, `availableMethods`, `isUnfamiliarDevice`, and a safe redirect handle; never return secrets or the full untrusted return URL.

- [ ] **Step 4: Make all factor completion paths call the same completion methods.**

Passkey and mobile approval must continue to call `CompleteMfaWithPasskeyAsync` or a semantically equivalent centralized method. TOTP must use the same pending context and issue the same session/token shape with `amr` containing the validated factor.

- [ ] **Step 5: Add replay/expiry/session-mismatch tests.**

Cover:

```text
same ticket twice -> second attempt is rejected
expired ticket -> rejected
ticket for user A with pending session for user B -> rejected
successful factor -> original callback/state/PKCE preserved
```

- [ ] **Step 6: Run focused tests and commit.**

```powershell
dotnet test tests/IdentityService/IdentityService.IntegrationTests/IdentityService.IntegrationTests.csproj --filter FullyQualifiedName~AdaptiveMfaEndpointTests
git add src/Services/IdentityService
git commit -m "feat: expose adaptive MFA methods for pending OIDC sessions"
```

## Task 3: Replace the server-rendered verification UI with passkey-first progressive disclosure

**Files:**
- Modify: `src/Services/IdentityService/IdentityService.Api/Composition/IdentityServiceEndpointExtensions.cs`.
- Modify: `src/Services/IdentityService/IdentityService.Api/wwwroot/js/identity-login.js`.
- Test: `tests/IdentityService/IdentityService.IntegrationTests/VerificationPageTests.cs`.

**Interfaces:**
- HTML exposes `data-mfa-methods-endpoint`, `data-preferred-method`, and stable button IDs: `passkey-mfa`, `native-passkey-mfa`, `alternate-methods`, `totp-form`.
- JavaScript consumes the server-derived method model and only sends ticket/session-bound requests.

- [ ] **Step 1: Write a page contract test** asserting the primary passkey action, conditional mobile action, hidden alternate methods, and TOTP fallback markup.

- [ ] **Step 2: Run the page test and confirm failure.**

- [ ] **Step 3: Implement the page layout.**

Render:

```html
<button id="passkey-mfa" class="primary">Continue with device passkey</button>
<button id="native-passkey-mfa" class="secondary">Approve in His.Hope mobile app</button>
<button id="alternate-methods" class="link">Use another method</button>
<section id="alternate-method-panel" hidden>
  <form id="totp-form" hidden><label for="totp-code">Authenticator code</label><input id="totp-code" inputmode="numeric" autocomplete="one-time-code" maxlength="6"><button type="submit">Verify with TOTP</button></form>
</section>
```

Only render the mobile action as a top-level button when `preferredMethod` is `mobileApproval`; otherwise keep it in the alternate panel. Keep accessible status/error regions and preserve the existing His.Hope theme.

- [ ] **Step 4: Implement JavaScript behavior.**

On click:

1. Passkey calls `/api/v1/auth/passkeys/mfa/options`, invokes WebAuthn, then `/complete`.
2. Mobile calls `/api/v1/auth/passkeys/mfa/native/start`, opens the deep link, polls with bounded backoff, and handles `202`, timeout, rejection, and success.
3. TOTP posts only the six-digit code to the pending-session endpoint and redirects using the server response.
4. Any failure keeps the panel open and re-enables only valid methods.

- [ ] **Step 5: Run page/JavaScript tests and commit.**

```powershell
dotnet test tests/IdentityService/IdentityService.IntegrationTests/IdentityService.IntegrationTests.csproj --filter FullyQualifiedName~VerificationPageTests
git add src/Services/IdentityService
git commit -m "feat: render passkey-first OIDC verification page"
```

## Task 4: Add reusable Angular adaptive MFA state and UI

**Files:**
- Create: `shared/frontend-foundation/src/security/his-hope-adaptive-mfa.ts`.
- Modify: `shared/frontend-foundation/src/index.ts`.
- Modify: `admin-app/src/app/features/auth/login.component.ts` only for the initiating login state; the actual verification page remains server-rendered by Identity Service.
- Create: `admin-app/src/app/features/auth/adaptive-mfa.component.spec.ts`.

**Interfaces:**

```ts
export type HisHopeMfaMethod = 'passkey' | 'mobileApproval' | 'totp';
export interface HisHopeAdaptiveMfaState {
  preferredMethod: HisHopeMfaMethod | null;
  availableMethods: HisHopeMfaMethod[];
  unfamiliarDevice: boolean;
  alternateMethodsOpen: boolean;
}
```

- [ ] **Step 1: Write failing tests** for default method, mobile-first unfamiliar device, alternate-method disclosure, and TOTP-only availability.

- [ ] **Step 2: Run the focused Angular tests and confirm failure.**

- [ ] **Step 3: Implement the pure state transition functions** with no HTTP, WebAuthn, or router dependencies.

- [ ] **Step 4: Wire the auth component** to the existing shared security capability and server-rendered endpoint contract. The component must preserve the pending OIDC URL and show localized Vietnamese/English labels from shared foundation dictionaries.

- [ ] **Step 5: Build and test.**

```powershell
cd shared/frontend-foundation
npm run build
cd ..\..\admin-app
npm run build
npm test -- --watch=false --browsers=ChromeHeadless
```

- [ ] **Step 6: Commit.**

```powershell
git add shared/frontend-foundation admin-app
git commit -m "feat: add reusable adaptive MFA UI state"
```

## Task 5: Implement native mobile approval through the shared bridge

**Files:**
- Modify: `shared/mobile-foundation/src/index.ts` and add `shared/mobile-foundation/src/security/his-hope-native-mfa.ts` for the reusable ticket-bound capability.
- Modify: `mobile-app/src/app/mobile-native-mfa.component.ts` and `mobile-app/src/app/core/native-capability.service.ts` only as consumers of the bridge.
- Modify: `mobile-app/android/app/src/main/java/com/hishope/mobile/HisHopeSecurityPlugin.java` for Android Credential Manager.
- Modify: `mobile-app/ios/App/App/HisHopeSecurityPlugin.swift` for iOS AuthenticationServices.
- Test: platform bridge tests and mobile approval flow tests.

**Interfaces:**

```ts
approveMfa(request: { ticket: string }): Promise<{ approved: boolean }>;
```

- [ ] **Step 1: Write failing bridge tests** for successful assertion, user cancellation, unsupported platform, expired ticket, and server rejection.
- [ ] **Step 2: Run them and confirm failure.**
- [ ] **Step 3: Implement Android using Credential Manager and iOS using AuthenticationServices.** Keep ticket binding and assertion submission in the bridge service; do not store browser access/refresh tokens.
- [ ] **Step 4: Wire the mobile page to show loading, approved, rejected, expired, and retry states.
- [ ] **Step 5: Validate on Android emulator/device and iOS simulator/device where passkey capability is available. Record unavailable platform gates explicitly.
- [ ] **Step 6: Commit.**

```powershell
git add shared/mobile-foundation mobile-app
git commit -m "feat: complete native mobile MFA approval bridge"
```

## Task 6: End-to-end security and deployment verification

**Files:**
- Modify: `tests/e2e` auth/MFA tests or create `tests/e2e/adaptive-mfa.spec.ts`.
- Modify: deployment/security documentation under `docs/` with the final flow and operational configuration.

- [ ] **Step 1: Add browser E2E cases** for passkey-first, alternate methods, TOTP fallback, unfamiliar-device mobile approval, timeout, cancel, replay, and callback preservation.
- [ ] **Step 2: Run backend tests, Angular builds, lint/type checks, and E2E.**

```powershell
dotnet test His.Hope.sln
cd shared/frontend-foundation; npm run build
cd ..\..\admin-app; npm run build
cd ..\tests\e2e; npx playwright test --workers=1
```

- [ ] **Step 3: Rebuild and restart the affected Docker services.**

```powershell
docker compose -f docker/docker-compose.yml build identityservice admin-app frontend dashboard-app
docker compose -f docker/docker-compose.yml up -d identityservice admin-app frontend dashboard-app
docker compose -f docker/docker-compose.yml ps identityservice admin-app frontend dashboard-app
```

- [ ] **Step 4: Probe `/.well-known/openid-configuration`, the login page, and the MFA method endpoint; verify all expected HTTP status codes and no secrets in response/logs.
- [ ] **Step 5: Commit documentation and test evidence.**

```powershell
git add tests/e2e docs
git commit -m "test: verify adaptive passkey-first MFA flow"
```

## Completion Gate

The feature is complete only when all of the following are evidenced:

- Passkey is the primary user action after the email-authenticated login step.
- Unfamiliar devices prefer mobile approval, with a server-derived decision.
- TOTP is available as fallback without restarting OIDC authorization.
- Browser and native mobile completion preserve the original OIDC callback, state, nonce, and PKCE.
- Replay, expiry, user mismatch, and session mismatch are rejected.
- Shared foundation and mobile bridge own reusable capability seams; app components only compose them.
- Backend, Angular, Docker, and native verification gates are reported separately as PASS or UNVERIFIED.
