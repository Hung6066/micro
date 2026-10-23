# Azure Phase 0 — Four-Week Execution Runbook

> **Prerequisite:** Complete [conglomerate-software-first-four-week.md](./conglomerate-software-first-four-week.md) first.  
> Deploy Azure infra only after `./scripts/azure/Test-ConglomerateSoftwareReadiness.ps1` passes.

Conglomerate Identity on Azure (manufacturing + tech vendor + group HQ).  Region default: **Southeast Asia**. Subscription: dedicated non-production staging.

## Success criteria (end of week 4)

- [ ] Phase 0 Bicep deployed; `artifacts/azure/phase0-deployment.json` present
- [ ] Key Vault populated; Identity connects to PostgreSQL + Redis
- [ ] IAM scopes seeded for `abc-group` / three tenants
- [ ] Pilot SSO for `manufacturing-app` and `tech-console`
- [ ] Identity image in ACR; CI workflow green on `develop`
- [ ] Backup container exists; restore drill documented
- [ ] Cross-tenant default deny verified (tech cannot access manufacturing prod APIs)

---

## Week 1 — Foundation (network + data plane)

| Day | Task | Command / artifact |
|-----|------|-------------------|
| 1 | Create resource group, choose region | `az group create -n rg-hishop-azure-staging -l southeastasia` |
| 1 | Copy params file outside repo | `main.bicepparam.example` → secure path |
| 2 | Deploy Bicep | `./scripts/azure/Deploy-Phase0.ps1 -SubscriptionId ... -ResourceGroup rg-hishop-azure-staging -ParametersFile <secure>/azure-phase0.bicepparam` |
| 3 | Store secrets in Key Vault | `./scripts/azure/Configure-AzureStagingSecrets.ps1 -DeploymentArtifact artifacts/azure/phase0-deployment.json -PostgresPassword ... -Apply` |
| 4 | Readiness smoke | `./scripts/azure/Test-AzurePhase0Readiness.ps1` |
| 5 | DNS + TLS plan | CNAME `identity.staging.abc-group.example` → ingress (Container Apps / VM / AKS later) |

**Verify:** `phase0-readiness.json` shows all checks passed.

---

## Week 2 — Identity on Azure staging

| Day | Task | Notes |
|-----|------|-------|
| 1 | Fill `azure-staging.env` from template | `config/environments/azure-staging.env.example` |
| 2 | Build Identity image locally | `docker compose -f docker/docker-compose.yml build identityservice` |
| 3 | Run migrations + seed | `ASPNETCORE_ENVIRONMENT=Azure.Staging`, `Persistence:RunMigrationsOnStartup=true` |
| 4 | Wire App Insights | Connection string from deployment artifact |
| 5 | Health check | `/.well-known/openid-configuration` on staging authority |

**Verify:** OpenIddict issuer matches `OpenIddict:Issuer` in `appsettings.Azure.Staging.json`.

---

## Week 3 — Conglomerate IAM + pilot users

| Day | Task | Command |
|-----|------|---------|
| 1 | Validate conglomerate config | `./scripts/azure/Seed-ConglomerateTenant.ps1 -ValidateOnly` |
| 2 | Restart Identity (seed scopes) | `Conglomerate:Enabled=true` in appsettings |
| 3 | Create 5–10 manufacturing pilot users | Admin UI or SCIM dry-run |
| 4 | Register redirect URIs for pilots | Already in `oidc-clients.azure-staging.json` |
| 5 | Backup drill | Export PostgreSQL to `identity-backups` container; record evidence JSON |

**Verify:** IAM scopes `abc-group`, `manufacturing`, `tech-vendor`, `group-hq` exist in database.

---

## Week 4 — CI, hardening, handoff

| Day | Task | Command |
|-----|------|---------|
| 1 | Enable ACR push workflow | `.github/workflows/azure-identity-acr.yml` + `AZURE_CREDENTIALS` secret |
| 2 | Deploy from ACR to staging host | Pull `identity-service:staging` tag |
| 3 | Cross-tenant test | Tech user token must not authorize manufacturing tenant resources |
| 4 | Run enterprise validator (local DR allowed) | `./scripts/validate-enterprise-production-phases.ps1 -AllowLocalDrEvidence` |
| 5 | Gate checklist + ADR sign-off | ADR 016, update remediation plan |

**Verify:** GitHub Actions `azure-identity-acr` succeeds; pilot login completes authorization code + PKCE flow.

---

## Rollback

1. Stop Identity workload (Container App / VM / pod).
2. Restore PostgreSQL from backup blob or point-in-time restore (7-day retention).
3. Re-deploy previous ACR image tag.
4. Document incident in `artifacts/evidence/`.

## Related

- [ADR 016](../adr/016-conglomerate-tenant-model-azure.md)
- [infra/azure/phase0/README.md](../../infra/azure/phase0/README.md)
- [legacy auth deprecation](./legacy-auth-deprecation.md)
