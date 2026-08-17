# Fine-grained RBAC Integration Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Nâng cấp ủy quyền từ permission/role ở route và facility scope lên resource-aware, audit-able authorization cho toàn bộ microservices, sau đó đồng bộ entitlement/denial UX vào frontend foundation.

**Architecture:** Giữ permission catalog hiện tại làm RBAC nền. Bổ sung authorization context, resource evaluator và decision audit trong shared authorization package; mỗi service vẫn là PEP cuối cùng và tự sở hữu resource metadata/query scope. ABAC/ReBAC chỉ ở dạng contract và pilot shadow mode, không trở thành dependency production của P0.

**Tech Stack:** .NET 8, ASP.NET Core Authorization, EF Core, xUnit, Angular shared frontend foundation, Docker Compose.

## Global Constraints

- Mọi quyết định server-side phải fail closed; frontend không phải security boundary.
- Không đổi các port hiện tại, đặc biệt Identity Service `5001`.
- Không đưa PHI, secret hoặc raw policy details vào audit/log.
- Không thêm external PDP/OpenFGA runtime dependency trong P0.
- Mọi thay đổi phải giữ i18n/theme/share-foundation conventions hiện có.

### Task 1: Authorization context and resource contract

**Files:**
- Create: `src/Shared/Authorization/His.Hope.Authorization/AuthorizationContext.cs`
- Create: `src/Shared/Authorization/His.Hope.Authorization/AuthorizationDecision.cs`
- Create: `src/Shared/Authorization/His.Hope.Authorization/IResourceAuthorizationEvaluator.cs`
- Test: `tests/Shared/Authorization.Tests/AuthorizationContextTests.cs`

- [x] Write tests for normalized subject, action, tenant/facility and fail-closed missing resource metadata.
- [x] Implement immutable request/decision records and reason codes without exposing policy internals.
- [x] Run `dotnet test tests/Shared/Authorization.Tests/Authorization.Tests.csproj --no-restore` and require PASS (**23/23**).

### Task 2: Decision audit and authorization service registration

**Files:**
- Create: `src/Shared/Authorization/His.Hope.Authorization/IAuthorizationDecisionSink.cs`
- Create: `src/Shared/Authorization/His.Hope.Authorization/AuthorizationEvaluator.cs`
- Modify: `src/Shared/Authorization/His.Hope.Authorization/AuthorizationPoliciesExtensions.cs`
- Test: `tests/Shared/Authorization.Tests/AuthorizationEvaluatorTests.cs`

- [x] Test allow/deny for permission, facility mismatch, missing resource and unauthenticated principal.
- [x] Register evaluator and a redacting decision sink through dependency injection.
- [x] Ensure logs contain decision id/action/resource type but never PHI values or token contents.
- [x] Run the shared authorization test project (**23/23**).

### Task 3: Attach resource gates to domain services

**Files:**
- Modify: `src/Services/PatientService/PatientService.Api/Program.cs`
- Modify: `src/Services/ClinicalService/ClinicalService.Api/Program.cs`
- Modify: `src/Services/AppointmentService/AppointmentService.Api/Program.cs`
- Modify: `src/Services/LabService/LabService.Api/Program.cs`
- Modify: `src/Services/PharmacyService/PharmacyService.Api/Program.cs`
- Modify: `src/Services/BillingService/BillingService.Api/Program.cs`
- Modify: corresponding service handlers/DbContexts only where the resource check cannot be expressed in the endpoint.

- [x] Add resource authorization before read-by-id, update, delete, sign/approve/pay/dispense and export operations.
- [x] Derive facility/tenant from persisted resource and authenticated claims, never request body values.
- [x] Keep EF query filters as defense-in-depth and return the existing non-enumerating denial response.
- [x] Add one positive and one cross-scope negative **HTTP integration** test for the read-by-id gate in each service (Lab, Billing, Patient, Appointment, Clinical and Pharmacy now pass in Docker-network SDK runners); mutation-specific action denial tests now pass for all six services.

**Implementation checkpoint (2026-08-13):** Shared resource evaluator and generic EF metadata loader are implemented. Patient, Appointment, Clinical, Lab, Billing and Pharmacy high-risk commands now perform facility-aware resource checks and return non-enumerating `404` on denial. Service builds and rebuilt Docker images passed; database-backed read, gRPC and representative mutation denial tests are green in Docker-network SDK runners.

