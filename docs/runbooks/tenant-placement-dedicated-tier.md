# Dedicated tenant placement onboarding (ADR 018 Phase 3)

Onboard a **dedicated database tier** for one customer tenant when contract, compliance, data residency, or scale requires physical isolation. Default remains **shared DB + `tenant_key` row scope** (ADR 017).

## When to use

| Signal | Action |
|--------|--------|
| Enterprise contract requires isolated backup/restore boundary | Dedicated tier |
| Compliance audit requires separate PostgreSQL instance/database | Dedicated tier |
| Noisy-neighbor or large data volume on shared cluster | Dedicated tier (manual approval) |
| SMB pilot / standard customer | **Stay on shared** (`enabled: false`) |

## Prerequisites

- Contract/compliance ticket ID recorded in placement `reason`
- Tenant exists in IAM (`customer-tenants.v1.json` or internal tenant registry)
- `Seed-ConglomerateTenant.ps1 -ValidateOnly` passes
- PostgreSQL admin access (`psql`, `pg_dump`, `pg_restore`) or approved Docker postgres container
- `dotnet-ef` tool installed (`dotnet tool install --global dotnet-ef`)

## Artifacts

| Path | Purpose |
|------|---------|
| `config/conglomerate/tenant-placement.v1.json` | Active placement registry (`enabled: false` by default) |
| `config/conglomerate/tenant-placement.v1.example.json` | Documented dedicated example |
| `config/conglomerate/tenant-placement.connections.v1.example.json` | Example connection string map for ops scripts |
| `scripts/tenant-placement/` | Provisioning, backup, onboarding orchestration |

## Phase A — Design & approval

1. Confirm tenant key (e.g. `customer-enterprise-y`).
2. Choose services requiring dedicated storage (typically `manufacturing`, optionally `commerce` / `content`).
3. Name connection keys: `{ServiceDb}_{tenant_slug}` (example: `ManufacturingDb_customer_enterprise_y`).
4. Record approval in placement entry:

```json
{
  "tenantKey": "customer-enterprise-y",
  "tier": "dedicated",
  "dataRegion": "eu-west-1",
  "active": false,
  "reason": "enterprise-contract-2026-ticket-12345",
  "services": {
    "manufacturing": { "connectionName": "ManufacturingDb_customer_enterprise_y" },
    "commerce": { "connectionName": "CommerceDb_customer_enterprise_y" }
  }
}
```

Keep `"active": false` and global `"enabled": false` until infrastructure is ready.

## Phase B — Validate manifest

```powershell
./scripts/azure/Seed-ConglomerateTenant.ps1 -ValidateOnly

./scripts/tenant-placement/Get-TenantPlacementOpsManifest.ps1 `
  -TenantKey customer-enterprise-y `
  -PlacementFile config/conglomerate/tenant-placement.v1.example.json `
  -ConnectionStringsFile config/conglomerate/tenant-placement.connections.v1.example.json `
  -IncludeInactive
```

Or run the onboarding validator:

```powershell
./scripts/tenant-placement/Invoke-TenantPlacementOnboarding.ps1 `
  -TenantKey customer-enterprise-y `
  -PlacementFile config/conglomerate/tenant-placement.v1.example.json `
  -ConnectionStringsFile config/conglomerate/tenant-placement.connections.v1.example.json `
  -Phase Validate `
  -IncludeInactive
```

## Phase C — Provision databases & migrate schema

1. Copy `tenant-placement.connections.v1.example.json` to a **private** path (never commit secrets).
2. Create empty databases and apply EF migrations:

```powershell
./scripts/tenant-placement/Provision-TenantPlacementDatabases.ps1 `
  -TenantKey customer-enterprise-y `
  -PlacementFile config/conglomerate/tenant-placement.v1.example.json `
  -ConnectionStringsFile C:\secure\tenant-placement.connections.json `
  -IncludeInactive
```

What this does per service binding:

1. `CREATE DATABASE` (if missing) via `psql`
2. Sets the service's named `ConnectionStrings__*` value only for the child EF process so startup design-time DI can resolve the context
3. `dotnet ef database update` against the dedicated connection string, then restores the caller's environment

Dry-run provision:

```powershell
./scripts/tenant-placement/Provision-TenantPlacementDatabases.ps1 `
  -TenantKey customer-enterprise-y `
  -ConnectionStringsFile C:\secure\tenant-placement.connections.json `
  -WhatIf
```

## Phase D — Register runtime configuration

