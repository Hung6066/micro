# Identity Service Production Upgrade — Phase 1: Core OAuth2/OIDC

**Date:** 2026-07-23  
**Status:** Design Approved  
**Phase:** 1 of 5 — Core OAuth2/OIDC  
**Framework:** OpenIddict  
**Related ADR:** ADR 012 (RSA asymmetric JWT via Vault)

## 1. Problem Statement

The His.Hope Identity Service currently uses a custom JWT authentication system (HMAC-SHA256) with a hardcoded key fallback. It lacks OAuth2/OIDC standard protocols, has no gRPC contract (unlike all other microservices), and has minimal test coverage. This prevents interoperability, poses security risks, and blocks production certification.

**Goal:** Upgrade to a production-grade OAuth2/OIDC Authorization Server comparable to Keycloak, starting with Phase 1: Core OIDC infrastructure.

## 2. Current State Assessment

### Existing (Strong Foundation)
| Capability | Status |
|-----------|--------|
| ASP.NET Core Identity with 6 seeded users, 7 roles, 49 permissions | Complete |
| Custom JWT (HMAC-SHA256, 8h expiry) | Complete |
| Refresh token rotation with family-based reuse detection | Complete |
| Redis token blacklist (jti + user-level revocation) | Complete |
| TOTP MFA (RFC 6238) with recovery codes | Complete |
| BFF session cookies (HttpOnly + CSRF) | Complete |
| HIPAA audit logging (dual channel: Serilog + DB) | Complete |
| K8s deployment (3 replicas, HPA, PDB, distroless) | Complete |
| Account lockout (5 attempts → 15min) | Complete |
| Clean Architecture (Domain/Application/Infrastructure/Api) | Complete |

### Critical Gaps
| Gap | Impact | Phase |
|-----|--------|-------|
| No OAuth2/OIDC protocol (custom JWT only) | Cannot interoperate with standards | **Phase 1** |
| HMAC-SHA256 symmetric signing | Any service with the key can forge tokens | **Phase 1** |
| Hardcoded JWT key fallback in `JwtTokenGenerator.cs` | Security vulnerability | **Phase 1** |
| No gRPC proto contract for identity | Inconsistent with other services | **Phase 1** |
| Only 10 mock-based contract tests | No confidence in production readiness | **Phase 1** |
| No client management | No M2M auth, no third-party apps | Phase 2 |
| No social login / external IdP | No Google/Microsoft login | Phase 3 |
| No SCIM/LDAP federation | No HR system integration | Phase 4 |

## 3. Proposed Solution — Phase 1: Core OAuth2/OIDC

### 3.1 Framework: OpenIddict

**Why OpenIddict over Duende IdentityServer or custom build:**
- Open-source, no licensing cost
- .NET-native, integrates with ASP.NET Core Identity + EF Core
- OpenID Connect certification
- Actively maintained, used by Microsoft Orleans and Orchard Core
- Supports Vault-based signing natively

### 3.2 High-Level Architecture

OpenIddict runs inside the existing `IdentityService.Api` — no separate process. The existing Clean Architecture layers are preserved.

```
BEFORE (Custom JWT)                    AFTER (OIDC via OpenIddict)
─────────────────────                  ──────────────────────────────
POST /api/v1/auth/login       →       GET /connect/authorize + POST /connect/token
POST /api/v1/auth/refresh     →       POST /connect/token (grant_type=refresh_token)
POST /api/v1/auth/logout      →       POST /connect/logout
No discovery endpoint          →       GET /.well-known/openid-configuration
No JWKS endpoint               →       GET /.well-known/jwks
HMAC-SHA256 (shared key)      →       RS256 (Vault transit signing)
Custom token validation        →       POST /connect/introspect (gRPC)
No gRPC contract               →       identity.proto (6 RPC methods)
Hardcoded key fallback         →       Removed. Vault-only.
```

### 3.3 Architecture Layers

