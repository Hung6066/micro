# Conglomerate Software-First — Four-Week Plan

Complete Identity/conglomerate **software** on local Docker **before** Azure infra deploy.

Azure Bicep and cloud deploy move to **Phase 2** ([azure-phase0-four-week.md](./azure-phase0-four-week.md)).

## Phase 1 success criteria (software complete)

- [ ] `Conglomerate:Enabled=true` seeds IAM scopes for `abc-group` + 3 tenants
- [ ] OAuth clients bound to tenants (`manufacturing-app`, `tech-console`, `group-hq-admin`)
- [ ] Human SSO tokens include `tenant_id` claim
- [ ] Cross-tenant default deny; group-hq `admin.audit.read` allowed cross-tenant
- [ ] `./scripts/azure/Test-ConglomerateSoftwareReadiness.ps1` passes
- [ ] Unit tests green for tenant claims + tenant PEP

---

## Week 1 — Config + seed (local)

| Day | Task | Verify |
|-----|------|--------|
| 1 | Review `config/conglomerate/*.json` | `./scripts/azure/Seed-ConglomerateTenant.ps1 -ValidateOnly` |
| 2 | Enable conglomerate in local env | `docker compose up identityservice` with `ASPNETCORE_ENVIRONMENT=Azure.Staging` or overlay |
| 3 | Run migrations + seed | IAM scopes exist in Postgres |
| 4 | Check admin `tenant_membership` claims | 3 tenants for bootstrap admin |
| 5 | OIDC discovery | `/.well-known/openid-configuration` |

---

## Week 2 — Token + authorize enforcement

| Day | Task | Verify |
|-----|------|--------|
| 1 | Login via `manufacturing-app` | Access token contains `tenant_id=manufacturing` |
| 2 | Login via `tech-console` | `tenant_id=tech-vendor` |
| 3 | User without membership | Token request rejected |
| 4 | Cross-tenant API call (tech → manufacturing) | `tenant_scope_denied` |
| 5 | group-hq audit read cross-tenant | Allowed for `admin.audit.read` only |

**Tests:**

```powershell
dotnet test tests/Services/IdentityService/IdentityService.Application.Tests `
  --filter "FullyQualifiedName~OpenIddictPopulateTokenClaims"
dotnet test tests/Shared/Authorization.Tests `
  --filter "FullyQualifiedName~cross_tenant"
```

---

## Week 3 — Pilot users + admin-app

| Day | Task | Verify |
|-----|------|--------|
| 1 | Set `CONGLOMERATE_PILOT_PASSWORD` and restart Identity | `manufacturing.pilot`, `tech.pilot` created |
| 2 | Login admin-app (`his-hope-admin` local / `group-hq-admin` staging) | Toolbar tenant selector shows 3 tenants |
| 3 | IAM scopes / groups / permission sets | Filtered by active tenant |
| 4 | Create pilot users via admin UI | `tenant_membership` claim set |
| 5 | Software readiness gate | `Test-ConglomerateSoftwareReadiness.ps1` |

---

## Week 4 — Handoff to infra (no Azure deploy yet)

| Day | Task | Notes |
|-----|------|-------|
| 1 | Document env matrix | Local vs Azure.Staging appsettings |
| 2 | Freeze software version / tag | Git tag or release branch |
| 3 | Review ADR 016 | Software items signed off |
| 4 | Prepare Azure params (secure path) | **Do not deploy** until software gate green |
| 5 | Start Phase 2 infra runbook | [azure-phase0-four-week.md](./azure-phase0-four-week.md) |

---

## Phase 2 — Azure infra (after software gate)

Only begin when Phase 1 checklist is complete:

1. `Deploy-Phase0.ps1` — Bicep
2. `Configure-AzureStagingSecrets.ps1`
3. Deploy Identity image with `ASPNETCORE_ENVIRONMENT=Azure.Staging`
4. CI ACR workflow

## Related

- [ADR 016](../adr/016-conglomerate-tenant-model-azure.md)
- [Seed-ConglomerateTenant.ps1](../../scripts/azure/Seed-ConglomerateTenant.ps1)
