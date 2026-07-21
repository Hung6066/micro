# BFF Microservices Architecture — Design Spec

**Date**: 2026-07-21  
**Status**: Approved — Ready for Implementation  
**Author**: Lead System Architect  
**Classification**: PATH_FULL (fundamental architectural transformation)

---

## Executive Summary

Upgrade His.Hope from a monolithic YARP API Gateway to a **per-module BFF (Backend-for-Frontend)** architecture. Primary goals: **security** (remove JWT from browser storage via HttpOnly session cookies) and **API aggregation** (reduce frontend calls via BFF-composed responses). Migration uses parallel dual-running with feature flags — zero-downtime, per-module rollout.

---

## 1. Current State

| Layer | Detail |
|-------|--------|
| **Gateway** | Single YARP reverse proxy at `src/ApiGateway/` — routes `/api/v1/*` to 7 backend services |
| **Auth** | JWT access token in `sessionStorage` (XSS-vulnerable) + HttpOnly refresh cookie. `AuthInterceptor` attaches `Authorization: Bearer` header |
| **Frontend** | Angular 17 SPA — 13 API service files, ~60+ REST endpoints, 3 NgRx stores |
| **Backend** | 7 domain services (Identity, Patient, Appointment, Clinical, Lab, Billing, Pharmacy) + FHIR Gateway |
| **gRPC** | 21 internal RPC methods — not exposed to frontend |
| **Infra** | Linkerd mTLS, Cilium eBPF/Wireguard, Redis dual-cluster (single + 6-node), Vault, RabbitMQ, Tekton+ArgoCD |
| **Redis** | Used for JWT token blacklisting + hybrid L1/L2 cache (memory + Redis). `allkeys-lru` eviction |

---

## 2. Design Decisions Summary

| Decision | Choice | Rationale |
|----------|--------|-----------|
| **Primary motivation** | Security + Aggregation equally | Remove JWT from browser AND reduce API call count |
| **Approach** | Hybrid Smart BFF Router (Approach C) | YARP for pass-through, custom handlers for aggregation. Right-sized per endpoint |
| **Auth model** | HttpOnly session cookie → Redis → JWT injection | JWT never reaches browser. Backend services unchanged |
| **CSRF protection** | Double-submit cookie | `hishop_csrf` cookie + `X-CSRF-Token` header on mutations |
| **Migration strategy** | Parallel dual-running with Unleash feature flags | Old YARP paths and new BFF paths coexist. Per-module toggle. Instant rollback |
| **Gateway relationship** | YARP stays as edge layer | CORS, rate limiting, security headers, idempotency remain at edge |
| **BFF topology** | Per-module (6 BFFs + 1 DashboardBff) | Independent deployability, scaling, and team ownership |
| **Error handling** | HTTP 200 with `degraded[]` metadata | Clear, cacheable, Angular-friendly. 502 when all downstreams fail |
| **Session store** | Existing Redis cluster, new `session:*` keyspace | No new infrastructure. TTL auto-expiry matches JWT |
| **Auth migration order** | Auth cutover FIRST (Phase 1), BFF modules AFTER (Phases 2-7) | Single auth change, no dual-auth complexity in Angular |

---

## 3. Architecture

### 3.1 Target State (Post-Migration)

```
Angular SPA
  ↓ HttpOnly cookie (hishop_sid)
YARP Edge Gateway (port 5000)
  ├── /api/v1/auth/*  → IdentityService (direct)
  ├── /api/v1/admin/* → IdentityService (direct)
  ├── /api/v1/patients/*     → PatientBff   :5100
  ├── /api/v1/encounters/*   → ClinicalBff  :5200
  ├── /api/v1/lab-orders/*   → LabBff       :5300
  ├── /api/v1/invoices/*     → BillingBff   :5400
  ├── /api/v1/medications/*  → PharmacyBff  :5500
  ├── /api/v1/prescriptions/*→ PharmacyBff  :5500
  └── /api/v1/dashboard/*    → DashboardBff :5600
        │
        ↓ (cookie → Redis session → JWT injection → downstream)
        │
  Backend Services (unchanged JWT validation)
```

### 3.2 Dual-Path During Migration