```
IdentityService.Api/
├── OpenIddict Endpoints (built-in middleware)
│   ├── GET  /.well-known/openid-configuration
│   ├── GET  /.well-known/jwks
│   ├── POST /connect/authorize
│   ├── POST /connect/token
│   ├── POST /connect/logout
│   └── POST /connect/introspect
├── gRPC Identity Service (new identity.proto)
│   ├── IntrospectToken
│   ├── GetUser
│   ├── CheckPermission
│   ├── CheckAnyPermission
│   ├── GetUserRoles
│   └── RevokeUserTokens
└── Legacy endpoints (retained, to be deprecated in Release N+1)
    ├── POST /api/v1/auth/login
    ├── POST /api/v1/auth/refresh
    └── POST /api/v1/auth/logout

IdentityService.Application/
├── OpenIddict Handlers
│   ├── CustomAuthorizationHandler (user lookup + MFA check)
│   ├── CustomTokenHandler (claims enrichment: permissions, roles, facility)
│   └── CustomIntrospectionHandler (per-service auth decisions)
└── VaultKeyProvider (RSA signing via Vault transit engine)

IdentityService.Domain/
├── Existing entities (User, Role, Permission, UserMfa) — NO CHANGES
└── New entities
    ├── OAuthClient (extends OpenIddictApplications metadata)
    └── OAuthScope (maps scopes → permission codes)

IdentityService.Infrastructure/
├── OpenIddict EF Core Stores (Applications, Authorizations, Scopes, Tokens)
├── VaultKeyService (RS256 sign/verify via Vault transit API)
└── Redis Cache (authorization codes, nonce store)
```

### 3.4 Database Changes

**Migration:** `022-oidc-openiddict.sql`

**New Tables (OpenIddict-managed):**

| Table | Purpose |
|-------|---------|
| `openiddict_applications` | OAuth2 clients: client_id, client_secret_hash, redirect_uris, grant_types, scopes |
| `openiddict_authorizations` | Authorization grants + consent: user ↔ application scope grants |
| `openiddict_scopes` | Scope catalog: openid, profile, email, hishope:permissions, hishope:* |
| `openiddict_tokens` | All token types: authorization_codes, access_tokens, refresh_tokens, device_codes. Replaces legacy refresh_token_store and refresh_tokens |

**Existing Tables — No Changes:**
- `asp_net_users`, `asp_net_roles`, `asp_net_user_roles`
- `permissions`, `role_permissions`, `user_mfa`
- `audit_logs`, `security_events`, `system_settings`
- `facilities`, `outbox_messages`, `login_attempts`

**Data Migration:**
- Active refresh tokens from `refresh_token_store` migrated to `openiddict_tokens`
- 7 roles × 49 permissions mapped to OIDC scopes
- Initial OAuth2 client seeded: `his-hope-spa` (SPA/BFF), `internal-services` (M2M placeholder)

### 3.5 OAuth2/OIDC Flows

#### Authorization Code + PKCE (Primary Flow)

```
1. BFF detects unauthenticated request
2. BFF redirects to GET /connect/authorize
   ?response_type=code
   &client_id=his-hope-spa
   &redirect_uri=https://his-hope.local/callback
   &code_challenge=<SHA256(verifier)>
   &code_challenge_method=S256
   &scope=openid profile email hishope:permissions
   &state=<random>
   &nonce=<random>
3. IdentityService validates: client, redirect_uri, scopes
4. If no session: redirect to login page (Angular SPA)
5. After login + MFA: generate authorization code
6. Code stored in Redis: key=<code>, value={user_id, client_id, scopes, nonce, code_challenge}, TTL=60s
7. Redirect back to BFF: /callback?code=<code>&state=<state>
8. BFF POSTs to /connect/token:
   grant_type=authorization_code
   code=<code>
   code_verifier=<PKCE verifier>
   client_id=his-hope-spa
9. OpenIddict validates:
   - Code exists in Redis (single-use: deleted after read)
   - PKCE verifier matches code_challenge
   - Client is active
10. Returns: access_token (RS256, 1h), id_token, refresh_token (7d), token_type=Bearer
11. BFF stores in Redis session, sets HttpOnly hishop_sid cookie
12. User is authenticated
```

#### Refresh Token Flow

```
1. Access token near expiry → BFF detects
2. BFF POSTs to /connect/token:
   grant_type=refresh_token
   refresh_token=<token>
   client_id=his-hope-spa
3. OpenIddict validates token, performs rotation:
   - Old token marked consumed
   - New access_token + new refresh_token issued
   - Family chain maintained for reuse detection
4. If reused token detected → revoke entire family → force re-authentication
```

#### Token Validation (Other Services via gRPC)

```
Service A receives Bearer token in request
  → Calls IdentityService.IntrospectToken(token) via gRPC
  → IdentityService validates:
      - Token signature (JWKS)
      - Token not expired
      - Token not revoked (Redis jti blacklist)
      - Token binding: (jti, user_id, ip_hash, client_id) matches
  → Returns IntrospectResponse { active, sub, permissions, roles, ... }
  → Service A caches result for token lifetime (1 hour max)
  → If gRPC call fails: circuit breaker opens → fail-closed (deny access)
```

