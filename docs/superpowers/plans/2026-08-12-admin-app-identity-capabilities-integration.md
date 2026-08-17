# Admin App Identity Capabilities Integration Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Integrate the newly available Identity Service P0/P1/P2 capabilities into admin-app with least-privilege UX, no browser-held vendor secrets, and complete contract/runtime validation.

**Architecture:** Angular admin-app consumes Identity Service admin endpoints through the existing `AdminApiService` and auth interceptor. Each capability gets a focused feature route and typed API facade. Server permissions remain authoritative; the UI hides unavailable actions but never treats hiding as authorization. P2 remains observe-only and preview-capable until a separately approved enforcement rollout.

**Tech Stack:** Angular standalone components, TypeScript strict mode, existing frontend-foundation components, ASP.NET Minimal APIs, Playwright/Karma tests.

## Global Constraints

- Admin-app never stores Google/Entra/SCIM tokens, Vault secrets, private keys, or raw device attestation.
- Every mutating action requires the server permission and a confirmation dialog with correlation id.
- Every table is facility-scoped by the server response; the UI must not merge or infer cross-facility data.
- P2 default is `observe`; UI must label `stepup`/`deny` as preview policy outcomes until enforcement is explicitly enabled.
- CSV downloads use server-issued short-lived responses and are audited.

## Current implementation status (2026-08-12)

The shipped admin experience intentionally consolidates P0/P1/P2 into one
Foundation-based workspace at `/identity-capabilities` (and the
`/security/identity` alias) instead of duplicating six nearly-identical shell
screens. The server remains authoritative for permissions and facility scope.

- Typed facade, normalized fail-soft errors, permission guard, forbidden state,
  P2 policy/preview/assessment table, provisioning queue/retry, mTLS metadata
  and revoke, RADIUS/SSF status, audit CSV export, i18n and theme-token checks
  are implemented and covered by the repository gates.
- P0/P1 screens listed below are represented as focused tabs in the workspace;
  they do not expose provider secrets, raw attestation, private keys or full
  evidence hashes.
- The remaining live-provider, PKI, receiver and Windows/Chrome gates are
  intentionally external prerequisites and remain skipped until credentials
  and a lab are supplied. Do not mark those boxes complete from a build alone.
- Facility-scoped list and mutation paths were hardened for posture
  assessments, provisioning jobs and mTLS bindings.

---

### Task 1: Establish typed admin capability API layer

**Files:**
- Modify: `admin-app/src/app/core/services/admin-api.service.ts`
- Create: `admin-app/src/app/core/models/identity-capabilities.models.ts`
- Create: `admin-app/src/app/core/services/identity-capabilities.service.ts`
- Test: `admin-app/src/app/core/services/identity-capabilities.service.spec.ts`

- [ ] Define typed models for password policy, audit rows, SCIM client scope, provisioning jobs, SSF subscriptions, mTLS bindings, RADIUS status, CSV jobs, and posture policy/assessment/decision.
- [ ] Add methods with exact server routes: `/api/v1/admin/device-posture/*`, `/api/v1/admin/provisioning/*`, `/api/v1/admin/audit-logs`, `/api/v1/admin/mtls/*`, `/api/v1/admin/radius/*`, and existing settings/export routes.
- [ ] Normalize errors into `{status, code, correlationId}` without logging response bodies containing secrets.
- [ ] Add request tests for auth headers, HTTP 401/403 handling, and no token persistence.

### Task 2: Add capability navigation and permission gates

**Files:**
- Modify: `admin-app/src/app/app.routes.ts`
- Modify: `admin-app/src/app/app.component.ts`
- Create: `admin-app/src/app/core/guards/capability-permission.guard.ts`
- Test: `admin-app/src/app/core/guards/capability-permission.guard.spec.ts`

- [ ] Add lazy routes under `/security/identity` for posture, provisioning, audit, federation, certificates, and exports.
- [ ] Map UI actions to server permissions: `admin.settings.read/write`, `admin.users.write`, `admin.audit.read`, `admin.clients.write`, and SCIM scope status.
- [ ] Render an explicit forbidden state for 403; do not redirect a forbidden admin to a generic dashboard.
- [ ] Add route tests proving a missing permission cannot instantiate mutation screens.

### Task 3: Implement P0 security administration screens

