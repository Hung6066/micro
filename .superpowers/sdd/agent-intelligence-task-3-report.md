# Task 3 Report: Phase 3 Instinct Optimizer + Auto-Record Hook

## Status
✅ Complete

## Commits Created
1. `52a54b0` - `feat(harness): add instinct optimizer and auto-record hook`

## Files Changed
| File | Status |
|------|--------|
| `src/AgentHarness.Core/Models/MemoryEntry.cs` | Modified |
| `src/AgentHarness.Application/DTOs/InstinctOptimizationResultDto.cs` | Created |
| `src/AgentHarness.Application/Services/InstinctOptimizer.cs` | Created |
| `src/AgentHarness.Mcp/Program.cs` | Modified |
| `src/AgentHarness.Infrastructure/Persistence/Configurations.cs` | Modified |
| `src/AgentHarness.Infrastructure/Persistence/Migrations/20260720105641_AddMemoryEntryConfidenceScore.cs` | Created |
| `src/AgentHarness.Infrastructure/Persistence/Migrations/20260720105641_AddMemoryEntryConfidenceScore.Designer.cs` | Created |
| `src/AgentHarness.Infrastructure/Persistence/Migrations/HarnessDbContextModelSnapshot.cs` | Modified |
| `tests/AgentHarness.UnitTests/Services/InstinctOptimizerTests.cs` | Created |
| `tests/AgentHarness.UnitTests/Services/LoopEngineerTests.cs` | Modified |

## Test Results
- **46 unit tests pass** (14 Task 3 tests + 32 existing tests)
- `InstinctOptimizerTests`: 10/10 pass
- `LoopEngineerTests`: 4/4 pass (including new auto-record test)
- Red phase confirmed: tests failed as expected before implementation (16 compilation errors)
- Green phase: all 46 tests pass after implementation

## Implementation Details

### MemoryEntry Confidence
- Added `ConfidenceScore` property (decimal, precision 4/scale 3, default 0.85)
- `BoostConfidence(decimal amount)` — additive increase, capped at 1.0
- `DecayConfidence(decimal factor)` — multiplicative decay, floored at 0.0
- `MergeFrom(MemoryEntry other)` — combines UseCount, keeps higher confidence, more recent LastUsedAt

### InstinctOptimizer
- `OptimizeAsync()` performs three phases:
  1. **Boost**: entries used within 7 days get +0.05 confidence
  2. **Decay**: entries unused for 7+ days get ×0.95 confidence
  3. **Merge**: deduplicates by `AgentName|ErrorPattern`, keeps survivor with combined stats
- Returns `InstinctOptimizationResultDto` with counts for each operation

### Auto-Record Hook
- LoopEngineer already called `_memory.StoreAsync(...)` on successful auto-fix (pre-existing)
- Added test verification (`LoopEngineerTests.AnalyzeAndFix_OnSuccessfulAutoFix_ShouldRecordInstinct`) confirming the hook operates correctly

### Migration
- `confidence_score` column added to `memory_entries` table (numeric(4,3), default 0.85)

## Concerns
- None. Code is surgical, TDD workflow was followed, tests cover all required behaviors.
- Learning must not auto-apply destructive changes: the Optimizer only modifies in-memory entries and saves them; no auto-deletion of entries.
- Existing record/query instincts behavior preserved unchanged.

---

## Post-Review Fixes (2026-07-20)

### Issues Fixed

| # | Finding | Fix |
|---|---------|-----|
| 1 | Duplicate merge did not persistently remove duplicates | Added `IStateStore.DeleteMemoryEntryAsync(Guid, CancellationToken)` + `StateStore` implementation. Optimizer now calls delete for merged duplicates. Added `RemovedCount` to result DTO. |
| 2 | Boost applied to all recent entries regardless of success | Boost phase now checks `entry.Success` in addition to recency and confidence cap. |
| 3 | Tests didn't catch the persistence gap | Added `Optimize_ShouldNotBoostUnsuccessfulRecentEntries`, `Optimize_ShouldDeleteMergedDuplicates`, `Optimize_ShouldPersistSurvivorAndRemoveDuplicates`. Updated `Optimize_ShouldMergeDuplicatePatterns` to verify `RemovedCount` + `DeleteMemoryEntryAsync` call. |
| 4 | Async-without-await noise | Removed `async` from 6 synchronous test methods (`BoostConfidence_*`, `DecayConfidence_*`, `MergeFrom_*`, `CreateEntry_*`). |

### Files Changed

| File | Change |
|------|--------|
| `src/AgentHarness.Core/Interfaces/IStateStore.cs` | Added `DeleteMemoryEntryAsync` |
| `src/AgentHarness.Infrastructure/Persistence/StateStore.cs` | Implemented `DeleteMemoryEntryAsync` |
| `src/AgentHarness.Application/DTOs/InstinctOptimizationResultDto.cs` | Added `RemovedCount` |
| `src/AgentHarness.Application/Services/InstinctOptimizer.cs` | Added `entry.Success` check in boost; `SaveEntriesAsync` now calls `DeleteMemoryEntryAsync` for duplicates; tracks `RemovedCount` |
| `tests/AgentHarness.UnitTests/Services/InstinctOptimizerTests.cs` | 3 new tests + 2 updated + 6 cleaned of async-without-await |

### Test Results

```
Passed! - Failed: 0, Passed: 49, Skipped: 0, Total: 49
```

- **New tests** (3):
  - `Optimize_ShouldNotBoostUnsuccessfulRecentEntries` — verifies `Success=false` entries are not boosted
  - `Optimize_ShouldDeleteMergedDuplicates` — verifies `DeleteMemoryEntryAsync` is called for merged duplicate
  - `Optimize_ShouldPersistSurvivorAndRemoveDuplicates` — verifies duplicate is unqueryable after merge
- **Updated tests** (2):
  - `Optimize_ShouldMergeDuplicatePatterns` — now asserts `RemovedCount=1` and `DeleteMemoryEntryAsync` called
  - `Optimize_WithNoEntries_ShouldReturnZeroCounts` — now asserts `RemovedCount=0`
- **Sync-converted** (6): removed `async` from pure-sync test methods

### Commit
`dd02527` — `fix(harness): merge persistence, success-gated boost, and test coverage`

## Report File
`D:\AI\micro\.superpowers\sdd\agent-intelligence-task-3-report.md`
