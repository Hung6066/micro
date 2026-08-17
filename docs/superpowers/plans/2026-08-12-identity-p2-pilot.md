# Identity Service P2 Pilot Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Add a safe, observe-first device posture pilot control plane to Identity Service.

**Architecture:** Persist normalized, hashed device evidence and evaluate it through a small policy service. Expose admin assessment/preview and authenticated decision endpoints; do not integrate clinical API enforcement or vendor credentials in this phase.

**Tech Stack:** ASP.NET Core Minimal APIs, EF Core/PostgreSQL, ASP.NET authorization, xUnit.

## Global Constraints

- Default policy mode is `Observe`.
- Evidence TTL defaults to 15 minutes and expired evidence is non-compliant.
- Raw attestation/token/private-key material must never be persisted.
- All mutations require admin permission and emit audit events.
- No live Google/Windows connector is enabled by this plan.

---

### Task 1: Add posture domain and persistence

**Files:**
- Create: `src/Services/IdentityService/IdentityService.Domain/Entities/DevicePostureAssessment.cs`
- Create: `src/Services/IdentityService/IdentityService.Domain/Entities/DevicePosturePolicy.cs`
- Modify: `src/Services/IdentityService/IdentityService.Infrastructure/Persistence/IdentityDbContext.cs`
- Create: EF migration under `src/Services/IdentityService/IdentityService.Infrastructure/Persistence/Migrations/`

- [x] Define immutable assessment fields: id, user/device/provider, evidence hash, observed/expiry timestamps, normalized signal JSON, policy version, decision, correlation id.
- [x] Define policy fields: singleton key, mode, enabled providers, TTL seconds, required signals JSON, version, updated metadata.
- [x] Add DbSets, snake_case tables, bounded lengths, indexes on user/device/expiry and provider/expiry.
- [x] Generate and inspect migration SQL; do not modify existing migrations.

### Task 2: Implement evaluator and normalized evidence contract

**Files:**
- Create: `src/Services/IdentityService/IdentityService.Application/DevicePosture/DevicePostureContracts.cs`
- Create: `src/Services/IdentityService/IdentityService.Application/DevicePosture/DevicePosturePolicyEvaluator.cs`
- Create: `tests/Services/IdentityService/IdentityService.Application.Tests/DevicePosturePolicyEvaluatorTests.cs`

- [x] Normalize provider/device/signal names and reject empty ids or unknown providers.
- [x] Hash canonical evidence with SHA-256; reject raw token/certificate/private-key keys in input.
- [x] Evaluate `Observe`, `StepUp`, and `Deny`; expired, replayed, malformed, or missing required signals must not return compliant.
- [x] Add tests for default observe, opt-in deny, stale evidence, replay hash, and secret rejection.

### Task 3: Add pilot endpoints and audit boundary

**Files:**
- Create: `src/Services/IdentityService/IdentityService.Api/Endpoints/DevicePostureEndpoints.cs`
- Modify: `src/Services/IdentityService/IdentityService.Api/Composition/IdentityServiceEndpointExtensions.cs`
- Modify: `src/Services/IdentityService/IdentityService.Api/Composition/IdentityServiceRegistrationExtensions.cs`
- Create: `tests/Services/IdentityService/IdentityService.IntegrationTests/DevicePostureEndpointTests.cs`

- [x] Register evaluator and map `/api/v1/device-posture` endpoints.
- [x] Add admin `GET /policy`, `PUT /policy`, `POST /assessments`, and `POST /preview` endpoints.
- [x] Add authenticated `GET /decision/{userId}/{deviceId}` endpoint that returns decision, freshness, provider, and policy version only.
- [x] Enforce admin permission/facility scope on writes; validate provider allow-list and bounded payloads.
- [x] Emit append-only audit events with correlation id and redacted before/after values.
- [ ] Add integration tests for authorization, observe default, expiry and no raw secret persistence.

### Task 4: Verify and document pilot operations

**Files:**
- Modify: `docs/research/2026-08-12-identity-service-authentik-enterprise-capability-assessment.vi.md`
- Create: `artifacts/identity-p2-pilot-migrations.sql`

- [x] Regenerate migration script and confirm new tables/indexes.
- [x] Run targeted unit tests and Identity API build.
- [x] Document kill switch, break-glass behavior, evidence TTL, and live-gate prerequisites.
