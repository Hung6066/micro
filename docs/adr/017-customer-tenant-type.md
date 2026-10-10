# ADR 017: Customer Tenant Type, Vendor Support Cross-Tenant, and Portal Classes

## Status

Accepted — 2026-08-24

## Context

[ADR 016](./016-conglomerate-tenant-model-azure.md) establishes a conglomerate identity plane
with three **internal** legal-entity tenants (`manufacturing`, `tech-vendor`, `group-hq`),
OAuth client binding, default-deny cross-tenant access, and HQ operator narrowing via
`?scopeId=`. That model does not yet describe:

1. **B2B customer organizations** that consume platform services as external tenants.
2. **Vendor support** access from `tech-vendor` into customer tenants without granting
   customer membership to internal staff.
3. **End-user portals** (customer admins, patient users) versus **operator consoles**
   (group HQ, vendor ops, internal IAM).

Customer tenants must remain **sovereign** (default deny, PEP-enforced `tenant_id`) while
allowing auditable, least-privilege support and group oversight. Reusing internal HQ
switcher semantics for customer end-users would violate least privilege and confuse audit
boundaries.

This ADR extends ADR 016; it does not replace tenant binding, `tenant_membership` claims, or
cross-tenant default deny.

## Decision

### 1. Tenant classification (extends `IamScope`, does not replace hierarchy)

Keep the existing scope hierarchy from ADR 016:

```
organization → tenant → account → environment
```

All legal entities—internal and customer—remain `Kind = "tenant"`. Classification is carried
in **tenant profile metadata**, not a new scope kind (avoids breaking
`IamTenantScopeResolver`, workload-role audience resolution, and hierarchy validation in
`IamControlPlaneEndpoints`).

**Schema extension**

| Field | Location | Values | Purpose |
|-------|----------|--------|---------|
| `tenantClass` | `ConglomerateTenantOptions` + optional `IamScope.MetadataJson` | `internal`, `customer` | Distinguish group entities vs B2B customers |
| `operatorHome` | customer tenant config only | `tech-vendor` \| `group-hq` | Which internal tenant owns provisioning/support contract |
| `portalPolicy` | customer tenant config | see §4 | Allowed OAuth client classes for this tenant |

`MetadataJson` on `IamScope` (when persisted) mirrors config for admin UI display:

```json
{
  "tenantClass": "customer",
  "operatorHome": "tech-vendor",
  "contractId": "cust-acme-2026",
  "dataRegion": "ap-southeast-1"
}
```

**Naming**

- Tenant keys: `customer-{slug}` (e.g. `customer-acme`).
- Internal keys unchanged (`manufacturing`, `tech-vendor`, `group-hq`).

**Isolation invariant (unchanged)**

- Every business resource carries `TenantId` / scope subtree of exactly one tenant.
- Token `tenant_id` must match resource tenant unless an explicit cross-tenant policy pair
  allows the action (read-only by default).

### 2. User ↔ client ↔ tenant relationships for customers

| Principal | `tenant_membership` | Login client | Token `tenant_id` | Admin switcher |
|-----------|---------------------|--------------|-------------------|----------------|
| Customer org admin | `customer-acme` only | `customer-acme-portal` | `customer-acme` | **No** |
| Customer end-user | `customer-acme` only | `customer-acme-app` | `customer-acme` | **No** |
| Tech-vendor support | `tech-vendor` only | `tech-console` | `tech-vendor` | Internal tenants only |
| Group HQ operator | `group-hq` (+ optional others) | `group-hq-admin` | `group-hq` | Internal + customer (policy-gated) |
| Vendor support into customer | **No** customer membership | `tech-console` + JIT session | stays `tech-vendor` | Narrow via `scopeId` + JIT grant |

**Rules**

1. **Never** grant internal staff standing `tenant_membership` on customer tenants for
   routine support. Use cross-tenant policy + JIT/break-glass (existing access-governance
   surfaces) instead.
2. Customer users **never** receive membership on internal tenants.
3. OAuth client binding remains **one client → one tenant** (ADR 016). Multi-entity customer
   groups get **one tenant per legal entity**, each with its own portal client—not one token
   spanning entities.
4. Customer users with roles in two customer tenants hold two memberships and must log in
   through the matching portal client per tenant (same pattern as multi-membership internal
   users).

### 3. Cross-tenant pairs for vendor support

Extend `crossTenantPolicy.allowedPairs` in `config/conglomerate/iam-scopes.v1.json` (or a
dedicated `customer-tenant-policy.v1.json` merged at startup) with **class-aware** pairs.

**Default matrix**

