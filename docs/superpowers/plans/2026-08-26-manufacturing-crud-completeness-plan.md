# Manufacturing CRUD Completeness Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Close the five audited CRUD gaps in the manufacturing operator application without hard-deleting transactional data.

**Architecture:** Add typed lifecycle/update commands to Application ports and implement them in Infrastructure stores. Expose guarded API routes, then bind Angular controls to existing shared-foundation pages and add authenticated E2E coverage.

**Tech Stack:** .NET 8 Minimal API, EF Core/PostgreSQL, Angular 21, RxJS, Playwright.

**Spec:** The approved CRUD design in the conversation: master-data lifecycle, governed entity versioning, production/inventory lifecycle, procurement lifecycle, and machine/traceability lifecycle with E2E.

## Global Constraints

- Never hard-delete records that participate in lots, reservations, production, receipts, or audit history.
- All API handlers depend on Application ports, never concrete EF stores.
- Preserve tenant checks and actor fields on lifecycle commands.
- Reuse shared foundation UI, i18n, tokens, fonts, and icons.

### Task 1: Master-data update/deactivate

Add typed update/deactivate contracts and port methods for products, materials, UOMs, conversions, facilities, warehouses, and storage locations. Add PATCH routes and operator controls with active/inactive filtering.

### Task 2: Governed entity edit/archive

Add version replacement or archive commands for recipes, product specifications, deviations, CAPA, and quality inspections. Preserve existing lifecycle transitions and audit actor/reason fields.

### Task 3: Production/inventory lifecycle

Add cancel/archive transitions for production orders and batches, reservation listing/release, and lot/inspection correction workflows with conflict validation.

### Task 4: Procurement and machine lifecycle

Add RFQ/quotation edit/close/select-winner, PO cancel/archive, inbound correction, machine deactivate/update, and traceability lifecycle UI.

### Task 5: Verification and E2E

Add contract tests for each lifecycle command, authenticated operator Playwright coverage, build the shared foundation/operator images, restart Docker, and smoke-test the protected routes.
