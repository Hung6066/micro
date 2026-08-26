# Customer Portal App (ADR 017)

Minimal Angular shell for **customer_operator** portals. One codebase; bind a different OIDC client per B2B customer tenant.

## Run locally

**Acme** (tech-vendor customer, port 4203):

```bash
cd customer-portal-app
npm install
npm start
```

Open http://localhost:4203 — sign in as `acme.pilot` / `ConglomeratePilot@Dev1` (`customer-acme-portal`).

**Factory X** (manufacturing customer, port 4204):

```bash
npm run start:factory-x
```

Open http://localhost:4204 — sign in as `factory.pilot` / `ConglomeratePilot@Dev1` (`customer-factory-x-portal`).

## Scope

- Dashboard + Users only (no IAM workbench, no tenant switcher)
- Requires token claim `portal_class=customer_operator`
- Operator admin-app rejects `customer_operator` / `end_user` tokens via `operatorPortalGuard`

## OIDC pilots

| Tenant | Client | Local port |
|--------|--------|------------|
| customer-acme | customer-acme-portal | 4203 |
| customer-factory-x | customer-factory-x-portal | 4204 |