| Source | Target class | Allowed permissions | Mutation | Notes |
|--------|--------------|---------------------|----------|-------|
| `group-hq` | `internal` | `admin.audit.read` | No | ADR 016 (existing) |
| `group-hq` | `customer` | `admin.audit.read` | No | Group oversight |
| `tech-vendor` | `customer` where `operatorHome=tech-vendor` | `admin.audit.read`, `admin.users.read`, `identity.view` | No* | Vendor support read |
| `tech-vendor` | `customer` | `admin.users.write`, `identity.update` | Yes** | JIT only |

\* Read pairs may be standing policy for contracted vendor support.  
\*\* Write pairs require active **JIT access request** or **break-glass** approval; never
standing cross-tenant write.

**Pair schema extension**

```json
{
  "source": "tech-vendor",
  "target": "customer-acme",
  "targetClass": "customer",
  "operatorHomeMatch": true,
  "reason": "vendor-support-read",
  "permissions": ["admin.audit.read", "admin.users.read", "identity.view"],
  "requiresJit": false
}
```

```json
{
  "source": "tech-vendor",
  "targetClass": "customer",
  "operatorHomeMatch": true,
  "reason": "vendor-support-write",
  "permissions": ["admin.users.write", "identity.update"],
  "requiresJit": true,
  "maxDurationMinutes": 60
}
```

**Enforcement (builds on existing guards)**

1. `ConfigurableCrossTenantAccessPolicy` — evaluate `targetClass`, `operatorHomeMatch`,
   `requiresJit`.
2. `IamTenantAccessGuard.EnsureCrossTenantRead` — unchanged entry point for IAM admin reads.
3. **Mutations** into customer tenants from internal sources: reject unless JIT/break-glass
   artifact is present on the request context (new `SupportElevationContext` checked in
   `WithTenantMutationScope` for cross-tenant targets only).
4. All cross-tenant actions emit `authorization-control-plane` audit with `sourceTenant`,
   `targetTenant`, `elevationId`.

**Customer ↔ customer**

- Default **deny**. No cross-customer pairs except explicit federation contracts (future ADR).

### 4. Portal classes: operator vs end-user

Introduce **`portalClass`** on OAuth client registration (config + OpenIddict application
properties):

| `portalClass` | Examples | Auth policy | IAM admin API | Tenant switcher | Assurance (ADR 015) |
|---------------|----------|-------------|---------------|-----------------|---------------------|
| `operator` | `group-hq-admin`, `tech-console`, `his-hope-admin` | `HumanAdmin` + permissions | Yes | HQ: yes; vendor: internal only | High (MFA, step-up on write) |
| `customer_operator` | `customer-acme-portal` | Customer admin role bundle | Limited admin subset | **No** | High |
| `end_user` | `customer-acme-app`, patient portal | App-scoped permissions only | **No** | **No** | Moderate; step-up on sensitive actions |

**Token claims (additions)**

| Claim | Operator portal | End-user portal |
|-------|-----------------|-----------------|
| `portal_class` | `operator` | `end_user` \| `customer_operator` |
| `tenant_class` | `internal` | `customer` |
| `tenant_id` | Client-bound | Client-bound |
| `permissions` | IAM + app | App only (no `admin.*` unless customer_operator) |

**Route guards**

- `/api/v1/admin/**` — reject unless `portal_class` is `operator` or `customer_operator`.
- `customer_operator` — further restrict to an allowlist of routes (users, roles within tenant,
  consents); exclude IAM control plane (`/admin/iam/**`), break-glass approve, policy publish.
- `end_user` — BFF/API routes for application features only; no admin group mapping.

**UI**

- `admin-app` remains **operator-only** (`portalClass=operator`). Customer admin gets
  `customer-portal-app` (separate shell or route pack) without HQ switcher or `hqOnly` nav.
- End-user apps never load `TenantContextService` switcher.

### 5. Configuration layout

```
config/conglomerate/
  iam-scopes.v1.json              # organization + internal tenants (existing)
  customer-tenants.v1.json        # NEW: customer tenant definitions + portal clients
  customer-cross-tenant.v1.json   # NEW: vendor/HQ → customer pairs (optional split)
  oidc-clients.azure-staging.json # extended with portalClass + tenant binding
```

**Example `customer-tenants.v1.json`**

```json
{
  "version": "1",
  "customers": [
    {
      "key": "customer-acme",
      "displayName": "Acme Healthcare",
      "tenantClass": "customer",
      "operatorHome": "tech-vendor",
      "accountKey": "customer-acme-account",
      "environmentKey": "staging",
      "portalClients": [
        {
          "clientId": "customer-acme-portal",
          "portalClass": "customer_operator",
          "displayName": "Acme Admin Portal"
        },
        {
          "clientId": "customer-acme-app",
          "portalClass": "end_user",
          "displayName": "Acme Clinical App"
        }
      ]
    }
  ]
}
```

### 6. Resolver and registry behavior