```
YARP Edge Gateway
  ├── Legacy path (bff.{module}.enabled = false)
  │     → Direct proxy to backend service
  │     → Angular sends Bearer token in header
  └── BFF path (bff.{module}.enabled = true)
        → Route to BFF service
        → Angular sends HttpOnly cookie
        → BFF exchanges cookie → JWT → injects downstream
```

### 3.3 BFF Service Template

Each BFF uses an identical skeleton (~50 lines `Program.cs`):

```csharp
builder.Services.AddBffCore(config);        // session auth, CSRF, resilience
builder.Services.AddBffProxy(config);        // YARP with JWT transform
builder.Services.AddBffAggregation<T>();     // scan for IAggregationHandler
builder.Services.AddGrpcClient<TClient>(..); // per-module downstream clients

app.UseBffSessionAuth();                     // cookie → session → JWT
app.UseBffCsrfProtection();                  // X-CSRF-Token on mutations
app.MapBffReverseProxy();                    // pass-through routes
app.MapBffAggregation();                     // custom aggregation endpoints
```

### 3.4 BFF Inventory

| BFF | Port | Pass-through Routes | Aggregation Endpoints | gRPC Clients |
|-----|------|-------------------|----------------------|-------------|
| PatientBff | 5100 | 6 (CRUD + search) | `GET /{id}/timeline` | Patient, Clinical, Lab, Pharmacy |
| ClinicalBff | 5200 | 4 (search/get/start/complete) | `GET /{id}/full`, `GET /{id}/vitals` | Clinical, Patient |
| LabBff | 5300 | 7 (CRUD + submit/collect/result/cancel) | None (pure proxy) | Lab |
| BillingBff | 5400 | 5 (search/get/create/pay/void) | `GET /{id}/detailed` | Billing |
| PharmacyBff | 5500 | 9 (medications + prescriptions CRUD) | `GET /{id}/full` | Pharmacy |
| DashboardBff | 5600 | 0 (no proxy) | `GET /stats`, `GET /recent`, `GET /upcoming` | ALL 7 services |

---

## 4. Auth Model

### 4.1 Cookie Specification

```
Set-Cookie: hishope_sid=<opaque-session-id>;
  HttpOnly;           // JS cannot access
  Secure;             // HTTPS only
  SameSite=Lax;       // Sent on same-site + top-level navigation
  Path=/api;          // Only on API calls, not static assets
  Max-Age=3600;       // 1 hour, matches JWT expiry
```

### 4.2 Session Store (Redis)

```
Key:   session:{sid}
Value: {
  "userId": "usr_abc123",
  "jwt": "eyJhbGciOiJSUzI1NiIs...",
  "permissions": ["patients.view", "clinical.create", ...],
  "csrfToken": "random-opaque-token",
  "userAgentHash": "sha256(ua)",
  "issuedAt": "2026-07-21T10:00:00Z",
  "expiresAt": "2026-07-21T11:00:00Z"
}
TTL:   3600s (auto-expires)

Key:   session:user:{userId}
Value: Set of active session IDs (for force logout — delete all sessions for a user)
```

### 4.3 Per-Request Flow

```
1. Angular: fetch('/api/v1/patients/123')
   → Browser auto-attaches cookie: hishope_sid=abc...
2. YARP Edge: routes to PatientBff
3. PatientBff.SessionAuthMiddleware:
   a. Extract hishope_sid cookie
   b. Redis GET session:{sid} → session data
   c. Validate: not expired, user-agent hash matches
   d. Set HttpContext.Items["SessionJwt"] + ["Permissions"]
   e. Expired → 401 → Angular redirects to /auth/login
4. BFF → Backend:
   a. Proxy: YARP transform adds Authorization: Bearer <jwt>
   b. Aggregation: gRPC metadata injects JWT
5. Backend Service: validates JWT normally (no code changes!)
```

### 4.4 CSRF Protection

```
On login:
  → Set hishope_sid cookie (HttpOnly)
  → Set hishope_csrf cookie (NOT HttpOnly, SameSite=Strict)
  → Store csrfToken in Redis session

On mutation (POST/PUT/PATCH/DELETE):
  → Angular CsrfInterceptor reads hishope_csrf cookie
  → Sends X-CSRF-Token: <value> header
  → BFF validates header matches Redis session.csrfToken
  → Reject 403 if mismatch
```

### 4.5 Token Refresh (Transparent)

