# Manufacturing Demo Seed Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Seed a complete, relationship-valid Manufacturing demo graph for local development without creating data in production.

**Architecture:** Add an Infrastructure-owned idempotent seeder invoked after EF migrations. Stable tenant-scoped business keys and deterministic UUIDs make reruns safe; one transaction preserves graph consistency. Startup configuration controls whether demo data is enabled.

**Tech Stack:** .NET 8, EF Core/PostgreSQL, xUnit integration tests, Docker Compose.

**Spec:** Approved in chat on 2026-08-26; runtime tenant `manufacturing` and local-only activation.

## Global Constraints

- Never run demo seed when `Manufacturing:SeedDemoData` is false.
- Use deterministic identifiers and upsert-by-business-key semantics.
- Keep all records tenant-scoped to `manufacturing`; no PHI or production secrets.
- Seed through Infrastructure and invoke only after migrations complete.
- Preserve existing Clean Architecture ports and API behavior.

---

### Task 1: Seeder and configuration

**Files:**
- Create: `src/Services/ManufacturingService/ManufacturingService.Infrastructure/Persistence/ManufacturingDemoSeeder.cs`
- Modify: `src/Services/ManufacturingService/ManufacturingService.Infrastructure/ManufacturingInfrastructureServiceCollectionExtensions.cs`
- Modify: `src/Services/ManufacturingService/ManufacturingService.Api/Program.cs`
- Modify: `docker/docker-compose.yml`

- [x] Add an idempotent seeder that inserts UOM, facilities, master data, procurement, inventory, production, quality, maintenance, planning, CAPA and audit records in FK order.
- [x] Invoke it only when `Manufacturing:SeedDemoData` is true.
- [x] Set the local operator compose environment variable to true without changing production defaults.

### Task 2: Graph verification

**Files:**
- Modify: `tests/Services/ManufacturingService/ManufacturingService.Integration.Tests/ManufacturingHttpContractTests.cs`

- [x] Add a test that runs the seeder against the test database and asserts every feature group has rows for `manufacturing`.
- [x] Assert foreign-key links, an approved recipe, an available lot, a reservation, a production batch with operation/loss review, an inbound receipt and a quality/CAPA record.

### Task 3: Runtime validation

- [x] Run API build and integration tests.
- [x] Rebuild/restart `manufacturing` container with local seed enabled.
- [x] Verify the manufacturing health endpoint and seeded database records.
- [x] Run `git diff --check` and report any warnings separately from failures.
