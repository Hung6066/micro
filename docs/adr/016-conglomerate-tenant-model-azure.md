# ADR 016: Conglomerate Tenant Model on Azure Phase 0

## Status

Accepted — 2026-08-23

## Context

The platform must support a **conglomerate** with multiple legal entities (manufacturing,
technology vendor, group HQ) under one global identity plane, with tenant sovereignty and
future international expansion. A cloud-first Phase 0 on **Azure** is required to deploy
Identity staging without operating full on-prem Kubernetes first.

## Decision

1. **Tenant hierarchy:** `organization` → `tenant` → `account` → `environment` via IAM scopes
   (`config/conglomerate/iam-scopes.v1.json`).
2. **OAuth clients** are bound to a single tenant (`manufacturing-app`, `tech-console`, `group-hq-admin`).
3. **Cross-tenant access** defaults to deny; group HQ receives explicit audit-read pairs only.
4. **Azure Phase 0** provisions: VNet, PostgreSQL Flexible (B1ms), Redis Basic, Key Vault,
   ACR, backup storage, Log Analytics + Application Insights (`infra/azure/phase0/main.bicep`).
5. **Identity staging** uses `ASPNETCORE_ENVIRONMENT=Azure.Staging` and
   `appsettings.Azure.Staging.json` with `Conglomerate:Enabled=true`.
6. **Secrets** live in Azure Key Vault; connection strings never committed to the repo.

## Consequences

- Demo hospital IAM graph is skipped on Azure staging (`Conglomerate:SkipDemoHospitalScope`).
- **Software-first:** tenant binding, `tenant_id` claims, and cross-tenant PEP ship before Azure deploy.
- Additional OIDC clients from configuration are seeded as public PKCE applications.
- Manufacturing and tech-vendor pilots can SSO without sharing tenant scope by default.
- Production cutover still requires DNS/TLS, external pentest, and live DR/SIEM evidence.

## Evidence

- Bicep: `infra/azure/phase0/main.bicep`
- Deploy: `scripts/azure/Deploy-Phase0.ps1`
- Runbook (software): `docs/runbooks/conglomerate-software-first-four-week.md`
- Runbook (infra): `docs/runbooks/azure-phase0-four-week.md`
- Config: `config/conglomerate/`, `config/environments/azure-staging.env.example`
