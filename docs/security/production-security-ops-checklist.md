# Production Security Ops Checklist

> **Audience:** Platform / security / release engineering  
> **Last updated:** 2026-08-21  
> **Related:** [frontend-enterprise-hardening.md](./frontend-enterprise-hardening.md), [cosign-image-signing.md](./cosign-image-signing.md), [penetration-test-plan.md](./penetration-test-plan.md), [bff-security-review.md](./bff-security-review.md)

This checklist is the operational counterpart to the frontend and backend hardening work. Complete every section before declaring the admin SPA and mobile client production-ready.

---

## 1. Pre-release gates (block ship if any fail)

| # | Check | Owner | Command / evidence |
|---|--------|-------|---------------------|
| 1.1 | Mobile release pins are real (no placeholders) | Mobile | `npm run validate:mobile-release` after `npm run prepare:mobile-release` |
| 1.2 | Android `certificate_pins.json` matches iOS plist and `environment.prod.ts` | Mobile | Diff the three artifacts in CI artifact bundle |
| 1.3 | Independent OIDC + pentest evidence present | Security | `./scripts/verify-independent-security-evidence.ps1` (mobile-release workflow) |
| 1.4 | Admin route guards + backend permission policies aligned | Identity | `dotnet test --filter AdminPermissionHardeningTests` |
| 1.5 | CSRF enforced on cookie-session mutating `/api/*` | Platform | `npm --prefix tests/e2e run test:security` (CSRF cases) |
| 1.6 | Container images digest-pinned and cosign-verified | DevSecOps | `./scripts/validate-production-image-signatures.ps1 -RequireSigned` |
| 1.7 | No high/critical npm audit findings in production deps | Frontend | `npm audit --omit=dev --audit-level=high` (security-quality-gate workflow) |

---

## 2. Mobile certificate pinning

### 2.1 Initial production setup

1. Obtain the production API TLS certificate (or intermediate) used by the API gateway ingress.
2. Compute SPKI pin (RFC 7469):

   ```bash
   openssl s_client -servername api.example.com -connect api.example.com:443 </dev/null 2>/dev/null \
     | openssl x509 -pubkey -noout \
     | openssl pkey -pubin -outform der \
     | openssl dgst -sha256 -binary \
     | openssl enc -base64
   ```

   Prefix the output with `sha256/` when storing.

3. Populate GitHub environment secrets for the mobile release workflow:
   - `HISHOPE_API_HOST`
   - `HISHOPE_API_ORIGIN` (must be `https://<host>`)
   - `HISHOPE_API_SPKI`
   - `HISHOPE_CERTIFICATE_PINS_JSON` — JSON array: `[{"host":"api.example.com","sha256Spki":"sha256/..."}]`

4. Run `npm run prepare:mobile-release` locally or via CI; verify:
   - `mobile-app/android/app/src/main/res/raw/certificate_pins.json`
   - `mobile-app/android/app/src/main/res/xml/network_security_config.xml`
   - `mobile-app/ios/App/App/HisHopeCertificatePins.plist`
   - `mobile-app/src/environments/environment.prod.ts`

5. Build signed APK/IPA and confirm release builds reject placeholder pins.

### 2.2 Pin rotation (zero-downtime)

| Step | Action | Rollback |
|------|--------|----------|
| T-30d | Add **backup** pin for new certificate to all pin bundles | Remove backup pin |
| T-14d | Ship mobile release containing **both** pins | Hold rollout |
| T-0 | Swap TLS certificate on API ingress | Revert cert; old pin still in field |
| T+30d | Remove retired pin after adoption >95% (telemetry or store version) | Re-add retired pin temporarily |

---

## 3. Android App Links & iOS Universal Links

### 3.1 Android

1. Extract release signing cert SHA-256:

   ```bash
   keytool -list -v -keystore release.keystore -alias <alias> | grep SHA256
   ```

2. Replace `REPLACE_WITH_RELEASE_SIGNING_CERT_SHA256` in `mobile-app/public/.well-known/assetlinks.json`.
3. Deploy to production host:

   ```
   https://mobile.<domain>/.well-known/assetlinks.json
   ```

4. Verify:

   ```bash
   curl -fsS https://mobile.<domain>/.well-known/assetlinks.json | jq .
   ```

### 3.2 iOS

1. Add Associated Domains entitlement in Xcode:
   - `applinks:mobile.<domain>`
2. Host Apple App Site Association at `https://mobile.<domain>/.well-known/apple-app-site-association`.
3. Test on physical device (simulator does not fully validate universal links).

---

## 4. Device attestation (mobile → Identity Service)

### 4.1 Current behavior (shipped)

- Native client calls `POST /api/v1/mobile/attestation` after login with **normalized boolean signals** (no raw vendor tokens).
- Providers: `play-integrity` (Android), `app-attest` (iOS).
- Backend stores evidence via device posture pipeline when policy enables the provider.

### 4.2 Production enablement

