# Manufacturing Operator Completeness Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Complete the five audited manufacturing operator feature groups and verify them end-to-end.

**Architecture:** Keep API handlers behind Application store ports and Infrastructure adapters. Add focused Angular feature components around the existing `ManufacturingApiService`, shared foundation UI, i18n, tokens, and theme. Add Playwright coverage against the running operator app.

**Tech Stack:** .NET 8 Minimal APIs, EF Core/PostgreSQL, Angular 21, RxJS, shared frontend foundation, Playwright.

**Spec:** `docs/superpowers/specs/2026-08-26-manufacturing-operator-completeness-design.md`

## Global Constraints

- Preserve unrelated working-tree changes.
- Do not access persistence directly from API routes or Angular features.
- Reuse existing shared foundation components, icons, tokens, theme, and i18n dictionaries.
- Validate each task with focused tests before moving to the next task.

### Task 1: Master Data feature

**Files:**
- Modify: `internal-operator-app/src/app/core/services/manufacturing-api.service.ts`
- Create or modify: `internal-operator-app/src/app/features/master-data/master-data-page.component.ts`
- Modify: `internal-operator-app/src/app/app.routes.ts`
- Modify: `internal-operator-app/src/app/app.component.ts`
- Modify: `shared/frontend-foundation/i18n/src/dictionaries/en.ts`
- Modify: `shared/frontend-foundation/i18n/src/dictionaries/vi-vn.ts`

**Interfaces:** consume `GET/POST /products`, `/uoms`, `/uom-conversions`; produce a `/master-data` route with list/create flows.

- [ ] Add missing typed API methods for products, UOM conversions, and conversion creation if absent.
- [ ] Create the feature component with tabs/sections for products, UOMs, conversions, and human-readable UOM labels.
- [ ] Add route/menu entry and localized labels.
- [ ] Build the Angular app and verify validation/error/empty states.

### Task 2: Traceability feature

**Files:**
- Modify: `internal-operator-app/src/app/core/services/manufacturing-api.service.ts`
- Create or modify: `internal-operator-app/src/app/features/traceability/traceability-page.component.ts`
- Modify: `internal-operator-app/src/app/app.routes.ts`
- Modify: `internal-operator-app/src/app/app.component.ts`
- Modify: shared i18n dictionaries

**Interfaces:** consume `/lots/{id}/genealogy`, `/lots/{id}/inventory-transactions`, `/products/{sku}/fefo`, `/lots/{id}/reservations`, `/reservations/{id}/release`, `/events/receipts`.

- [ ] Add typed methods and request models for the traceability endpoints.
- [ ] Implement lot search, FEFO results, reservation release, transactions, receipts, and upstream/downstream genealogy.
- [ ] Render foreign keys as names where lookup data is available.
- [ ] Build and run focused API contract tests for the covered endpoints.

### Task 3: Procurement completion

**Files:**
- Modify: `internal-operator-app/src/app/core/services/manufacturing-api.service.ts`
- Modify: `internal-operator-app/src/app/features/procurement/procurement-page.component.ts`
- Modify: `internal-operator-app/src/app/features/rfqs/rfqs-page.component.ts`
- Modify: shared i18n dictionaries

**Interfaces:** consume quotation list and batch receipt endpoints; preserve existing supplier/PO flows.

- [ ] Add quotation history/list loading by RFQ and a comparison table with supplier names.
- [ ] Add batch receipt form with line-level validation and receipt history refresh.
- [ ] Ensure supplier, facility, material, and PO foreign keys use display labels.
- [ ] Build and run procurement integration tests.

### Task 4: Production analytics and loss review

**Files:**
- Modify: `internal-operator-app/src/app/core/services/manufacturing-api.service.ts`
- Modify: `internal-operator-app/src/app/features/dashboard/dashboard-page.component.ts`
- Modify: `internal-operator-app/src/app/features/production/production-page.component.ts`
- Modify: shared i18n dictionaries

**Interfaces:** consume `/dashboard/cost-projection` and `/production-batches/{batchId}/operations/{operationId}/loss-review`.

- [ ] Add typed API methods for cost projection and loss review.
- [ ] Add cost projection inputs/results to dashboard and operation loss review to production.
- [ ] Validate non-negative quantities and display calculated loss/yield clearly.
- [ ] Add/extend backend contract tests where behavior is not already covered.

### Task 5: Operator E2E

**Files:**
- Create: `tests/e2e/manufacturing-operator-completeness-ui-tests.mjs`
- Create: `tests/e2e/manufacturing-operator-completeness.playwright.config.mjs`
- Modify: `tests/e2e/config/urls.js` only if a shared URL is missing

**Interfaces:** use the configured OIDC test session and operator URL; exercise the five feature groups.

- [ ] Add authenticated navigation assertions for dashboard, master data, traceability, procurement, and production.
- [ ] Add one read/write assertion per newly completed group using stable accessible labels.
- [ ] Run Playwright with one worker and record any environment-blocked auth prerequisite explicitly.

### Task 6: Verification

- [ ] Run `dotnet build src/Services/ManufacturingService/ManufacturingService.Api/ManufacturingService.Api.csproj --no-restore`.
- [ ] Run Application tests and Manufacturing integration tests.
- [ ] Run `npm run build -- --configuration manufacturing` in `internal-operator-app`.
- [ ] Run the focused Playwright config with `--workers=1`.
- [ ] Run `git diff --check` and inspect the final diff against the five acceptance criteria.
