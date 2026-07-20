# Agent Intelligence Task 5 Report — Migration Gap Fix

## Concern Addressed

**Migration Gap (CRITICAL):** `AddEvalTables` migration (20260720090000) has timestamp preceding already-applied `AddMemoryEntryConfidenceScore` (20260720105641). On databases where the latter migration was applied but the former was not (e.g., after EnsureCreated with partial migrations), the eval tables would never be created by EF Core's `Migrate()`.

## Root Cause Analysis

The migration chain in `src/Infrastructure/AgentHarness/src/AgentHarness.Infrastructure/Persistence/Migrations/`:

| Migration | Timestamp | Status |
|-----------|-----------|--------|
| `InitialHarnessSchema` | 20260719060100 | Base tables |
| `AddParentPipelineRunId` | 20260719060800 | Column + index |
| `AddEvalTables` | **20260720090000** | Creates eval_suites and eval_runs |
| `AddMemoryEntryConfidenceScore` | **20260720105641** | Adds confidence_score column |

The `AddEvalTables` timestamp (20260720090000) precedes `AddMemoryEntryConfidenceScore` (20260720105641). On databases where history was populated by partial migration runs (e.g., `EnsureCreated` → later `Migrate` with subset), EF Core's `GetPendingMigrations()` would not include `AddEvalTables` if it wasn't in the history but later migrations were.

## Fix Applied

**Surgical additive corrective migration** — no existing files modified, no migrations deleted.

**File created:** `src/Infrastructure/AgentHarness/src/AgentHarness.Infrastructure/Persistence/Migrations/20260720210000_FixAddEvalTables.cs`

**Design:**
- Timestamp **20260720210000** (after all existing migrations) ensures EF Core always applies it
- Uses `CREATE TABLE IF NOT EXISTS` for both `harness.eval_suites` and `harness.eval_runs` — fully idempotent
- Uses `CREATE UNIQUE INDEX IF NOT EXISTS` / `CREATE INDEX IF NOT EXISTS` for all 3 indexes
- Uses conditional `DO $$ ... END $$` block for FK constraint (avoids "already exists" error)
- `Down()` uses `DROP TABLE IF EXISTS` for clean rollback

**Behavior by scenario:**

| Scenario | Old `AddEvalTables` | Corrective `FixAddEvalTables` | Result |
|----------|---------------------|-------------------------------|--------|
| Fresh DB, full Migrate() | Creates tables | No-op (tables exist) | ✅ Tables exist |
| Existing DB, all 4 applied | Already in history | No-op (tables exist) | ✅ Tables exist |
| Existing DB, eval tables missing | May be skipped | Creates tables | ✅ Tables created |
| EnsureCreated → partial Migrate | Not in history | Creates tables | ✅ Tables created |

## Verification

- **Build:** `dotnet build AgentHarness.Mcp.csproj` — **PASS** (0 errors)
- **Unit tests:** `dotnet test AgentHarness.UnitTests` — **PASS** (62/62 passed)
- **Integration tests:** `dotnet test AgentHarness.IntegrationTests` — **PASS** (39/39 passed)

## Git Changes

Only one new file (surgical, no modifications to existing files):
```
?? src/Infrastructure/AgentHarness/src/AgentHarness.Infrastructure/Persistence/Migrations/20260720210000_FixAddEvalTables.cs
```

## Batch 2 Fixes Applied (2026-07-20)

### Issue: Migration file was non-functional

The corrective migration `20260720210000_FixAddEvalTables.cs` was created as a standalone file but had two critical defects:

**Defect 1 — No EF Core discovery attributes**
EF Core discovers migrations via `[DbContext(typeof(HarnessDbContext))]` and `[Migration("...")]` attributes on the class. Normal migrations have a `.Designer.cs` partial file that carries these attributes. Since this corrective migration has no designer, the attributes were missing entirely, meaning EF Core would **never discover or apply** this migration.

**Fix:** Added `using His.Hope.AgentHarness.Infrastructure.Persistence;` and `using Microsoft.EntityFrameworkCore.Infrastructure;` imports, then applied `[DbContext(typeof(HarnessDbContext))]` and `[Migration("20260720210000_FixAddEvalTables")]` directly on the `FixAddEvalTables` partial class.

**Defect 2 — Unsafe Down() implementation**
The original `Down()` used `DROP TABLE IF EXISTS harness.eval_runs` / `DROP TABLE IF EXISTS harness.eval_suites`. On databases where the earlier `20260720090000_AddEvalTables` migration had **already** been applied, rolling back this corrective migration would **destroy the eval tables** that belong to the earlier migration, leaving the database in an inconsistent state.

**Fix:** Replaced with a **no-op Down()** with a detailed explanatory comment.

### Verification (Batch 2)

| Check | Result |
|-------|--------|
| `dotnet build AgentHarness.Infrastructure.csproj` | ✅ 0 errors, 0 warnings |
| `dotnet build AgentHarness.Mcp.csproj` | ✅ 0 errors, 3 pre-existing warnings |
| `dotnet test AgentHarness.UnitTests` | ✅ 62/62 passed (0 failed) |

## Remaining Concerns

1. **Migration history discrepancy:** The running database (`docker/harness` PostgreSQL) shows only 2 migrations in `__ef_migrations_history` (`AddEvalTables` + `AddMemoryEntryConfidenceScore`), missing `InitialHarnessSchema` and `AddParentPipelineRunId`. This suggests the DB was initialized with `EnsureCreated()` and only later migrations were added via `Migrate()`. This pattern is fragile but the corrective migration handles the eval tables specifically.

2. **Original concern severity:** After deeper analysis, EF Core's `Migrate()` does NOT skip migrations with earlier timestamps — it computes pending migrations as an unordered set difference (all migrations − applied migrations), then applies them in sorted order. So `AddEvalTables` WOULD be applied even if its timestamp precedes `AddMemoryEntryConfidenceScore`, as long as it's not in the history. The corrective migration adds defense-in-depth for edge cases where the history entry exists but tables were dropped.

3. **OpenTelemetry vulnerability** (pre-existing, unrelated): `OpenTelemetry.Exporter.OpenTelemetryProtocol` 1.12.0 has GHSA-4625-4j76-fww9.

4. **BuildServiceProvider warning** (pre-existing, unrelated): ASP0000 in Program.cs line 504.

5. **Migration designer pattern**: This corrective migration lacks a `.Designer.cs` partial. The attributes are applied directly on the class, which is valid but non-standard. If `dotnet ef migrations add` is ever run again, EF's code generator will overwrite the file if it detects the same migration ID. A manual note in the XML comment warns against auto-generation.
