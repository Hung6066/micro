# His.Hope System Architecture Upgrade Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax.

**Goal:** Execute the three architecture upgrade phases sequentially: restore a clean P0 baseline, unify contracts and runtime discipline, then add operational and release evidence.

**Architecture:** Preserve the current domain services, OpenIddict identity service, BFF edge, Angular applications, and Capacitor mobile app. The upgrade removes protocol drift and unsafe runtime fallbacks at the existing seams instead of adding new services. Web uses BFF HttpOnly sessions, mobile uses Authorization Code + PKCE with secure native storage, and service-to-service calls use explicit contracts.

**Tech Stack:** .NET 8, ASP.NET Core, OpenIddict 5.7, EF Core/PostgreSQL, Redis, RabbitMQ/outbox, gRPC, Angular 21, Capacitor 7, Kubernetes/Kustomize, OpenTelemetry, Prometheus, Jaeger, Playwright.

## Global Constraints

- Preserve all pre-existing user changes in the dirty checkout; do not reset, checkout, or delete unrelated files.
- Production must fail fast on missing persistent keys, insecure issuer/redirect configuration, placeholder mobile pins, default credentials, and disabled security-critical dependencies.
- Web authentication converges on BFF HttpOnly session cookies; mobile authentication remains public-client Authorization Code + PKCE with Keychain/Keystore storage.
- Every behavior change gets a focused failing test before implementation; configuration-only edits are verified by build/configuration gates.
- Do not claim a phase complete until its full verification commands have fresh exit-zero evidence.

---

### Phase 1: P0 Baseline and Release Safety

#### Task 1: Restore solution dependency closure

**Files:**
- Modify: `src/Services/BillingService/BillingService.Application/BillingService.Application.csproj`
- Modify: `src/Services/FhirGateway/FhirGateway.Application/FhirGateway.Application.csproj`

- [ ] Add the same `His.Hope.Contracts` and `His.Hope.Validation` project references already used by the other Application projects.
- [ ] Run `dotnet build His.Hope.sln --no-restore --configuration Release` and confirm the missing namespace/PagedResult errors are gone.
- [ ] Run the affected Application project builds independently.

#### Task 2: Enforce production configuration policy

**Files:**
- Modify: `src/Services/IdentityService/IdentityService.Api/Configuration/OidcSecurityConfiguration.cs`
- Modify: `src/Services/IdentityService/IdentityService.Api/Composition/IdentityServiceRegistrationExtensions.cs`
- Modify: `src/Services/IdentityService/IdentityService.Api/appsettings.Production.json`
- Test: `tests/IdentityService/IdentityService.IntegrationTests/OidcSecurityConfigurationTests.cs`
- Test: `tests/IdentityService/IdentityService.IntegrationTests/OidcProductionConfigurationContractTests.cs`

- [ ] Add failing tests for missing persistent production signing/encryption material and insecure production redirect/issuer values.
- [ ] Run those tests and confirm the failures are caused by missing policy enforcement.
- [ ] Implement fail-fast production validation while preserving development-only ephemeral/local behavior.
- [ ] Run the focused Identity tests and inspect that no token or secret is logged.

#### Task 3: Make mobile release validation a mandatory gate

**Files:**
- Modify: `.github/workflows/platform-quality-gates.yml`
- Modify: `.github/workflows/security-quality-gate.yml`
- Modify: `scripts/validate-mobile-release.mjs`
- Modify: `scripts/prepare-mobile-release.mjs`
- Test: `mobile-app/src/app/core/mobile-runtime.spec.ts`

- [ ] Add a CI step that runs `npm run validate:mobile-release` after release preparation and before mobile artifact publication.
- [ ] Add a test/configuration check proving placeholder pinning cannot produce a release artifact.
- [ ] Run the gate once with placeholders and record the expected failure.
- [ ] Run preparation with explicit release environment variables, then run build and validation again.

#### Task 4: Remove production use of EnsureCreated and NoOp cache

**Files:**
- Modify: `src/Services/ClinicalService/ClinicalService.Api/Program.cs`
- Modify: `src/Services/PharmacyService/PharmacyService.Api/Program.cs`
- Test: the corresponding service integration test projects.

- [ ] Add failing production-environment tests for migration-runner registration and cache dependency health.
- [ ] Replace startup `EnsureCreated()` with the existing migration runner path.
- [ ] Remove `NoOpCacheService` registration from production execution paths.
- [ ] Run service integration tests and migration verification.