**Validation checkpoint (2026-08-13):** Shared authorization tests pass **23/23**, including scope claim parsing/policy composition, explicit human/workload principal type, FHIR scope substitution denial, EF-backed cross-facility and unknown-resource denials plus audited resource lookup failure. Domain application suites pass **267/267** (Patient 69, Appointment 46, Clinical 42, Lab 63, Billing 32, Pharmacy 60). Lab alert test fixtures were aligned with the current locking repository contract (`GetCurrentForUpdateAsync`); this corrected stale test setup rather than weakening production authorization.

Identity validation checkpoint: full `IdentityService.IntegrationTests` passes **128/128**, 0 skipped, in 1m17s when pointed at a native PostgreSQL local/CI service via `IDENTITY_TEST_POSTGRES_CONNECTION`. Docker Desktop/Testcontainers random host-port forwarding remains a Windows-only infrastructure caveat and is not used as pass evidence.

**Transaction checkpoint (2026-08-13):** Admin table export now enforces dual authorization (`admin.users.read` plus `reports.export`) before synchronous or queued export; disabling sensitive-column masking additionally requires `reports.manage`. Identity API build, image rebuild and internal login/provider smoke passed.

**Identity application checkpoint (2026-08-13):** `IdentityService.Application.Tests` passes **68/68** after correcting stale positional `LoginRequest` fixtures to named `Username`/`Password` arguments; no validator rule was changed. Focused Identity integration now includes Auth **13/13** and MFA **9/9**; the fixture uses per-session rate-limit keys and a bounded Testing-only auth limit so 429 tests do not contend with unrelated sessions.

**Frontend entitlement checkpoint (2026-08-13):** Admin users, roles and clients data tables bind their export affordance to the shared `reports.export` permission snapshot. The foundation snapshot now carries normalized OAuth scopes for UX-only `hasScope` checks; foundation Karma passes **54/54**, package build passes, and admin Karma/build pass (**13/13**). Frontend remains a UX optimization; server-side PEP decisions are authoritative.

**gRPC checkpoint (2026-08-13):** Patient, Billing, Appointment, Clinical, Lab and Pharmacy gRPC read/existence methods now use the shared resource evaluator before repository/mediator access. Six contract suites pass (**102 tests total**), including deny/no-repository-access checks and scoped list/search propagation. Database-backed gRPC allow/deny tests now pass **12/12** (2 per service) through in-process TestServer channels connected to Compose PostgreSQL via Docker-network SDK runners. Timestamp mapping was normalized to UTC in Clinical and Pharmacy projections where persisted values can be `Unspecified`.

**List/search checkpoint (2026-08-13):** Patient gRPC and HTTP list/search now derive `FacilityAccessScope` from the authenticated principal, include the scope in cache partitioning, and apply the facility predicate in `PatientRepository.SearchAsync`; an empty non-cross-facility scope returns an empty result fail-closed. Patient contract tests pass **15/15**, including explicit empty-scope propagation. Patient image was rebuilt/recreated healthy and `smoke-compose-internal.ps1` passed.

**Appointment list/search checkpoint (2026-08-13):** Appointment HTTP list/search and gRPC patient-appointment queries now derive `FacilityAccessScope` and pass it to facility-aware repository predicates; an empty non-cross-facility scope returns no rows. Appointment contract tests pass **15/15**, application tests **46/46**, API build passes, the container is healthy, and `smoke-compose-internal.ps1` passes.

**Clinical list/search checkpoint (2026-08-13):** Clinical encounter list/search, patient aggregation HTTP routes and gRPC search now derive `FacilityAccessScope`, partition cache keys by scope, and apply facility predicates in `EncounterRepository`; empty non-cross-facility scopes return no rows. Clinical contract tests pass **24/24**, application tests **42/42**, API build and container health pass, and internal compose smoke remains green.

**Lab list/search checkpoint (2026-08-13):** Lab order list/search and patient aggregation HTTP routes plus gRPC patient/search methods now derive `FacilityAccessScope`, partition cache keys by scope, and apply facility predicates in `LabOrderRepository`; empty non-cross-facility scopes return no rows. Lab contract tests pass **15/15**, application tests **63/63**, API build/container health pass, and internal compose smoke remains green.

