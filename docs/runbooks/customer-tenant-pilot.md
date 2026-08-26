# Customer tenant pilot runbook

Pilot tenants: **customer-acme** (tech-vendor) and **customer-factory-x** (manufacturing) — ADR 017.

## Prerequisites

- Identity running with `ASPNETCORE_ENVIRONMENT=Azure.Staging` (or overlay with `Conglomerate:Enabled=true`)
- `Conglomerate:CustomerTenantsPath=config/conglomerate/customer-tenants.v1.json`
- Pilot password configured (`Conglomerate:PilotUserPassword` or Development fallback)
- **CommerceService** on port **5015** for buyer ordering pilot

## Validate config (Phase 0)

```powershell
./scripts/azure/Seed-ConglomerateTenant.ps1 -ValidateOnly
```

## Seed & smoke

1. Restart Identity service (picks up commerce permissions + `buyer.pilot`).
2. Start CommerceService: `dotnet run --project src/Services/CommerceService/CommerceService.Api`
3. Confirm IAM scopes include `customer-acme` and `customer-factory-x` under `abc-group`.

### Customer B2B portals (direct login)

3. **Acme**: `acme.pilot` via `customer-acme-portal` on port **4203**.
4. **Factory X admin**: `factory.pilot` via `customer-factory-x-portal` on port **4204**.

### Buyer ordering (end_user — Phase 0–3)

5. **Factory X buyer app**: `buyer.pilot` via `customer-factory-x-app` on port **4205** (`manufacturing-buyer-app`).
   - Public marketing: `/home`
   - Authenticated: catalog → cart → checkout → orders, profile, notifications
   - Commerce API: `GET/PUT /api/v1/commerce/*` (tenant-scoped, not admin IAM)

### Internal operator shells (cross-tenant support — direction B)

6. **Manufacturing console**: `manufacturing.pilot` via `manufacturing-app` on port **4200** (`internal-operator-app`).
   - Switch tenant to `customer-factory-x` → dashboard/users scoped via `?scopeId=`.
   - **Orders** route: commerce orders for selected tenant (`?tenantKey=`).
7. **Tech vendor console**: `tech.pilot` via `tech-console` on port **4201** (`internal-operator-app`).
   - Switch tenant to `customer-acme` → dashboard/users scoped via `?scopeId=`.
8. **Group HQ**: `hq.pilot` via `admin-app` port **4202** (full IAM, all tenants with `HqCustomerVisibility=all`).

Operator write into customer tenant requires JIT elevation (`POST /api/v1/admin/support-elevations` + header `X-Support-Elevation-Id`).

## Portal matrix

| App | Port | Client | portal_class | Tenants in switcher |
|-----|------|--------|--------------|---------------------|
| internal-operator-app (mfg) | 4200 | manufacturing-app | operator | manufacturing, customer-factory-x |
| internal-operator-app (tech) | 4201 | tech-console | operator | tech-vendor, customer-acme |
| admin-app | 4202 | group-hq-admin | operator | all (HQ) |
| customer-portal-app | 4203 | customer-acme-portal | customer_operator | none |
| customer-portal-app | 4204 | customer-factory-x-portal | customer_operator | none |
| manufacturing-buyer-app | 4205 | customer-factory-x-app | end_user | none |

## Run all pilots locally

```bash
# Commerce API
dotnet run --project src/Services/CommerceService/CommerceService.Api

# Manufacturing internal operator
cd internal-operator-app && npm start

# Tech internal operator (second terminal)
cd internal-operator-app && npm run start:tech

# HQ admin
cd admin-app && npm start

# Customer portals
cd customer-portal-app && npm start
cd customer-portal-app && npm run start:factory-x

# Buyer ordering (end_user)
cd manufacturing-buyer-app && npm start
```

Password: `ConglomeratePilot@Dev1`

## Commerce permissions (pilot)

| Permission | Buyer (`buyer.pilot`) | Manufacturing ops |
|------------|----------------------|-------------------|
| `commerce.catalog.view` | ✓ | — |
| `commerce.orders.create` | ✓ | — |
| `commerce.orders.view` | ✓ (own) | ✓ (tenant via switcher) |
| `commerce.orders.update` | — | ✓ (fulfill) |
| `commerce.profile.manage` | ✓ | — |
| `commerce.notifications.view` | ✓ | — |

## Cross-tenant pairs (staging)

- `group-hq → customer`: audit read
- `tech-vendor → customer` (`operatorHome` match): audit/users/identity read; write requires JIT
- `manufacturing → customer` (`operatorHome` match): audit/users/identity read; write requires JIT

## Evidence

- ADR: `docs/adr/017-customer-tenant-type.md`
- Config: `config/conglomerate/customer-tenants.v1.json`
- Internal operator app: `internal-operator-app/README.md`
- Commerce service: `src/Services/CommerceService/CommerceService.Api/`
- Buyer app: `manufacturing-buyer-app/`