### Phase 2: Contract and Runtime Convergence

#### Task 5: Establish generated API contract entrypoints

**Files:**
- Create or modify: `src/Shared/Contracts/` generated-contract configuration and documentation.
- Modify: `admin-app`, `dashboard-app`, `src/Frontend/his-hope-app`, and `mobile-app` API adapters.
- Test: contract test projects and frontend API adapter tests.

- [ ] Define the authoritative OpenAPI/gRPC contract source and version policy.
- [ ] Generate TypeScript client types without duplicating DTO definitions in each app.
- [ ] Replace only the migrated endpoint adapters, keeping route behavior stable.
- [ ] Run protobuf breaking checks, contract tests, and all affected frontend builds.

#### Task 6: Complete OIDC token lifecycle and BFF session contract

**Files:**
- Modify: `src/Services/IdentityService/IdentityService.Application/OpenIddict/OpenIddictHandlers.cs`
- Modify: `src/Services/IdentityService/IdentityService.Infrastructure/Services/RedisRefreshTokenStore.cs`
- Modify: `src/Bff/His.Hope.Bff.Core/Authentication/`
- Test: `tests/IdentityService/IdentityService.IntegrationTests/OidcFlowTests.cs`
- Test: `tests/IdentityService/IdentityService.IntegrationTests/RedisRefreshTokenStoreTests.cs`
- Test: BFF integration test projects.

- [ ] Add failing tests for rotation, replay, concurrent refresh, family revocation, logout, and Redis failure behavior.
- [ ] Implement atomic consume/revoke semantics and stable standards-compatible errors.
- [ ] Verify browser clients do not receive access/refresh tokens and mobile clients never use localStorage.
- [ ] Run the full Identity/BFF integration matrix.

#### Task 7: Make migrations, cache, and security dependencies explicit

**Files:**
- Modify: shared infrastructure registration and service startup files.
- Modify: Kubernetes and Docker configuration.
- Test: service integration and configuration contract tests.

- [ ] Remove production fallback from Redis-backed rate limiting and session/security controls.
- [ ] Require explicit RabbitMQ, Redis, database, and Vault configuration in production.
- [ ] Validate migration ownership and one-service-one-database access boundaries.
- [ ] Run configuration, migration, and failure-mode tests.

### Phase 3: Operations, Resilience, and Release Evidence

#### Task 8: End-to-end observability and PHI-safe audit

**Files:**
- Modify: shared OpenTelemetry/Serilog/audit modules.
- Modify: `docker/otel-collector-config.yml`, Prometheus rules, dashboards, and runbooks.
- Test: observability and audit integration tests.

- [ ] Propagate correlation/trace IDs across HTTP, gRPC, and RabbitMQ.
- [ ] Add PHI/credential redaction assertions for logs, traces, and Sentry events.
- [ ] Add actionable SLO alerts and dead-letter/replay metrics.
- [ ] Run an end-to-end trace and audit verification.

#### Task 9: Chaos, backup/restore, and recovery evidence

**Files:**
- Create or modify: `tests/Resilience/`, `docs/runbooks/`, and Kubernetes recovery manifests.

- [ ] Test Redis, RabbitMQ, database, and Identity degradation paths.
- [ ] Verify PostgreSQL backup/restore, Redis session behavior, signing-key rotation, and message replay.
- [ ] Record RTO/RPO and rollback evidence in runbooks.

#### Task 10: Signed mobile and compliance release gates

**Files:**
- Modify: mobile release CI/CD and signing configuration.
- Modify: `scripts/validate-mobile-release.mjs`.
- Modify: `docs/operations/mobile-deployment.md`, security/compliance evidence docs.

- [ ] Require Android/iOS signing, App Links/Universal Links, OIDC redirect registration, and pin validation.
- [ ] Run accessibility, performance, offline/reconnect, and mobile OIDC tests.
- [ ] Publish a release evidence bundle only after every gate is green.

## Final Verification

- [ ] `dotnet build His.Hope.sln --configuration Release`
- [ ] `dotnet test His.Hope.sln --configuration Release --no-build`
- [ ] `npm run build:shared`
- [ ] `npm run lint`
- [ ] `npm run validate:mobile-release`
- [ ] `npx playwright test --workers=1`
- [ ] protobuf breaking checks and contract tests
- [ ] security scan, configuration scan, and release artifact inspection
