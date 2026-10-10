# Manufacturing Operator Completeness Design

## Goal

Complete the five audited gaps in the manufacturing operator experience while preserving the existing Clean Architecture boundaries, shared frontend foundation, and API contracts.

## Scope

1. Master data UI for products, UOMs, and UOM conversions.
2. Traceability UI for FEFO, reservations, release, inventory transactions, event receipts, and genealogy.
3. Procurement completion for quotation comparison/history and batch inbound receipts.
4. Production analytics for cost projection and operation loss review.
5. Operator Playwright E2E coverage for authenticated dashboard and core workflows.

## Architecture

Existing API routes continue to depend on Application ports (`IManufacturing*Store`). New backend behavior, if required, is expressed through Application ports and implemented by Infrastructure adapters. Angular features call `ManufacturingApiService` and use shared foundation components, tokens, icons, i18n, and theme selectors. No feature accesses persistence directly.

## Acceptance Criteria

- Every in-scope backend capability has an operator route and usable UI state, including loading, empty, validation, and API error states.
- Foreign-key fields display human-readable names while retaining stable IDs in requests.
- New UI text uses the shared i18n dictionaries; styling uses shared foundation tokens/components.
- Backend and frontend builds pass.
- Manufacturing Application and integration tests pass.
- Operator E2E covers authenticated navigation and at least one successful read/write flow for each newly completed group.

## Explicit Non-goals

- Replacing the existing API transport or authentication system.
- Redesigning unrelated buyer/admin pages.
- Moving every existing endpoint out of `Program.cs` in this iteration; that remains a follow-up Clean Architecture refactor.