```
1. BFF detects JWT expired (Redis TTL or JWT exp claim)
2. BFF calls IdentityService: POST /api/v1/auth/internal/refresh { sessionId }
3. BFF updates Redis: session:{sid}.jwt = new JWT
4. BFF rotates session cookie (new sid → Set-Cookie)
5. Original request proceeds with new JWT
6. Angular never knows refresh happened
```

---

## 5. Aggregation Pattern

### 5.1 Handler Interface

```csharp
public interface IAggregationHandler
{
    string Route { get; }         // "/api/v1/patients/{id}/timeline"
    string Method { get; }        // "GET"
    Task<AggregationResult> HandleAsync(AggregationContext ctx);
}

public class AggregationResult
{
    public int StatusCode { get; }           // 200 or 502
    public object Data { get; }              // composed response body
    public DegradedField[] Degraded { get; } // partial failures
}
```

### 5.2 Parallel Fan-Out with Partial Failure

```csharp
public class PatientTimelineHandler : IAggregationHandler
{
    public async Task<AggregationResult> HandleAsync(AggregationContext ctx)
    {
        var results = await ParallelAggregationExecutor.RunAsync(new()
        {
            ["patient"] = () => _patientClient.GetPatientAsync(req),
            ["encounters"] = () => _clinicalClient.GetPatientEncountersAsync(req),
            ["labOrders"] = () => _labClient.GetPatientLabOrdersAsync(req),
            ["prescriptions"] = () => _pharmacyClient.SearchPrescriptionsAsync(req)
        });

        return results.Successes.Count > 0
            ? AggregationResult.Partial(new { data = results.Successes }, results.Failures)
            : AggregationResult.Failed("All downstreams unavailable");
    }
}
```

### 5.3 Response Envelope

```json
// Full success
{ "data": { "patient": {...}, "encounters": {...}, ... }, "degraded": [] }

// Partial failure (Clinical timed out)
{
  "data": {
    "patient": { "id": "123", "name": "John Doe" },
    "encounters": { "items": [] },
    "labOrders": { "items": [...] },
    "prescriptions": { "items": [...] }
  },
  "degraded": [{
    "field": "encounters",
    "reason": "ClinicalService timeout after 5s",
    "correlationId": "hh-abc123"
  }]
}
```

### 5.4 Error Classification Matrix

| Failure | HTTP | Angular Behavior |
|---------|------|-----------------|
| Cookie expired/missing | 401 | Redirect to `/auth/login` |
| CSRF mismatch | 403 | "Session expired, please refresh" snackbar |
| All downstreams down | 502 | "Service temporarily unavailable" banner |
| Circuit breaker open | 503 | "Service busy, retrying..." + auto-retry |
| Partial downstream failure | 200 + degraded[] | Show partial data + inline warning badges |
| Downstream 5xx (single call) | Passes through 5xx | Standard error snackbar |
| Redis unavailable | 503 | Auto-retry |

---

## 6. Resilience

### 6.1 Circuit Breakers (Polly)

Per downstream service, per BFF:

- **Retry**: 1 retry with exponential backoff + jitter (timeouts, connection failures, Unavailable gRPC)
- **Circuit Breaker**: 50% failure rate, min 10 throughput, 30s break, 60s sampling window
- **Timeout**: 5s per downstream call, 15s per aggregation (fan-out)

### 6.2 Health Checks

```
GET /health          → 200 (process alive)
GET /health/ready    → 200 if Redis reachable + ≥1 downstream healthy
GET /health/startup  → 200 if Redis connected + gRPC clients initialized
```

---

## 7. Migration Plan

### 7.1 Feature Flags (Unleash)

