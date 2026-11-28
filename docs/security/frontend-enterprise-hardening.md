# Frontend Enterprise Hardening — admin-app & mobile-app

> **Date:** 2026-08-21  
> **Scope:** Phase 0–2 security implementation for Angular admin SPA and Capacitor mobile client

## Phase 0 — Production blockers

| Item | Status | Location |
|------|--------|----------|
| Android SPKI pin algorithm (RFC 7469) | Done | `mobile-app/android/.../HisHopeSpkiPin.java` |
| Android HTTPS via native pinned transport (prod) | Done | `mobile-native-http.interceptor.ts` |
| Bundled pin allow-list (Android raw + iOS plist) | Done | `certificate_pins.json`, `HisHopeCertificatePins.plist` |
| CI pin injection script | Done | `scripts/prepare-mobile-release.mjs` (legacy wrapper: `scripts/inject-mobile-cert-pins.mjs`) |
| Android OIDC WebView hardening | Done | `OidcAuthActivity.java` |
| Encrypted PIN storage (Android) | Done | `HisHopeSecurePrefs.java` |
| PIN Keychain storage (iOS) | Done | `HisHopeSecurityPlugin.swift` |
| iOS `isPinConfigured` bug fix | Done | `HisHopeSecurityPlugin.swift` |
| PIN brute-force lockout | Done | Android + iOS native plugins |
| Admin route read guards | Done | `admin-read.guard.ts` |
| CSRF fail-closed (BFF mutate) | Done | `his-hope-cookie-session.interceptor.ts` |

### Release checklist (mobile)

1. Set the production release inputs documented in `production-security-ops-checklist.md` and run `npm run prepare:mobile-release`. The legacy `npm run inject:mobile-cert-pins` command delegates to the same fail-closed flow.
2. Replace signing cert fingerprint in `mobile-app/public/.well-known/assetlinks.json`
3. Configure iOS Associated Domains for `mobile.his-hope.example`
4. Verify release build rejects placeholder pins

## Phase 1 — Hardening

| Item | Status | Location |
|------|--------|----------|
| Admin fail-closed navigation | Done | `admin-app/src/app/app.component.ts` |
| Admin CSP + security headers | Done | `admin-app/nginx.conf` |
| Deep link path restriction | Done | `native-capability.service.ts` |
| App Links template | Done | `mobile-app/public/.well-known/assetlinks.json` |
| Mobile MFA/notifications guards | Done | `mobile-app/src/app/app.routes.ts` |
| Mobile write-denied → forbidden | Done | `mobile-write.guard.ts` |

## Phase 2 — Operations

See `penetration-test-plan.md`, `cosign-image-signing.md`, and backend BFF security review for infra follow-ups.

## Phase 3 — Backend permission enforcement (2026-08-21)

| Item | Status | Location |
|------|--------|----------|
| Table views gated by resource permission | Done | `TableViewEndpoints.cs`, `AdminTableResourceAuthorization.cs` |
| LDAP sync requires `admin.users.write` | Done | `IdentityServiceEndpointExtensions.cs` |
| Signing key rotation requires `admin.settings.write` | Done | `IdentityServiceEndpointExtensions.cs` |
| Provisioning mutations require `admin.provisioning.manage` | Done | `DirectoryProvisioningEndpoints.cs` |
| Security signals require `admin.security-signals.manage` | Done | `SecuritySignalAdminEndpoints.cs` |
| Audit ingestion tiered by action permission | Done | `AuditLogEndpoints.cs` |
| Device posture decision self-or-admin read | Done | `DevicePostureEndpoints.cs` |
| Mobile attestation endpoint (normalized evidence) | Done | `POST /api/v1/mobile/attestation` |
| Mobile attestation providers | Done | `play-integrity`, `app-attest`, `firebase-app-check` |
| Admin route/nav aligned with backend policies | Done | `admin-route-permissions.ts`, `app.component.ts` |
| Endpoint policy regression tests | Done | `AdminPermissionHardeningTests.cs` |
| Android raw pin bundle in release prep | Done | `prepare-mobile-release.mjs`, `validate-mobile-release.mjs` |

| Mobile attestation client | Done | `mobile-device-attestation.service.ts` |
| Security E2E suite | Done | `tests/e2e/specs/security-hardening.spec.js` |
| Production ops checklist | Done | [production-security-ops-checklist.md](./production-security-ops-checklist.md) |

### Still requires production ops (not code)

See [production-security-ops-checklist.md](./production-security-ops-checklist.md) for the full runbook. Summary:

### Certificate pin rotation

1. Compute SPKI pin for new certificate
2. Add backup pin before rotation window
3. Run `npm run inject:mobile-cert-pins` in CI
4. Ship mobile release before cert swap
5. Remove retired pin after adoption >95%