### 3.6 Token Structure (RS256)

```json
{
  "alg": "RS256",
  "kid": "vault:v1",
  "typ": "at+jwt"
}
{
  "sub": "uuid",
  "iss": "https://identity.his-hope.local",
  "aud": "his-hope-services",
  "iat": 1690000000,
  "exp": 1690003600,
  "nbf": 1690000000,
  "jti": "unique-token-id",
  "client_id": "his-hope-spa",
  "scope": "openid profile email hishope:permissions",
  "permission": ["patients.view", "appointments.create", "..."],
  "role": ["Provider"],
  "fullName": "Dr. Nguyen Van A",
  "licenseNumber": "LIC-12345",
  "facilityId": "uuid",
  "auth_time": 1690000000,
  "amr": ["pwd", "mfa"]
}
```

**Signing:** Vault Transit API — private key never leaves Vault.  
**Key rotation:** Vault auto-rotates every 30 days. `kid` in JWT header identifies key version.  
**Validation:** Services verify via introspection (gRPC), not by downloading JWKS.

### 3.7 gRPC Identity Contract

File: `src/Shared/Protos/identity.proto`

```protobuf
syntax = "proto3";
package hishope.identity.v1;

service IdentityService {
  rpc IntrospectToken (IntrospectRequest) returns (IntrospectResponse);
  rpc GetUser (GetUserRequest) returns (GetUserResponse);
  rpc CheckPermission (CheckPermissionRequest) returns (CheckPermissionResponse);
  rpc CheckAnyPermission (CheckAnyPermissionRequest) returns (CheckAnyPermissionResponse);
  rpc GetUserRoles (GetUserRolesRequest) returns (GetUserRolesResponse);
  rpc RevokeUserTokens (RevokeUserTokensRequest) returns (RevokeUserTokensResponse);
}

message IntrospectRequest {
  string token = 1;
  string token_type_hint = 2;
}

message IntrospectResponse {
  bool active = 1;
  string sub = 2;
  string client_id = 3;
  int64 exp = 4;
  int64 iat = 5;
  string scope = 6;
  repeated string permissions = 7;
  repeated string roles = 8;
  string username = 9;
  string full_name = 10;
  string license_number = 11;
  string facility_id = 12;
  repeated string amr = 13;
  string jti = 14;
}

message GetUserRequest { string user_id = 1; }
message GetUserResponse {
  string user_id = 1; string username = 2; string email = 3;
  string full_name = 4; bool is_active = 5; bool mfa_enabled = 6;
  repeated string roles = 7; repeated string permissions = 8;
  string facility_id = 9;
}

message CheckPermissionRequest { string user_id = 1; string permission_code = 2; }
message CheckPermissionResponse { bool has_permission = 1; }

message CheckAnyPermissionRequest { string user_id = 1; repeated string permission_codes = 2; }
message CheckAnyPermissionResponse { bool has_any = 1; }

message GetUserRolesRequest { string user_id = 1; }
message GetUserRolesResponse { repeated string roles = 1; }

message RevokeUserTokensRequest { string user_id = 1; string reason = 2; }
message RevokeUserTokensResponse { int32 tokens_revoked = 1; }
```

### 3.8 Security Hardening

| Issue | Fix |
|-------|-----|
| Hardcoded JWT key `"super-secret-key..."` | Removed. Startup fails if Vault unreachable. |
| Symmetric HMAC → any service can forge | RS256 via Vault transit. Private key never exposed. |
| JWT leaked in login response body | Tokens only via BFF session cookies. Never in JavaScript-accessible context. |
| No token-to-IP binding | `jti` bound to `(user_id, ip_hash, client_id)` in Redis. Cross-IP replay rejected. |
| Authorization code replay | Single-use Redis keys. `GET` after `DEL`. |
| PKCE not enforced | Mandatory for all public clients. Request rejected without code_challenge. |
| No circuit breaker on identity | Polly circuit breaker on all gRPC callers. 3 failures in 30s → open. Fail-closed. |
| Minimal test coverage | Integration tests (Testcontainers + OpenIddict), OIDC conformance, security fuzz tests. |

### 3.9 Migration Plan (3 Releases)

| Release | Action |
|---------|--------|
| **N (this deliverable)** | OIDC endpoints go live alongside legacy. BFF upgraded to OIDC flow. gRPC introspection active. Dual-mode auth. |
| **N+1** | Legacy endpoints marked `[Obsolete]`. All clients migrated. Write path: OIDC only. |
| **N+2** | Legacy endpoints removed. Legacy token tables dropped. OIDC-only mode. |