**`IamTenantScopeResolver`**

| Actor | `tenantClass` filter | Scope result |
|-------|----------------------|--------------|
| HQ, no `scopeId` | internal only (default) | Unrestricted over **internal** tenants |
| HQ, `scopeId` = customer tenant | — | Subtree of that customer tenant |
| HQ + config `HqCustomerVisibility=all` | include customer | Unrestricted internal + list customer keys |
| Vendor, no `scopeId` | internal | Own tenant subtree only |
| Vendor, `scopeId` = customer with matching `operatorHome` | customer | Subtree if cross-tenant read pair allows |
| Customer user | customer | Own tenant subtree only |

**`IConglomerateTenantRegistry` extensions**

- `GetTenantClass(tenantKey) → internal | customer`
- `GetOperatorHome(tenantKey) → tenantKey?`
- `GetPortalClass(clientId) → operator | customer_operator | end_user`
- `GetCustomerTenantsForOperator(operatorTenantKey)`

Customer tenant seeding follows the same `IamScope` graph as ADR 016 (organization parent,
account, environment children) via `SeedConglomerateScopesAsync`.

### 7. Implementation phases

| Phase | Deliverable | Exit criteria |
|-------|-------------|---------------|
| **0 — ADR + config contract** | This ADR, example JSON, validator script | `Seed-ConglomerateTenant.ps1 -ValidateOnly` passes on example |
| **1 — Registry + claims** | `tenantClass`, `portalClass`, token claims | Token shows `portal_class`; client binding rejects wrong membership |
| **2 — Resolver** | HQ/vendor visibility rules | Unit tests: vendor cannot list unrelated customer |
| **3 — Cross-tenant policy** | Extended pairs + JIT gate on mutation | Integration test: vendor read OK; write without JIT → 403 |
| **4 — Portal split** | `customer-portal-app` or admin-app route pack | E2E: customer admin cannot hit `/admin/iam/scopes` |
| **5 — Pilot customer** | One `customer-acme` tenant in staging | Vendor support read audit trail verified |

## Consequences

### Positive

- B2B customers get the same isolation guarantees as internal tenants without duplicating
  identity planes.
- Vendor support is auditable, time-bound, and does not require shared credentials or standing
  customer membership.
- Operator and end-user surfaces are separated at OAuth client, token, API, and UI layers.
- ADR 016 patterns (client binding, endpoint filters, cross-tenant default deny) reuse cleanly.

### Negative / trade-offs

- `IamScope.Kind` alone is insufficient; operators must maintain `tenantClass` metadata in
  sync with config.
- HQ “see all customers” is **opt-in** via config to avoid accidental mass data exposure.
- JIT elevation adds request-context plumbing to mutation filters.
- A separate customer portal shell (or strict route pack) adds frontend maintenance.

### Out of scope (future ADRs)

- Patient-as-subject vs patient-as-tenant (B2B2C identity linking).
- Cross-customer federation and data sharing agreements.
- Per-customer database/schema isolation (tenant row-level isolation remains default).
- Dedicated tenant placement tier — see [ADR 018](./018-tenant-placement-tier.md) (opt-in, disabled by default).
- Billing/marketplace provisioning webhooks.

## Alternatives considered

| Alternative | Rejected because |
|-------------|------------------|
| New `Kind = "customer"` scope | Breaks hierarchy validator and workload-role tenant resolution |
| Vendor staff get `tenant_membership` on customer tenants | Standing cross-tenant access; weak audit; hard to revoke |
| Single portal for operators and end-users | IAM routes leak; switcher confusion; assurance mismatch |
| Customer tenants outside `IamScope` tree | Splits PEP and admin-app scope model; duplicates ADR 016 |

## Evidence

- Parent: [ADR 016 — Conglomerate Tenant Model](./016-conglomerate-tenant-model-azure.md)
- Assurance: [ADR 015 — NIST AAL Assurance Policy](./015-nist-aal-assurance-policy.md)
- Config (existing): `config/conglomerate/iam-scopes.v1.json`
- Config (proposed): `config/conglomerate/customer-tenants.v1.example.json`
- Code touchpoints: `ConglomerateTenantRegistry`, `IamTenantScopeResolver`,
  `OpenIddictHandlers.TryIssueTenantClaimsAsync`, `IamTenantAccessGuard`,
  `TenantAccessEvaluator`, `admin-app` `tenant-context.service.ts`, `hq-operator.guard.ts`
- Runbook (to add): `docs/runbooks/customer-tenant-pilot.md`

## References

- ADR 016 conglomerate model and cross-tenant default deny
- AWS Organizations / IAM account boundaries (analogy for tenant sovereignty)
- NIST SP 800-63B AAL separation for admin vs consumer journeys (ADR 015)