**Billing list/search checkpoint (2026-08-13):** Invoice list/search and patient aggregation HTTP routes plus gRPC patient/search methods now derive `FacilityAccessScope`, partition cache keys by scope, and apply facility predicates in `InvoiceRepository`; empty non-cross-facility scopes return no rows. Billing contract tests pass **15/15**, application tests **32/32**, API build/container health pass, and internal compose smoke remains green.

**Pharmacy list/search checkpoint (2026-08-13):** Medication and prescription list/search HTTP routes, patient prescription aggregation, and gRPC search methods now derive `FacilityAccessScope`, partition cache keys by scope, and apply facility predicates in both repositories; empty non-cross-facility scopes return no rows. Pharmacy contract tests pass **18/18**, application tests **60/60**, API build/container health pass, and internal compose smoke remains green.

**Database-backed HTTP checkpoint (2026-08-13):** Lab, Billing, Patient, Appointment, Clinical and Pharmacy read-by-id authorization tests pass **12/12** (allow + cross-facility deny) against the running Compose PostgreSQL over `docker_default` using SDK runners. Host Testcontainers/random port forwarding remains environment-blocked; mutation-specific action tests remain a follow-up gate.

**Mutation HTTP checkpoint (2026-08-13):** Cross-facility denial tests pass **6/6** against Compose PostgreSQL: Patient deactivate, Appointment check-in, Clinical complete, Lab cancel, Billing void and Pharmacy fill. Each returns non-enumerating `404` before mutation execution.

**P2 posture checkpoint (2026-08-13):** Device posture policy evaluator tests pass **4/4** (required signals, deny mode, freshness/replay and provider validation). The runtime remains observe/pilot-only; Chrome Enterprise and Windows lab/vendor gates are not represented as local test passes.

**Workload token implementation checkpoint (2026-08-14):** Identity now registers resource-specific service scopes (`hishop:patients`, `hishop:appointments`, `hishop:clinical`, `hishop:lab`, `hishop:billing`, `hishop:pharmacy`) and explicitly handles OAuth2 client-credentials requests by issuing `principal_type=workload` plus requested scopes. Identity API build and `IdentityService.Application.Tests` pass **68/68**. End-to-end token issuance against the running Compose Identity instance remains an environment/live credential gate and is not claimed as pass.

**P2 authorization shadow checkpoint (2026-08-14):** Shared authorization now exposes an advisory `IAuthorizationShadowProbe`. `AUTHZ_PDP_MODE=shadow` emits coarse decision telemetry; `canary` is also non-granting, and PDP absence/errors cannot change the local fail-closed P1 decision. Authorization tests pass **25/25** and the Docker runtime contract validator passes for development.

**Post-change validation checkpoint (2026-08-14):** `smoke-compose-internal.ps1` passes all internal login/UI/401 gates; frontend-foundation Karma passes **54/54**; `validate-all-runtimes.ps1` returns `ALL_RUNTIME_ADAPTERS_VALIDATED`. Seven Google/Entra/SSF/mTLS/RADIUS/Chrome/Windows vendor gates remain explicitly skipped for missing prerequisites.

**Compose rollout checkpoint (2026-08-14):** Identity image was rebuilt/recreated with the shadow seam and compose wiring; `AUTHZ_PDP_MODE=disabled` is present in the running container, health is `healthy`, and internal smoke remains green. Host port `5001` is unchanged.

**Runtime adapter normalization checkpoint (2026-08-14):** Added `AUTHZ_PDP_MODE` to the Compose runtime example and VM/Compose renderer map; development renders `disabled`, staging renders `shadow`, production renders `disabled`. Rendered values and Docker runtime contract validation pass.

**Kubernetes overlay checkpoint (2026-08-14):** Added explicit `AUTHZ_PDP_MODE` overrides to dev (`disabled`) and staging (`shadow`) runtime ConfigMap patches; production inherits the base `disabled` value. The Kustomize validator now asserts the expected value per overlay, and validation passes for **dev, staging and prod**.

### Task 4: Protect platform and integration services

