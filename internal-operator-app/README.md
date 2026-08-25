# Internal Operator App (ADR 017 — direction B)

Isolated operator shells for **internal legal entities**. Each instance binds one OIDC client (`portal_class=operator`) and can switch into contracted **customer tenants** via `operatorHome` cross-tenant policy.

## Run locally

**Manufacturing** (`manufacturing-app`, port 4200):

```bash
cd internal-operator-app
npm install
npm start
```

Sign in as `manufacturing.pilot` using the configured `CONGLOMERATE_PILOT_PASSWORD`. Tenant switcher: `manufacturing` + `customer-factory-x`.

**Tech vendor** (`tech-console`, port 4201):

```bash
npm run start:tech
```

Sign in as `tech.pilot`. Tenant switcher: `tech-vendor` + `customer-acme`.

## Isolation

| App                          | Port      | Client                          | Must NOT login                      |
| ---------------------------- | --------- | ------------------------------- | ----------------------------------- |
| internal-operator-app (mfg)  | 4200      | manufacturing-app               | group-hq-admin, customer portals    |
| internal-operator-app (tech) | 4201      | tech-console                    | manufacturing-app, customer portals |
| admin-app                    | 4202      | group-hq-admin / his-hope-admin | —                                   |
| customer-portal-app          | 4203/4204 | customer-\*-portal              | operator tokens                     |

Cross-tenant reads use `?scopeId=` injected by `tenantScopeInterceptor` when a customer tenant is selected in the switcher.

## API

`GET /api/v1/admin/me/switchable-tenants` — returns home tenant + customer tenants where `operatorHome` matches the operator membership.
