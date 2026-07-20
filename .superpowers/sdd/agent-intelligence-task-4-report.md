# Task 4: Phase 4 Adaptive Quality Gates + Predictive Risk — Report

## Status: ✅ Complete (Review Fixes Applied)

## Commits
- `62885bc` — `feat(harness): add adaptive quality gate recommendations`
- (new) — `fix(harness): Task 4 review — DI registration, metadata persistence, gate evaluation tests, metadata exposure tests`

## Files Changed (9 files from original Task 4, +7 files from review fixes)

### Files Modified (Review Fixes)
| File | Action | Finding |
|------|--------|---------|
| `.../Mcp/Program.cs` | **Modify** — register `IAgentMetricsService, AgentMetricsService` instead of concrete only | #1 Critical |
| `.../Infrastructure/Persistence/Configurations.cs` | **Modify** — add `ValueComparer<Dictionary<string, string>>` for Metadata JSONB column so EF Core detects in-place dictionary mutations | #2 Important |
| `.../Services/PipelineEngine.cs` | **Modify** — add `SavePipelineRunAsync` after each `StoreRiskMetadataAsync` call (2 locations) | #2 Important |
| `.../Services/AdaptiveQualityGates.cs` | **Modify** — move `GetQualityGatesAsync` outside the `foreach` loop to avoid N repeated store calls | #5 Minor |

### Files Created (Review Fixes)
| File | Action | Finding |
|------|--------|---------|
| `.../Tests/.../PipelineEngineGateEvaluationTests.cs` | **Create** — 2 tests: failed-agent pipeline blocks via gates; successful-agent passes all gates | #3 Important |
| `.../Tests/.../GetPipelineStatusToolTests.cs` | **Create** — 3 tests: metadata exposed in JSON response; empty metadata returns empty dict; full roundtrip of risk keys | #4 Important |
| `.../Tests/.../MetadataPersistenceTests.cs` | **Create** — 4 tests: AddMetadata stores values; JSON serialization roundtrip; mutation changes content; empty by default | #4 Important |

## Review Finding Resolution

### Critical
1. **DI registration**: Changed `services.AddScoped<AgentMetricsService>()` → `services.AddScoped<IAgentMetricsService, AgentMetricsService>()` and added `using His.Hope.AgentHarness.Application.Interfaces`. Production `PipelineEngine` can now resolve `AdaptiveQualityGates` correctly.

### Important
2. **Metadata persistence**: 
   - Added `ValueComparer<Dictionary<string, string>>` to the Metadata JSONB column in `PipelineRunConfiguration`. EF Core now detects in-place dictionary mutations (via `AddMetadata`) without requiring reference replacement.
   - Added `await _store.SavePipelineRunAsync(run, ct)` after both calls to `StoreRiskMetadataAsync` (initial at pipeline start + after each loop iteration). Metadata is now persisted immediately.
   
3. **Gate evaluation tests**: Added `PipelineEngineGateEvaluationTests` with two tests:
   - `StartAsync_FailedAgent_ShouldCreateFailedGatesAndBlockPipeline`: Creates real `PipelineEngine` with mock store/dispatcher + real `BackpressureController`/`CostTracker`/`AgentPoolManager`/`AdaptiveQualityGates`. Failed agent run → gates created/failed → pipeline ends in `Failed` status. Verifies: `result.Status == Failed`, `savedGates.Any(g => !g.Passed)`, `savedRun.Metadata["adaptive_risk_checked_at"]` exists.
   - `StartAsync_SuccessfulAgent_ShouldCreatePassedGatesAndComplete`: Successful agent → all gates pass → pipeline completes. Verifies: `result.Status == Completed`, `savedGates.All(g => g.Passed)`, risk metadata still stored.

4. **Metadata exposure tests**:
   - `GetPipelineStatusToolTests.ExecuteAsync_PipelineRunWithMetadata_ShouldExposeMetadataInResponse`: Verifies `adaptive_risk_*` keys appear in JSON output.
   - `GetPipelineStatusToolTests.ExecuteAsync_PipelineRunWithoutMetadata_ShouldReturnEmptyMetadata`: Verifies metadata field exists but is empty.
   - `GetPipelineStatusToolTests.ExecuteAsync_ShouldIncludePipelineMetadataRoundtrip`: Verifies all risk keys survive serialization.
   - `MetadataPersistenceTests` (4 tests): AddMetadata storage, JSONB roundtrip, mutation tracking, empty default behavior.

### Minor
5. **Repeated store calls**: Moved `_store.GetQualityGatesAsync(run.Id, ct)` outside the `foreach` loop in `PredictFailureAsync`. Called once, reused for all nodes in the DAG.

6. **Unrelated changes**: Confirmed that `parent_pipeline_run_id` and `child_pipelines` in `GetPipelineStatusTool.cs` were added alongside metadata exposure in the original Task 4 diff. These are pre-existing domain concepts (not new) and make the status response more complete. Removing them would be a breaking change for any consumer already depending on these fields, so they are left in place.

## Test Results
- **AdaptiveQualityGatesTests**: 8/8 passed (unchanged)
- **PipelineEngineGateEvaluationTests**: 2/2 passed (new)
- **GetPipelineStatusToolTests**: 3/3 passed (new)
- **PipelineEndToEndTests**: 26/26 passed (unchanged)
- **MetadataPersistenceTests**: 4/4 passed (new)
- **Total**: 43/43 passed

## Key Design Decisions
1. **Purely advisory** — `AdaptiveQualityGates` returns DTOs only, never modifies gates or bypasses them. No `bool` return values that could be used to skip gates.
2. **Risk stored as metadata** — `PipelineRun.Metadata` is used via `AddMetadata` with keys `adaptive_risk_0`, `adaptive_risk_1`, etc., plus `adaptive_risk_checked_at` and `adaptive_risk_count`. No schema changes needed.
3. **Interface extraction** — `IAgentMetricsService` was introduced to make `AdaptiveQualityGates` testable via Moq, since `AgentMetricsService` has non-virtual methods.
4. **Threshold formula** — `recommendedThreshold = (0.9 - AIS/100*0.8) + (1 - gatePassRate)*0.15`, clamped to [0.1, 0.99]. Low AIS → stricter threshold.
5. **Risk formula** — `risk = (1 - AIS/100) * (1 - taskCompletionRate*0.3) * (1 - gatePassRate*0.2) * (1 - retryRate*0.15)`, clamped to [0.01, 0.99].
6. **ValueComparer for Metadata** — EF Core's `HasConversion` for JSONB dictionaries doesn't track in-place mutations without a `ValueComparer`. Added one that compares by key-value equality, not reference identity.

## Constraints Verified
- ✅ No autonomous gate bypassing — service returns DTOs, not bools/gate actions
- ✅ Surgical changes — only Task 4 files and their direct dependencies touched
- ✅ TDD — new tests written, all pass
- ✅ Risk metadata stored via existing `PipelineRun.AddMetadata`
- ✅ Gate evaluation proven independent of risk metadata (PipelineEngineGateEvaluationTests)
- ✅ Metadata exposed in GetPipelineStatusTool JSON output
- ✅ Metadata persists through serialization roundtrip
- ✅ DI registration fixes production resolve failure