**Files:**
- Create: `admin-app/src/app/features/identity-security/password-policy-page.component.ts`
- Create: `admin-app/src/app/features/identity-security/federation-page.component.ts`
- Create: `admin-app/src/app/features/identity-security/scim-clients-page.component.ts`
- Create: `admin-app/src/app/features/identity-security/audit-log-page.component.ts`
- Test: matching `*.spec.ts` files

- [ ] Password history: show enabled/count/effective date and immutable audit outcome; no password or hash field is rendered.
- [ ] Federation: list immutable issuer/subject bindings, show collision/link status, and require re-auth confirmation for link/unlink.
- [ ] SCIM: show client id, scopes, last use and rotation state; token values are write-only and never displayed after save.
- [ ] Audit: server-side filters for actor/action/resource/outcome/correlation/facility, redacted before/after viewer, and CSV export action with dual permission.
- [ ] Tests cover redaction, 403, empty/error/loading states, and formula-injection-safe export handling.

### Task 4: Implement P1 interoperability screens

**Files:**
- Create: `admin-app/src/app/features/identity-security/provisioning-page.component.ts`
- Create: `admin-app/src/app/features/identity-security/ssf-page.component.ts`
- Create: `admin-app/src/app/features/identity-security/mtls-page.component.ts`
- Create: `admin-app/src/app/features/identity-security/radius-page.component.ts`
- Create: `admin-app/src/app/features/identity-security/export-jobs-page.component.ts`
- Test: matching `*.spec.ts` files

- [ ] Provisioning: show target mode (`disabled`, `dry-run`, `enabled`), queue/reconcile only with `admin.users.write`, immutable external-id bindings, retry/DLQ state and facility scope.
- [ ] SSF: show subscription URL/audience/status/last delivery/attempt count; create/revoke requires `admin.settings.write`; never show signing key material.
- [ ] mTLS: show normalized thumbprint, subject, EKU, expiry, revoked state and bind/revoke actions; certificate upload is PEM-only and server validates CA/EKU.
- [ ] RADIUS: show EAP-TLS enabled state and trust-CA health; no shared secret or private CA key input in browser.
- [ ] Export jobs: show queued/running/completed/expired state and short-lived download action; audit create/download/delete.
- [ ] Tests cover disabled-by-default adapters, stale/revoked certs, receiver outage status, retry state and download expiry.

### Task 5: Implement P2 pilot workspace

**Files:**
- Create: `admin-app/src/app/features/identity-security/device-posture-page.component.ts`
- Create: `admin-app/src/app/features/identity-security/device-posture-policy-dialog.component.ts`
- Create: `admin-app/src/app/features/identity-security/device-posture-assessment-table.component.ts`
- Test: matching `*.spec.ts` files

- [ ] Display current policy mode, providers, TTL, required signals, version and last update actor.
- [ ] Make `observe` the visible default; changing to `stepup` or `deny` requires explicit confirmation and warning that this is pilot policy only.
- [ ] Add preview form for provider/device/signals with normalized result (`observe`, `stepup`, `deny`, freshness, expiry, policy version).
- [ ] Add assessment table with provider provenance, evidence hash prefix only, freshness, expiry, decision and correlation id; never show raw evidence.
- [ ] Add kill-switch button that sets mode to `observe`, with audit confirmation and post-save reload.
- [ ] Tests cover stale evidence, replay conflict, provider allow-list, no raw secret rendering, and 403/409 states.

### Task 6: Runtime/config integration and validation

**Files:**
- Modify: `admin-app/src/environments/environment.ts`
- Modify: `admin-app/src/environments/environment.prod.ts`
- Create: `scripts/validate-admin-identity-capabilities.ps1`
- Modify: `docs/research/2026-08-12-identity-service-authentik-enterprise-capability-assessment.vi.md`

- [ ] Use `SERVICE_IDENTITY_URL`/public OIDC authority from the runtime contract; no environment-specific localhost fallback in production builds.
- [ ] Validate Docker Compose, VM env rendering, Kustomize overlays and admin-app API origin against the same contract.
- [ ] Run `npm ci`, Angular unit tests, production build, Playwright with workers=1, Identity API build, targeted integration tests, and static secret/route checks.
- [ ] Record each gate as pass, fail, unavailable, or environment-blocked; live Google/Entra/SSF/Vault/Windows gates require explicit credentials/lab evidence.