### 3.10 Deliverables Checklist

- [ ] OpenIddict server package added to IdentityService.Api
- [ ] `/connect/authorize` endpoint (Authorization Code + PKCE flow)
- [ ] `/connect/token` endpoint (grant_types: authorization_code, refresh_token)
- [ ] `/connect/logout` endpoint with token revocation
- [ ] `/.well-known/openid-configuration` discovery endpoint
- [ ] `/.well-known/jwks` public key endpoint
- [ ] `VaultKeyProvider` — RS256 signing via Vault transit API
- [ ] `VaultHealthCheck` — fail-fast startup if Vault unreachable
- [ ] Hardcoded JWT key fallback removed from `JwtTokenGenerator.cs`
- [ ] `identity.proto` gRPC contract
- [ ] `IntrospectToken` gRPC implementation with Redis binding check
- [ ] `GetUser`, `CheckPermission`, `CheckAnyPermission`, `GetUserRoles`, `RevokeUserTokens` gRPC
- [ ] Circuit breaker (Polly) on all downstream service gRPC callers to IdentityService
- [ ] `GrpcPermissionHandler` replacement for current `PermissionHandler`
- [ ] Authorization code single-use guarantee (Redis GET+DEL)
- [ ] Token binding (jti + user_id + ip_hash + client_id) in Redis
- [ ] Audit logging for all token operations (issue, refresh, revoke, introspect)
- [ ] Database migration `022-oidc-openiddict.sql`
- [ ] Seed data: `his-hope-spa` client, OIDC scopes, scope→permission mappings
- [ ] BFF updated to use OIDC authorization code flow
- [ ] Legacy `/api/v1/auth/*` endpoints retained with deprecation notice
- [ ] Integration tests (Testcontainers: CockroachDB + Redis + IdentityService + OpenIddict)
- [ ] OIDC conformance tests (authorization code flow + refresh token)
- [ ] Security fuzz tests on authorize/token endpoints
- [ ] Contract tests for `identity.proto`
- [ ] Penetration test plan document
- [ ] ADR: OIDC architecture decision
- [ ] Updated API documentation (OpenAPI for REST, buf for gRPC)
- [ ] Updated deployment runbook (Vault dependency, key rotation procedure)
- [ ] Updated Helm chart (Vault init container, health check endpoints)
- [ ] Performance benchmarks (introspect latency <5ms p99, token issue <50ms p99)

## 4. Out of Scope (Phase 2-5)

| Feature | Phase |
|---------|-------|
| Client management UI / dynamic registration API | 2 |
| Client credentials grant (M2M auth) | 2 |
| Consent management | 2 |
| Admin Console API | 2 |
| Social login (Google, Microsoft) | 3 |
| Identity brokering / federation | 3 |
| SAML support | 3 |
| LDAP/AD user federation | 4 |
| SCIM v2 provisioning | 4 |
| FIDO2/WebAuthn/Passkeys | 5 |
| HSM-backed signing (hardware) | 5 |
| Full penetration test | 5 |

## 5. Risks & Mitigations

| Risk | Impact | Mitigation |
|------|--------|-----------|
| Vault outage → no token signing | Auth down | Vault HA cluster (3 nodes). Startup health check blocks deployment if unreachable. |
| OpenIddict upgrade breaks | Auth down | Pinned version in .csproj. Integration tests cover all flows. |
| Legacy clients can't migrate | Dual-mode period | 2-release overlap. Deprecation notices. Migration guide. |
| gRPC introspection latency | Slower auth | Response caching (token lifetime). Circuit breaker prevents cascading failure. |
| Database migration failure | Deployment blocked | Backward-compatible (new tables only, no ALTER on existing). Rollback: drop new tables. |

## 6. Success Criteria

1. OIDC conformance: authorization code flow passes OpenID Connect certification tests
2. Zero hardcoded secrets: all keys in Vault, startup fails without Vault
3. gRPC introspection latency: p99 < 5ms within cluster
4. Token issue latency: p99 < 50ms (authorization code → tokens)
5. Circuit breaker: 3 failures → open → all services fail-closed (no unauthorized access)
6. Test coverage: >80% line coverage on identity service, all OIDC flows covered by integration tests
7. Zero breaking changes to existing API (Release N)
8. ADR filed, docs updated, runbooks complete
