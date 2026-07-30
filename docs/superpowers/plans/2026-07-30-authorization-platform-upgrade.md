# Authorization Platform Upgrade Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox syntax for tracking.

**Goal:** Close authorization P0/P1 gaps and establish one versioned authorization contract across IdentityService, domain microservices, and Angular shared foundation.

**Architecture:** Keep IdentityService as issuer and source of identity/role/permission/facility membership. Enforce coarse permissions at endpoints and contextual resource scope in each domain service. Use one shared .NET authorization package and one frontend authorization snapshot contract; client state remains UX-only.

**Tech Stack:** ASP.NET Core 8, OpenIddict, EF Core/CockroachDB, Redis, Angular 21, TypeScript, Jest, xUnit/integration tests, Playwright.

## Global Constraints

- Preserve the four pre-existing dirty shared foundation files.
- Use test-first vertical slices at public seams.
- No client-only authorization for security decisions.
- No hardcoded secrets or PII in logs.
- Do not delete legacy authorization code until all references and tests prove the replacement is active.
- Run npx playwright test --workers=1 before claiming completion.
- Full-stack status remains partial when Docker/Testcontainers or browser gates are unavailable.

---

### Task 1: P0 endpoint authorization

**Files:**
- Modify: src/Services/IdentityService/IdentityService.Api/Composition/IdentityServiceEndpointExtensions.cs
- Modify: src/Services/IdentityService/IdentityService.Api/Endpoints/BulkImportEndpoints.cs
- Test: existing IdentityService endpoint/integration test project, or create the smallest project-level endpoint test if no test seam exists.

- [ ] Write a failing test proving authenticated non-admin cannot execute bulk import.
- [ ] Run the focused test and capture red failure.
- [ ] Require Permission:admin.users.read for preview and Permission:admin.users.write for import routes.
- [ ] Run focused tests and verify 401/403/2xx matrix.
- [ ] Inspect all /api/v1/admin child maps for authenticated-only mutations and add explicit policies where needed.

### Task 2: Persisted facility membership

**Files:**
- Modify: src/Services/IdentityService/IdentityService.Domain/Entities/User.cs
- Create/modify: facility membership entity/configuration and EF migration under IdentityService.Infrastructure/Persistence
- Modify: IdentityService.Application/OpenIddict/OpenIddictHandlers.cs
- Modify: IdentityService.Infrastructure/Facility/*
- Test: IdentityService facility authorization tests and migration/model tests.

- [ ] Write a failing test for user facility membership surviving DbContext reload.
- [ ] Add UserFacility membership with active state, primary flag, and unique user/facility constraint.
- [ ] Remove NotMapped from the persisted primary facility representation only after migration/model configuration exists.
- [ ] Generate/apply a backward-compatible migration without destructive data loss.
- [ ] Project active memberships into token claims with normalized facility IDs.
- [ ] Add negative tests for wrong facility and positive tests for authorized facility.
- [ ] Ensure missing context fails closed for protected domain queries.

### Task 3: Domain data/resource scoping

**Files:**
- Modify: domain aggregate/configuration and repositories for Patient, Clinical, Lab, Billing, Pharmacy.
- Create: shared transport-neutral AccessScope contract in the authorization package.
- Modify: application query handlers and gRPC adapters.
- Test: repository/application integration tests for cross-facility read/write/export.

- [ ] Write a failing test for cross-facility patient lookup/search.
- [ ] Add required persisted FacilityId/tenant ownership to the affected aggregates, with migration path.
- [ ] Inject AccessScope from authenticated claims and apply it before First/ToList/Count.
- [ ] Add resource checks for state transitions and sensitive operations.
- [ ] Verify gRPC paths enforce the same decision as REST.
- [ ] Run focused domain tests and inspect generated SQL/query shape where available.

### Task 4: One authorization package and fail-closed token semantics

**Files:**
- Canonical: src/Shared/Authorization/His.Hope.Authorization/*
- Legacy: src/Shared/Infrastructure/His.Hope.Infrastructure/Security/Authorization/*
- Modify: service registrations and csproj references only where required.
- Test: authorization handler/policy contract tests.

- [ ] Write tests for direct permission claim, missing claim, explicit compatibility fallback, and deny-by-default.
- [ ] Add claim constants, policy version/authz version, and a compatibility option defaulting off in production.
- [ ] Ensure all services use the canonical package.
- [ ] Remove or quarantine duplicate implementation only after references are absent.
- [ ] Add a registry contract test covering permission constants, policy names, and DB seed codes.
- [ ] Verify refresh/revocation behavior and avoid broad token logging.

### Task 5: Shared frontend authorization contract

**Files:**
- Modify: shared/frontend-foundation/src/auth/his-hope-permission.service.ts
- Modify: shared/frontend-foundation/src/auth/his-hope-auth-coordinator.ts
- Modify: shared/frontend-foundation/src/index.ts
- Modify: three app auth/bootstrap consumers.
- Test: shared Jest tests and app guard/interceptor tests.

- [ ] Write failing tests for unknown/loading/stale/denied snapshot states and logout clearing.
- [ ] Add AuthorizationSnapshot with subject, facility, permissions, roles, version, issuedAt, expiresAt, source.
- [ ] Make wildcard/evaluator behavior presentation-only and fail closed on unknown/stale.
- [ ] Rehydrate permissions from server/OIDC response through one adapter.
- [ ] Standardize 401/403/step-up/policy-stale handling without persisting authz in localStorage.
- [ ] Export only the stable public contract from src/index.ts.

### Task 6: Verification and release evidence

- [ ] Run affected unit tests.
- [ ] Run backend build/tests.
- [ ] Run frontend builds/tests.
- [ ] Run npx playwright test --workers=1.
- [ ] Run Docker/Testcontainers gates when available; classify environment blocks honestly.
- [ ] Run endpoint authorization inventory and git diff --check.
- [ ] Update the research report with implementation evidence and remaining gates.

