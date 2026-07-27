# Identity and Frontend Contract Synchronization Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Synchronize Identity Service and all Angular clients around typed, authorized, auditable contracts.

**Architecture:** Identity Service owns authentication, permission, facility scope, mutation validation, and durable audit. Angular foundation owns interaction contracts; app adapters own HTTP calls and map server results into shared DataTable state.

**Tech Stack:** ASP.NET Core 8, OpenIddict, ASP.NET Identity, EF Core/PostgreSQL, Angular 19, RxJS, Playwright, Docker Compose.

## Global Constraints

- Backend authorization remains mandatory for every sensitive operation.
- Bulk/export payloads contain `rowKeys` and query state, not full PHI rows.
- OIDC redirect URLs are same-origin and client-registration validated.
- API errors use RFC 7807-style ProblemDetails with correlation IDs.
- Shared UI changes must preserve Angular standalone component APIs unless the contract is intentionally versioned.

---

### Task 1: Identity contract verification and hardening

**Files:**
- Inspect/modify: `src/Services/IdentityService/IdentityService.Api/Program.cs`
- Inspect/modify: `src/Services/IdentityService/IdentityService.Api/Endpoints/*.cs`
- Test: `tests/IdentityService/IdentityService.IntegrationTests/*`

- [ ] Verify session-status is authenticated, same-site safe, and returns a stable DTO.
- [ ] Verify admin endpoints use permission policies and bounded page/pageSize values.
- [ ] Add/extend tests for unauthorized, forbidden, invalid query, and conflict responses.
- [ ] Run the Identity integration test project.

### Task 2: Typed frontend identity/admin adapter

**Files:**
- Modify: `admin-app/src/app/core/services/admin-api.service.ts`
- Create/modify: `admin-app/src/app/core/services/api-problem-details.ts`
- Modify: `admin-app/src/app/core/services/auth-interceptor.service.ts`
- Test: `admin-app/src/app/core/services/*.spec.ts`

- [ ] Define typed page query/result, ProblemDetails, bulk, export, and audit DTOs.
- [ ] Map 401 to session recovery, 403 to access denied, and 409 to conflict UI.
- [ ] Expose server-query methods for users, roles, clients, consents, settings, and audit logs.
- [ ] Keep client secrets write-only and never render them after initial response.

### Task 3: Bind admin pages to server DataTable contracts

**Files:**
- Modify: `admin-app/src/app/features/{clients,users,roles,consents}/*.ts`
- Modify: `shared/frontend-foundation/src/ui/his-hope-data-table.component.ts`
- Test: `tests/e2e/specs/shared-foundation.spec.js`

- [ ] Pass `mode="server"`, `totalItems`, and `query` to each table.
- [ ] Handle `queryChange`, bulk requests, export requests, saving state, and reloads through `AdminApiService`.
- [ ] Send only `rowKeys` to mutation/export APIs.
- [ ] Preserve mobile item presentation and permission-aware actions.

### Task 4: CI security and contract gates

**Files:**
- Create/modify: `.github/workflows/*`
- Modify: `tests/e2e/package.json`
- Create: `scripts/validate-frontend-security.ps1`
- Docs: `shared/frontend-foundation/docs/INTEGRATION.md`

- [ ] Run shared build, Angular builds, Identity tests, axe, responsive Playwright, CSP/header probes, and dependency audit.
- [ ] Fail on critical dependency vulnerabilities unless an explicit documented exception exists.
- [ ] Document release evidence and remaining external gates.