| Step | Action |
|------|--------|
| 0 | Set `PLAY_INTEGRITY_CLOUD_PROJECT_NUMBER` in Android Gradle for release attestation |
| 1 | Enable provider in device posture policy (`admin.settings.write`): add `play-integrity` and/or `app-attest` |
| 2 | Integrate vendor SDK verification (Play Integrity API, DeviceCheck/App Attest) in native layer |
| 3 | Map vendor verdict → signals (`device_secure`, `not_rooted`, …) before HTTP submit |
| 4 | Monitor `device-posture/assessments` for replay conflicts and deny decisions |
| 5 | Set production policy mode to `stepup` or `deny` only after false-positive burn-in |

### 4.3 Firebase App Check (optional layer)

1. Enable App Check in Firebase console for Android + iOS apps.
2. Restrict API keys to App Check–attested clients.
3. Add `firebase-app-check` provider to posture policy when backend verification is wired.

---

## 5. Admin-app security operations

| # | Control | Verification |
|---|---------|--------------|
| 5.1 | Route read guards on all IAM surfaces | Navigate as limited-role test user → `/forbidden` |
| 5.2 | Nav fail-closed until permission snapshot | Clear `/me/permissions` cache; nav hidden until loaded |
| 5.3 | CSP + security headers at ingress | `curl -I https://admin.<domain>/` → `Content-Security-Policy`, `Strict-Transport-Security` |
| 5.4 | CSRF on all cookie BFF mutations | E2E `@security Admin API CSRF gate` |
| 5.5 | Backend enforces same permissions as UI | Attempt LDAP sync / key rotation with read-only JWT → 403 |

### Limited-role smoke user (recommended)

Create a staging role with:

- `admin.users.read` only (no write, no settings.write, no provisioning.manage)

Confirm:

- `/users` loads
- `/roles` → forbidden
- `POST /api/v1/admin/ldap/sync` → 403

---

## 6. Identity Service permission matrix sign-off

Review and sign the mapping in [frontend-enterprise-hardening.md](./frontend-enterprise-hardening.md) Phase 3 against your IAM role templates.

High-risk endpoints to spot-check manually:

| Endpoint | Required permission |
|----------|---------------------|
| `POST /api/v1/admin/ldap/sync` | `admin.users.write` |
| `POST /api/v1/admin/security/rotate-signing-key` | `admin.settings.write` |
| `POST /api/v1/admin/provisioning/*` (mutate) | `admin.provisioning.manage` |
| `/api/v1/admin/security-signals/*` | `admin.security-signals.manage` |
| `GET/PUT/DELETE /api/v1/admin/tables/{resource}/views` | Resource-scoped read/write |

---

## 7. Container supply chain

1. All production images referenced by digest in Kustomize overlays.
2. Cosign sign + attest in `container-release` / `gitops-release-promotion` workflows.
3. Sigstore policy controller admission enforced in production cluster.
4. Harbor HTTPS + canary signature verification: `./scripts/validate-harbor-production.ps1`

See [cosign-image-signing.md](./cosign-image-signing.md) for key custody and rotation.

---

## 8. Observability & incident response

| Signal | Source | Alert |
|--------|--------|-------|
| CSRF 403 spike | API Gateway access logs | Possible CSRF bypass attempt or client bug |
| Admin 403 on mutate | Identity audit + app logs | Permission misconfiguration or privilege escalation attempt |
| Device posture `deny` | `device-posture/assessments` | Compromised device cluster |
| Mobile attestation replay | `409 replayed_evidence` | Replay attack or client bug |
| CSP violations | `security.csp-violation` audit events | XSS or misconfigured script |

Wire `HisHopeErrorReportingService` to production SIEM before go-live.

---

## 9. Independent validation

Before production cutover:

1. Execute [penetration-test-plan.md](./penetration-test-plan.md) scope (admin BFF, mobile OIDC, deep links, permission bypass).
2. Archive assessor reports where `verify-independent-security-evidence.ps1` expects them.
3. Track findings to closure; re-run targeted tests for any High/Critical fix.

---

## 10. Automated verification commands (local / CI)

```bash
# Backend permission regression
dotnet test tests/IdentityService/IdentityService.IntegrationTests \
  --filter "FullyQualifiedName~AdminPermissionHardeningTests"

# Mobile foundation (deep links, etc.)
npm run test:mobile-foundation

# Admin guard unit tests
npm --workspace admin-app run test -- --include=**/admin-read.guard.spec.ts

# Mobile attestation unit tests
npm --workspace @his-hope/mobile-app run test -- --include=**/mobile-device-attestation.service.spec.ts

# Security E2E (requires docker admin :8083 + identity :5001 + gateway :5000)
./scripts/restart-local-security-stack.ps1
npm run test:e2e:security

# Mobile release gate
npm run prepare:mobile-release   # with env secrets set
npm run validate:mobile-release
```

---

## Sign-off template

| Role | Name | Date | Notes |
|------|------|------|-------|
| Identity / IAM | | | Permission matrix reviewed |
| Mobile | | | Pins + attestation path verified |
| Platform / SRE | | | CSRF, CSP, cosign gates green |
| Security | | | Pentest complete; exceptions documented |