For each affected service, add connection strings (Key Vault / appsettings overlay / docker-compose secrets):

```json
"ConnectionStrings": {
  "ManufacturingDb_customer_enterprise_y": "<from secure store>"
}
```

Enable placement **only after** connections exist:

```json
// tenant-placement.v1.json
{ "enabled": true, "placements": [ { "...": "...", "active": true } ] }
```

Service appsettings:

```json
"TenantPlacement": {
  "Enabled": true,
  "ConfigPath": "config/conglomerate/tenant-placement.v1.json"
}
```

Restart services. Startup validation fails in **Production** if dedicated connections are missing.

Verify routing (Manufacturing):

- Dedicated tenant API calls persist to dedicated DB only
- Shared tenants remain on default connection
- `/health/ready` green on all affected APIs

## Phase E — Data cutover (if migrating from shared)

Dedicated tier is usually provisioned **before** go-live. If moving an existing shared tenant:

1. **Freeze writes** — maintenance window or read-only mode for tenant
2. **Export tenant rows** from shared DB (service-specific; Manufacturing uses `tenant_key` on all business tables)
3. **Import** into dedicated DB (same schema revision)
4. Enable placement routing (`enabled=true`, `active=true`)
5. Smoke test tenant portals + internal operator switcher
6. Keep shared DB rows until retention policy allows purge (legal hold)

Row-level export from shared DB is **not** automated in Phase 3 — use approved DBA/support tooling per service. Dedicated-from-day-one tenants skip this step.

## Phase F — Backup & restore

Dedicated DB backup = **full database backup** (one customer per database).

```powershell
./scripts/tenant-placement/Backup-TenantPlacementDatabases.ps1 `
  -TenantKey customer-enterprise-y `
  -ConnectionStringsFile C:\secure\tenant-placement.connections.json `
  -OutputDirectory artifacts/tenant-placement-backups/customer-enterprise-y
```

Each backup produces:

- `{ConnectionName}-{timestamp}.dump` (custom format)
- `{ConnectionName}-{timestamp}.dump.sha256.json` (SHA256 metadata)

Validate archive readability:

```powershell
./scripts/manufacturing/Test-BackupRestoreReadiness.ps1 `
  -BackupFile artifacts/tenant-placement-backups/customer-enterprise-y/ManufacturingDb_customer_enterprise_y-20260101T120000Z.dump `
  -ChecksumFile artifacts/tenant-placement-backups/customer-enterprise-y/ManufacturingDb_customer_enterprise_y-20260101T120000Z.dump.sha256.json
```

Full restore drill (isolated DB):

```powershell
./scripts/manufacturing/Invoke-RestoreDrill.ps1 `
  -BackupFile <path-to-dump> `
  -PostgresContainer his-hope-postgres-restore-drill
```

Schedule backups per **contract RPO/RTO** — dedicated tenants add one backup job per service connection.

## Phase G — Offboarding / tier demotion

1. Set placement `active: false` (routes back to shared default when `enabled=true`, or disable globally)
2. Take final backup via `Backup-TenantPlacementDatabases.ps1`
3. Remove connection strings from service config after retention period
4. Drop dedicated databases only after legal/compliance sign-off

## Orchestrated entry point

```powershell
# Validate
./scripts/tenant-placement/Invoke-TenantPlacementOnboarding.ps1 -TenantKey customer-enterprise-y -Phase Validate -ConnectionStringsFile ...

# Provision
./scripts/tenant-placement/Invoke-TenantPlacementOnboarding.ps1 -TenantKey customer-enterprise-y -Phase Provision -ConnectionStringsFile ...

# Backup
./scripts/tenant-placement/Invoke-TenantPlacementOnboarding.ps1 -TenantKey customer-enterprise-y -Phase Backup -ConnectionStringsFile ...
```

## Identity & API invariants (unchanged)

Dedicated placement does **not** change:

- JWT `tenant_id`, OAuth client binding, `portal_class`
- Cross-tenant default deny + JIT elevation for internal writes
- HTTP API contract (`?tenantKey=` / body `tenantKey`)

## References

- [ADR 018 — Tenant Placement Tier](../adr/018-tenant-placement-tier.md)
- [ADR 017 — Customer Tenant Type](../adr/017-customer-tenant-type.md)
- [Customer tenant pilot](./customer-tenant-pilot.md)
- `scripts/backup-postgres.ps1` — generic single-database backup
- `scripts/manufacturing/Invoke-RestoreDrill.ps1` — restore verification drill
