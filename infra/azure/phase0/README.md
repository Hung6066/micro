# Azure Phase 0 — Identity Platform Foundation

Minimal Azure footprint for conglomerate Identity staging (2 companies + group HQ).

## Resources

| Resource | SKU | Purpose |
|----------|-----|---------|
| Virtual Network | — | Network boundary for later private endpoints |
| PostgreSQL Flexible Server | B1ms | Identity database |
| Azure Cache for Redis | Basic C0 | Sessions, token cache |
| Key Vault | Standard | Secrets, JWT keys (RBAC) |
| Container Registry | Basic | Identity service images |
| Storage Account | Standard LRS | Backup / DR evidence blobs |
| Log Analytics + App Insights | Pay-as-you-go | Observability |

## Prerequisites

- Azure subscription with Contributor on target resource group
- [Azure CLI](https://learn.microsoft.com/cli/azure/install-azure-cli) 2.55+
- PowerShell 7+

## Deploy

```powershell
./scripts/azure/Deploy-Phase0.ps1 `
  -SubscriptionId '<subscription-id>' `
  -ResourceGroup 'rg-hishop-azure-staging' `
  -Location southeastasia `
  -ParametersFile infra/azure/phase0/main.bicepparam
```

Copy `main.bicepparam.example` to a local path **outside the repo** (e.g. `D:\secure\his-hope\azure-phase0.bicepparam`) and pass that file. Never commit passwords.

## Post-deploy

1. `./scripts/azure/Configure-AzureStagingSecrets.ps1` — store connection strings in Key Vault
2. `./scripts/azure/Test-AzurePhase0Readiness.ps1` — smoke checks
3. Follow [azure-phase0-four-week.md](../../docs/runbooks/azure-phase0-four-week.md)

## Region

Default: **Southeast Asia** (`southeastasia`). Use **Azure Vietnam East** when generally available in your subscription.