| Flag | Scope | Purpose |
|------|-------|---------|
| `bff.auth.cookie-only` | Global | Switch auth from Bearer → cookie-only |
| `bff.patient.enabled` | Per-module | Route /api/v1/patients/* via PatientBff |
| `bff.clinical.enabled` | Per-module | Route /api/v1/encounters/* via ClinicalBff |
| `bff.lab.enabled` | Per-module | Route /api/v1/lab-orders/* via LabBff |
| `bff.billing.enabled` | Per-module | Route /api/v1/invoices/* via BillingBff |
| `bff.pharmacy.enabled` | Per-module | Route /api/v1/medications+prescriptions/* via PharmacyBff |
| `bff.dashboard.enabled` | Per-module | Route /api/v1/dashboard/* via DashboardBff |

### 7.2 Phased Rollout (4 weeks)

```
Week 1: Prep + Auth Cutover
  Day 1-2: Bff.Core NuGet + Redis session schema + IdentityService dual cookie+Bearer
  Day 3-4: Auth cutover (bff.auth.cookie-only=true), remove Bearer from Angular

Week 2: LabBff → BillingBff → PharmacyBff (simplest first)
Week 3: ClinicalBff → PatientBff (multi-service fan-out)
Week 4: DashboardBff → Cleanup (remove legacy routes + flags)
```

### 7.3 Canary Rollout (per BFF)

```
0%   → Deploy BFF, validate health endpoints
10%  → 1 hour observation
25%  → 1 hour observation
50%  → 2 hour observation
100% → Full rollout

Auto-rollback triggers:
  → Error rate > 1% above baseline
  → p99 latency > 2x baseline
  → Redis connection failures
```

### 7.4 Rollback

```
Any module: Unleash set bff.{module}.enabled = false
           → YARP instantly routes back to direct service
           → Angular: cookie sent but ignored by old path (harmless)
           → Recovery: <1 second (Unleash polling interval)
```

---

## 8. Angular Changes

### 8.1 Files to Modify

| File | Change |
|------|--------|
| `auth.interceptor.ts` | **REMOVED** — no more Bearer token, no 401 refresh queue |
| `auth.service.ts` | `login()`: remove `sessionStorage.setItem`. `logout()`: just POST /auth/logout |
| `error.interceptor.ts` | 401 → redirect `/auth/login`. 403 → "session expired" snackbar. Handle degraded[] |
| `bff-router.service.ts` | **NEW** — resolves BFF vs legacy path per-request, controls credentials mode |
| `csrf.interceptor.ts` | **NEW** — reads `hishop_csrf` cookie, adds `X-CSRF-Token` on mutations |

### 8.2 Environment Config

```typescript
// environment.ts — unchanged
{ apiUrl: '/api/v1' }

// environment.prod.ts — unchanged
{ apiUrl: '/api/v1' }
```

URLs remain identical. BFF routing is transparent to components. Only `BffRouterService` + interceptors change.

---

## 9. Infrastructure

### 9.1 K8s Resources (per BFF)

- Deployment (2 replicas, non-root, read-only FS, Linkerd-injected)
- Service (ClusterIP, port per BFF)
- NetworkPolicy (ingress: only from api-gateway, egress: downstream services + Redis + DNS)
- CiliumNetworkPolicy (L3/L4 identity-aware, same rules)
- Linkerd Server + ServerAuthorization (mTLS identity-based)
- Vault AppRole policy (Redis session creds + JWT transit key read)
- ServiceProfile (route-level timeouts + retries)

### 9.2 Container

Follows existing multi-stage pattern: SDK build → aspnet:8.0 runtime. Non-root (UID 1654). Cosign-signed. SHA256 digest-pinned. Read-only root filesystem.

### 9.3 Redis Schema Extension

```
New keyspaces:
  session:{sid}              (session data including csrfToken + jwt, 3600s TTL)
  session:user:{userId}      (active session IDs set, for force logout)

Existing keyspaces unchanged:
  token_blacklist:*          (JWT revocation — still active since JWTs live on server side)
  user_revocation:*          (user-level token revocation timestamps — still active)
  Service cache keys         (patient:{id}, laborder:{id}, etc. — unchanged)

### 9.4 CI/CD

Each BFF gets its own Tekton CI pipeline (build → test → sign → deploy → smoke → contract-tests). ArgoCD Application per BFF with automated sync + self-healing. Linkerd TrafficSplit for canary deployment (90/10 → progressive shift).

---

## 10. Testing Strategy

### 10.1 Test Pyramid

| Layer | Framework | Coverage Target | Key Tests |
|-------|-----------|----------------|-----------|
| **Unit** | xUnit | ≥85% | SessionAuthMiddleware, CsrfValidator, aggregation handlers |
| **Integration** | xUnit + Testcontainers | Per-module | Redis session flow, gRPC client resilience, partial failure |
| **Contract** | xUnit + gRPC fixtures | Per BFF module | BFF→Backend proto contracts unchanged |
| **E2E** | Playwright | All critical paths | Dual-path (legacy + BFF), degraded state UI |
| **Load** | k6 | Per BFF module | 200 RPS, p95<500ms, BFF overhead <200ms |

### 10.2 Quality Gates

```yaml
bff_gates:
  security:
    cookie_httponly: true       # curl verify: Set-Cookie has HttpOnly
    csrf_enforced: true         # all POST/PUT/PATCH/DELETE require X-CSRF-Token
    no_jwt_in_browser: true     # verify JWT never in response body or headers
    session_ttl_enforced: true  # Redis session expires within JWT lifetime
  performance:
    bff_overhead_p95_ms: 200   # BFF adds ≤200ms at p95
    redis_session_hit_rate: 0.99 # Cache hit rate for session lookups
  reliability:
    degraded_ui_tested: true    # Playwright tests for degraded[] state
    circuit_breaker_tested: true # Integration test for breaker open state
```

---

## 11. Project Structure

```
src/
├── Bff/
│   ├── His.Hope.Bff.Core/              Shared library
│   │   ├── Authentication/
│   │   │   ├── SessionAuthMiddleware.cs
│   │   │   ├── SessionCookieOptions.cs
│   │   │   ├── CsrfValidator.cs
│   │   │   └── TokenExchangeService.cs
│   │   ├── Aggregation/
│   │   │   ├── IAggregationHandler.cs
│   │   │   ├── AggregationContext.cs
│   │   │   ├── AggregationResult.cs
│   │   │   └── ParallelAggregationExecutor.cs
│   │   ├── Resilience/
│   │   │   ├── BffResiliencePipeline.cs
│   │   │   └── PartialFailurePolicy.cs
│   │   ├── Proxy/
│   │   │   ├── JwtTransformProvider.cs
│   │   │   └── BffProxyConfigExtensions.cs
│   │   └── DependencyInjection.cs
│   │
│   ├── PatientBff/
│   ├── ClinicalBff/
│   ├── LabBff/
│   ├── BillingBff/
│   ├── PharmacyBff/
│   └── DashboardBff/
│
├── ApiGateway/                         Existing YARP edge (modified routes)
└── Services/                           Existing (unchanged)
```

---

## 12. Risks & Mitigations

| Risk | Severity | Mitigation |
|------|----------|------------|
| Auth migration breaks login | High | Dual cookie+Bearer during transition. E2E test both paths before cutover |
| BFF adds latency | Medium | k6 benchmarks before rollout. Parallel fan-out in aggregation. Redis session cache is sub-millisecond |
| Redis becomes SPOF for sessions | Medium | Existing Redis cluster is HA (3 masters + 3 replicas). Fallback: circuit breaker returns 503, Angular retries |
| Partial failure confuses users | Low | Playwright tests for degraded[] UI. Inline warning badges, not blocking modals |
| Session cookie theft (MITM) | Low | Secure flag + SameSite=Lax + mTLS (Linkerd) + Wireguard (Cilium). Defense in depth |
| BFF-to-BFF chaining anti-pattern | Low | Enforced by design: BFFs only call backend services, never other BFFs |

---

## 13. Dependencies & Prerequisites

| Dependency | Status |
|-----------|--------|
| Redis cluster (HA, 6 nodes) | Already deployed |
| Linkerd mTLS + ServerAuthorization | Already deployed |
| Cilium eBPF + Wireguard | Already deployed |
| Vault AppRole + Transit JWT | Already deployed |
| Unleash feature flags | Already integrated |
| Tekton CI + ArgoCD | Already deployed |
| Playwright E2E suite | Already exists |
| k6 load testing infrastructure | Already exists |

**No new infrastructure needed.** BFF leverages 100% existing platform.

---

## 14. Open Decisions

None — all decisions resolved during design review.

---

## 15. References

- [YARP Documentation](https://microsoft.github.io/reverse-proxy/)
- [OWASP Session Management Cheat Sheet](https://cheatsheetseries.owasp.org/cheatsheets/Session_Management_Cheat_Sheet.html)
- [OWASP CSRF Prevention](https://cheatsheetseries.owasp.org/cheatsheets/Cross-Site_Request_Forgery_Prevention_Cheat_Sheet.html)
- His.Hope Architecture: Clean Architecture + CQRS + DDD (see `docs/architecture.md`)
- His.Hope Security: JWT + Permission-based RBAC + Vault (see `docs/security.md`)
