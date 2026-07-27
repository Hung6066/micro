# OIDC Enterprise Hardening Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Close the OIDC production gaps identified in the Identity Service so deployment uses persistent key material, secure environment-specific clients, standard revocation, and verifiable refresh-token replay protection.

**Architecture:** Keep OpenIddict as the standards-based authorization server. Use the existing Redis refresh-token family store for the custom token service, and add an explicit OpenIddict token-event bridge only where the OIDC flow can safely consume the same durable family/reuse policy. Keep production client registrations configuration-driven and reject insecure redirect URIs outside development.

**Tech Stack:** .NET 8, ASP.NET Core, OpenIddict 5.7, EF Core, Redis, Vault/KMS boundary, xUnit.

## Global Constraints

- Production must fail fast when persistent signing or encryption material is unavailable.
- Production client redirect and post-logout URIs must be HTTPS and exact; localhost HTTP is development-only.
- No access token, refresh token, client secret, or raw credential may be written to logs.
- Existing custom login/refresh behavior must remain backward compatible.
- Changes must be covered by focused tests and must not remove unrelated user changes.

---

### Task 1: Production key-material policy

**Files:**
- Modify: `src/Services/IdentityService/IdentityService.Api/Configuration/OidcSecurityConfiguration.cs`
- Modify: `src/Services/IdentityService/IdentityService.Api/Program.cs`
- Modify: `src/Services/IdentityService/IdentityService.Api/appsettings.Production.json`
- Test: `tests/IdentityService/IdentityService.Api.Tests/` or the existing Identity test project discovered by the worker

**Interfaces:**
- Produce an `OidcSecurityOptions` value that includes persistent signing and encryption credentials or a fail-fast production error.
- Preserve ephemeral credentials only for Development.

- [ ] **Step 1:** Add failing tests for production rejection when encryption key configuration is absent and development allowance of ephemeral keys.
- [ ] **Step 2:** Run the focused test and confirm it fails.
- [ ] **Step 3:** Implement the minimal configuration and OpenIddict wiring for persistent encryption material, with explicit key identifiers and production validation.
- [ ] **Step 4:** Run the focused tests and the Identity API build.

### Task 2: Environment-safe OIDC client registration

**Files:**
- Modify: `src/Services/IdentityService/IdentityService.Infrastructure/Persistence/IdentityDbInitializer.cs`
- Modify: `src/Services/IdentityService/IdentityService.Api/Program.cs` only if configuration binding is required
- Modify: `src/Services/IdentityService/IdentityService.Api/appsettings.Production.json`
- Modify: `src/Services/IdentityService/IdentityService.Api/appsettings.json`
- Test: existing Identity initializer/client registration tests

**Interfaces:**
- Production registrations contain only configured HTTPS redirect URIs.
- Development registrations may contain localhost URIs.

- [ ] **Step 1:** Add failing tests for rejecting HTTP/non-localhost production redirect URIs and for excluding localhost from production seed data.
- [ ] **Step 2:** Run focused tests and confirm failure.
- [ ] **Step 3:** Move redirect/post-logout URIs to environment configuration and validate exact URI schemes before creating/updating clients.
- [ ] **Step 4:** Run tests and inspect generated client descriptors.

### Task 3: Standard revocation and OIDC endpoint policy

**Files:**
- Modify: `src/Services/IdentityService/IdentityService.Api/Program.cs`
- Modify: `src/Services/IdentityService/IdentityService.Api/Endpoints/` only for the existing passthrough endpoint boundary
- Test: Identity API integration tests for discovery, revocation, and endpoint permissions

**Interfaces:**
- Discovery advertises `/connect/revoke`.
- Revocation is available only to valid clients and invalidates refresh/access token state according to the existing store policy.

- [ ] **Step 1:** Add failing integration tests for discovery revocation metadata and revoke behavior.
- [ ] **Step 2:** Run the tests and confirm failure.
- [ ] **Step 3:** Register the revocation endpoint and connect it to the existing OpenIddict persistence/session revocation boundary.
- [ ] **Step 4:** Run integration tests and inspect discovery output.

### Task 4: OIDC refresh-token rotation and reuse contract

**Files:**
- Modify: `src/Services/IdentityService/IdentityService.Api/Program.cs`
- Modify: `src/Services/IdentityService/IdentityService.Application/OpenIddict/OpenIddictHandlers.cs`
- Modify: `src/Services/IdentityService/IdentityService.Infrastructure/Services/RedisRefreshTokenStore.cs`
- Test: Identity token-flow tests

**Interfaces:**
- Every accepted OIDC refresh request has one-time-use semantics.
- Reuse revokes the token family and returns a standards-compatible invalid-grant response.

- [ ] **Step 1:** Add tests for first refresh success, second use rejection, family revocation, concurrent replay, and invalid-grant response.
- [ ] **Step 2:** Run focused tests and confirm failure or identify already-covered behavior.
- [ ] **Step 3:** Wire the OIDC refresh event to an atomic Redis consume operation without logging token material.
- [ ] **Step 4:** Run all token-flow tests and verify concurrent behavior.

### Task 5: Security policy and verification gates

**Files:**
- Modify: `src/Services/IdentityService/IdentityService.Api/appsettings.Production.json`
- Modify: `src/Services/IdentityService/IdentityService.Api/Program.cs`
- Test: security/configuration tests and existing Identity integration suite

**Interfaces:**
- Production rejects wildcard hosts, insecure issuer/redirect configuration, and disabled protections for privileged operations.
- MFA and rate-limit policy remains configurable but has an explicit production baseline.

- [ ] **Step 1:** Add failing tests for wildcard host and insecure production security settings.
- [ ] **Step 2:** Run focused tests and confirm failure.
- [ ] **Step 3:** Enforce host allowlist, privileged step-up/MFA baseline, secure cookie/data-protection settings, and durable audit failure behavior.
- [ ] **Step 4:** Run build, unit tests, integration tests, dependency/security checks, and document remaining environment-only gates.

### Task 6: Integration review

**Files:**
- Review all files changed by Tasks 1-5.

- [ ] **Step 1:** Confirm no agent changed unrelated modules or reintroduced development credentials.
- [ ] **Step 2:** Run the complete Identity test/build gate.
- [ ] **Step 3:** Run the OIDC negative-flow matrix: redirect tampering, PKCE downgrade, nonce/state mismatch, replay, revoke, logout, and unauthorized client scope.
- [ ] **Step 4:** Report evidence and any remaining Vault/KMS or deployment-only requirements.