**Files:**
- Modify: `src/Services/IdentityService/IdentityService.Api/Endpoints/*.cs`
- Modify: `src/Services/FhirGateway/FhirGateway.Api/**/*.cs`
- Modify: `src/Services/ExternalIntegrationService/ExternalIntegrationService.Api/**/*.cs`
- Modify: `src/Services/DatabaseContinuityService/DatabaseContinuityService.Api/Program.cs`
- Modify: `src/Services/RemediationOperator/**/*.cs`

- [x] Separate human admin permissions from workload/service principals. The `HumanAdmin` policy is applied at `/api/v1/admin`; workload tokens must use explicit integration policies instead of inheriting interactive admin permissions.
- [x] Require explicit scopes for FHIR resource types and integration targets.
- [x] Keep database continuity/remediation mutations behind operator permission plus explicit workload scope; separate human/workload principal modeling remains a follow-up.
- [x] Add negative tests for cookie-to-SCIM misuse, wrong audience and missing workload scope.
- [x] Add controller-boundary and direct HTTP contract tests proving resource actions cannot be anonymous or omit their resource-specific policy (`FhirGateway.Contract.Tests`, **7/7**, including workload-principal denial).

**Platform scope checkpoint (2026-08-13):** Added shared `ScopeRequirement`/`ScopeHandler` with support for RFC-style `scope` and `scp` claims. FHIR Patient and Encounter endpoints now require both the existing clinical permission and explicit `fhir.patient.read`/`fhir.encounter.read` scopes plus `principal_type=human`; Database Continuity backup and restore-drill mutations now require `admin.settings.write`, `platform.continuity.write`, and an explicit `human` or `workload` principal type. Identity issues `principal_type=human` for interactive tokens and `principal_type=workload` for client-credentials tokens. Shared authorization tests pass **23/23**; FHIR Gateway and Database Continuity builds pass. Identity SCIM integration tests pass **9/9**, including cookie/admin-token rejection without SCIM scope. OIDC JWT audience validation is now enforced with a bounded allow-list; AspNetCore security tests pass **7/7** and runtime smoke remains green. FHIR controller-boundary and direct HTTP authorization contract tests pass **7/7** (401 unauthenticated, 403 missing scope/workload principal, 200 valid permission+scope); live client-credentials issuance and equivalent direct HTTP tests for the remaining services remain open.

**Principal separation checkpoint (2026-08-14):** Added shared `HumanAdmin` policy requiring `principal_type=human` and applied it to the Identity `/api/v1/admin` group. A workload principal carrying an admin permission is rejected while an explicitly typed human principal is accepted; shared authorization tests pass **26/26**.

**Administrative surface checkpoint (2026-08-14):** Extended `HumanAdmin` to Identity device-posture, provisioning, mobile-admin, mTLS bindings, RADIUS status, security-signal and settings route groups so alternate endpoint mappings cannot bypass human/workload separation. Identity build passes with 0 errors; rebuilt Compose identity is healthy and internal smoke passes.

### Task 5: Frontend foundation entitlement contract

**Files:**
- Modify: `shared/frontend-foundation/src/lib/permissions/permission.service.ts`
- Modify: `shared/frontend-foundation/src/lib/guards/permission.guard.ts`
- Modify: `shared/frontend-foundation/src/lib/i18n/dictionaries/en.ts`
- Modify: `shared/frontend-foundation/src/lib/i18n/dictionaries/vi-vn.ts`
- Test: existing foundation permission/guard specs.

- [x] Consume the server permission snapshot and `authz_version` without persisting sensitive tokens.
- [x] Add a single localized denial/expired-entitlement state using theme tokens and shared components.
- [x] Keep route guards and button visibility as UX optimization only; 401/403 handling remains server-driven.
- [x] Run foundation Karma tests and all three application builds/specs.

### Task 6: Validation and evidence update

**Files:**
- Modify: `docs/integration/identity-p0-p2-integration.vi.md`
- Modify: `docs/research/2026-08-13-fine-grained-rbac-standards-and-upgrade-blueprint.vi.md` only for implementation evidence.

- [x] Run shared authorization tests, targeted Identity tests, service tests and frontend tests separately to avoid Docker fixture contention.
- [x] Run `scripts/config/smoke-compose-internal.ps1` and preserve port 5001 evidence.
- [x] Run `scripts/config/validate-all-runtimes.ps1` and classify unavailable live/vendor gates honestly.
- [x] Run `git diff --check` on all changed files and record pass/fail/skipped evidence in the integration document.
