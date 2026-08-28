# ADR 018: Tenant Placement Tier (Shared Default, Dedicated Opt-In)

## Status

Accepted — 2026-08-27

## Context

[ADR 016](./016-conglomerate-tenant-model-azure.md) and [ADR 017](./017-customer-tenant-type.md) establish:

- **Internal tenants** and **customer tenants** share the same logical isolation model (`tenant_key` row scope + identity sovereignty).
- Customer tenants remain **sovereign** via OAuth client binding, `tenantClass`, cross-tenant default deny, and JIT elevation for internal mutations.
- Physical database isolation per customer is explicitly **out of scope** in ADR 017.

Operations now need a **controlled escape hatch**: enterprise contracts, compliance, data residency, or noisy-neighbor scale may require a **dedicated database** for one customer without forking the codebase or duplicating the domain model.

## Decision

### 1. Default: shared database (unchanged)

| Setting | Default | Meaning |
|---------|---------|---------|
| `TenantPlacement:Enabled` | `false` | All tenants use the service default connection |
| `defaultTier` | `shared` | Row isolation via `tenant_key` only |
| `placements` | `[]` | No dedicated overrides |

When `Enabled=false`, the placement registry is loaded for **metadata and validation only**; services **must not** route connections to alternate databases.

### 2. Opt-in: dedicated placement tier

When `Enabled=true` **and** a tenant entry has `tier=dedicated` + `active=true`, the service resolves that tenant's connection via `services.{serviceName}.connectionName` instead of the default.

```json
{
  "version": "1",
  "enabled": false,
  "defaultTier": "shared",
  "services": {
    "manufacturing": { "defaultConnectionName": "ManufacturingDb" }
  },
  "placements": [
    {
      "tenantKey": "customer-enterprise-y",
      "tier": "dedicated",
      "dataRegion": "eu-west-1",
      "active": true,
      "reason": "enterprise-contract-2026",
      "services": {
        "manufacturing": { "connectionName": "ManufacturingDb_customer_enterprise_y" }
      }
    }
  ]
}
```

**Activation gates (all required for dedicated routing):**

1. Global `enabled=true` in placement config.
2. Per-tenant `tier=dedicated` and `active=true`.
3. Target service registers placement options and implements tenant-aware connection resolution (Manufacturing: phase 2 factory hook).
4. Connection string exists in service configuration (`ConnectionStrings:{connectionName}`).
5. Runbook approval (contract/compliance/scale ticket referenced in `reason`).

### 3. Identity sovereignty is unchanged

Dedicated placement **does not** change:

- JWT `tenant_id`, client binding, or `portal_class` rules (ADR 017).
- Cross-tenant policy matrix or JIT elevation for internal writes.
- API contract (`?tenantKey=` / body `tenantKey`).

Physical isolation is an **infrastructure overlay** on top of the existing logical tenant model.

### 4. Configuration layout

```
config/conglomerate/
  tenant-placement.v1.json          # active config (enabled: false by default)
  tenant-placement.v1.example.json  # documented dedicated example (inactive)
```

Service appsettings:

```json
"TenantPlacement": {
  "Enabled": false,
  "ConfigPath": "config/conglomerate/tenant-placement.v1.json"
}
```

### 5. Shared library

`His.Hope.AspNetCore.Tenancy`:

- `TenantPlacementRegistry` — load + query placements
- `TenantPlacementConnectionResolver` — map `(serviceName, tenantKey)` → connection string name
- `AddHisHopeTenantPlacement()` — DI registration
- Startup validation: warn when dedicated placements exist but global flag is off; fail fast when enabled but connection string missing (production only)

### 6. Implementation phases

| Phase | Deliverable | Exit criteria |
|-------|-------------|---------------|
| **0 — ADR + config** | This ADR, JSON schema, validator script | `Seed-ConglomerateTenant.ps1 -ValidateOnly` includes placement file |
| **1 — Registry (this PR)** | Shared registry + Manufacturing startup validation | Unit tests; no runtime routing change when disabled |
| **2 — Connection routing** | Tenant-aware `IDbContextFactory` for Manufacturing | Integration test: dedicated tenant hits alternate DB when enabled — **done** |
| **3 — Ops** | Per-tenant backup/export, provisioning pipeline | Runbook + `scripts/tenant-placement/*` — **done** |

## Consequences

### Positive

- Default path stays simple: one DB, one migration pipeline, one deploy.
- Enterprise/compliance path is explicit, config-gated, and auditable (`reason`, `contractId`).
- Same API and domain code for shared and dedicated tiers.

### Negative / trade-offs

- Dedicated tier doubles migration/backup operational surface per customer.
- Cross-tenant operator views spanning shared + dedicated tenants need federation at query layer (future).
- Misconfiguration risk if `enabled=true` without connection strings — mitigated by startup validation.

### Out of scope

- Automatic tier promotion based on load (manual contract-driven only).
- Cross-region active-active for a single tenant.
- Customer-managed keys (BYOK) — future ADR.

## Alternatives considered

| Alternative | Rejected because |
|-------------|----------------|
| Dedicated DB by default for all customers | Ops cost; slows pilot; ADR 017 shared model is sufficient for SMB |
| Schema-per-tenant in same DB | CockroachDB/Postgres migration complexity; weaker isolation story than dedicated DB |
| Silent auto-routing from `dataRegion` alone | Hides infra changes; violates explicit opt-in requirement |

## References

- [ADR 017 — Customer Tenant Type](./017-customer-tenant-type.md)
- [ADR 016 — Conglomerate Tenant Model](./016-conglomerate-tenant-model-azure.md)
- `config/conglomerate/tenant-placement.v1.json`
- `His.Hope.AspNetCore.Tenancy.TenantPlacementRegistry`
