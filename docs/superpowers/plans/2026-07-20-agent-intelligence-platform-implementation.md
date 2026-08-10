# Agent Intelligence Platform Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add agent intelligence metrics, eval benchmarking, instinct learning, and adaptive quality gates to the existing .NET Agent Harness without replacing the current pipeline engine.

**Architecture:** Keep the work inside the existing harness boundaries. Phase 1 reads from current persisted runs and emits AIS/profile data. Phase 2 adds durable eval definitions and run history. Phase 3 extends the memory/instinct loop with confidence boost/decay/merge behavior. Phase 4 adds advisory adaptive gate logic and pre-dispatch risk prediction, but it must never bypass existing safety gates.

**Tech Stack:** .NET 8, ASP.NET Core MCP server, EF Core, CockroachDB, Prometheus/OpenTelemetry, xUnit, Testcontainers, Docker Compose.

## Global Constraints
- No new external service for analytics.
- No replacement of the current pipeline engine.
- No autonomous gate bypassing.
- No production model training system.
- No UI redesign.
- Metrics must be derived from existing persisted records; do not invent new runtime telemetry paths when the data already exists.
- Eval results must be reproducible from stored suite definitions.
- Learning must not auto-apply destructive changes.
- Adaptive gates may recommend thresholds, but human review remains required for critical paths.
- Security-sensitive tasks must continue to use redaction and existing guardrails.

---

### Task 1: Phase 1 metrics + agent profile

**Files:**
- Create: `src/Infrastructure/AgentHarness/src/AgentHarness.Application/DTOs/AgentProfileDto.cs`
- Create: `src/Infrastructure/AgentHarness/src/AgentHarness.Application/DTOs/AgentRunSummaryDto.cs`
- Create: `src/Infrastructure/AgentHarness/src/AgentHarness.Application/Services/AgentMetricsService.cs`
- Modify: `src/Infrastructure/AgentHarness/src/AgentHarness.Infrastructure/Observability/HarnessMetrics.cs`
- Modify: `src/Infrastructure/AgentHarness/src/AgentHarness.Mcp/Program.cs`
- Create: `src/Infrastructure/AgentHarness/src/AgentHarness.Mcp/Tools/GetAgentProfileTool.cs`
- Create: `src/Infrastructure/AgentHarness/tests/AgentHarness.UnitTests/Services/AgentMetricsServiceTests.cs`
- Create: `src/Infrastructure/AgentHarness/tests/AgentHarness.UnitTests/Tools/GetAgentProfileToolTests.cs`

**Interfaces:**
- Consumes: `IStateStore.GetAgentRunsAsync`, `IStateStore.GetQualityGatesAsync`, `IStateStore.GetMemoryEntriesAsync`.
- Produces: `AgentMetricsService.GetAgentProfileAsync(string agentName, CancellationToken ct = default)` returning `AgentProfileDto`, plus MCP tool `get-agent-profile`.

- [ ] **Step 1: Write the failing AIS/profile tests**

Write `AgentMetricsServiceTests` so they assert:
- a profile for `dotnet` returns a score between 0 and 100,
- total runs, successful runs, retry rate, gate pass rate, and confidence accuracy are calculated from fake runs,
- the profile includes a bounded history list ordered newest-first.

Write `GetAgentProfileToolTests` so they assert the tool returns the same JSON shape as `AgentProfileDto`.

- [ ] **Step 2: Run the unit tests to confirm they fail for missing service/tool**

Run: `dotnet test src/Infrastructure/AgentHarness/tests/AgentHarness.UnitTests/His.Hope.AgentHarness.UnitTests.csproj --filter "AgentMetricsServiceTests|GetAgentProfileToolTests" -v normal`

Expected: fail because `AgentMetricsService` and `GetAgentProfileTool` do not exist yet.

- [ ] **Step 3: Implement the service, DTO, metric exports, and MCP tool**

Implement `AgentProfileDto` with these properties:
- `AgentName`
- `AisScore`
- `TaskCompletionRate`
- `QualityGatePassRate`
- `RetryRate`
- `ConfidenceAccuracy`
- `LearningEffectiveness`
- `AverageJudgeScore`
- `TotalRuns`
- `SuccessfulRuns`
- `RecentRuns` (`IReadOnlyList<AgentRunSummaryDto>`)

Implement `AgentRunSummaryDto` with these properties:
- `AgentRunId`
- `PipelineRunId`
- `Status`
- `ConfidenceScore`
- `StartedAt`
- `CompletedAt`
- `DurationSeconds`
- `ArtifactRef`

Implement `AgentMetricsService.GetAgentProfileAsync` so it:
- reads persisted runs for the agent,
- reads matching quality gates,
- computes a weighted AIS score,
- returns newest-first history entries,
- emits Prometheus metrics from `HarnessMetrics`.

Register the service and `get-agent-profile` in `Program.cs`.

- [ ] **Step 4: Run the unit tests and the MCP smoke check**

Run: `dotnet test src/Infrastructure/AgentHarness/tests/AgentHarness.UnitTests/His.Hope.AgentHarness.UnitTests.csproj --filter "AgentMetricsServiceTests|GetAgentProfileToolTests" -v normal`

Expected: pass.

Run: `curl -s -X POST http://localhost:5200/mcp -H "Content-Type: application/json" -H "X-API-Key: dev-key-change-in-production" -d '{"jsonrpc":"2.0","id":"1","method":"tools/list","params":{}}'`

Expected: JSON includes `get-agent-profile`.

- [ ] **Step 5: Commit**

Commit message: `feat(harness): add agent profile metrics and AIS scoring`

---

### Task 2: Phase 2 eval engine + persistence

**Files:**
- Create: `src/Infrastructure/AgentHarness/src/AgentHarness.Core/Models/EvalSuite.cs`
- Create: `src/Infrastructure/AgentHarness/src/AgentHarness.Core/Models/EvalRun.cs`
- Create: `src/Infrastructure/AgentHarness/src/AgentHarness.Application/DTOs/EvalRunDto.cs`
- Create: `src/Infrastructure/AgentHarness/src/AgentHarness.Application/DTOs/ModelComparisonDto.cs`
- Create: `src/Infrastructure/AgentHarness/src/AgentHarness.Application/Services/EvalEngineService.cs`
- Modify: `src/Infrastructure/AgentHarness/src/AgentHarness.Core/Interfaces/IStateStore.cs`
- Modify: `src/Infrastructure/AgentHarness/src/AgentHarness.Infrastructure/Persistence/StateStore.cs`
- Modify: `src/Infrastructure/AgentHarness/src/AgentHarness.Infrastructure/Persistence/HarnessDbContext.cs`
- Modify: `src/Infrastructure/AgentHarness/src/AgentHarness.Infrastructure/Persistence/Configurations.cs`
- Modify: `src/Infrastructure/AgentHarness/src/AgentHarness.Mcp/Program.cs`
- Create: `src/Infrastructure/AgentHarness/src/AgentHarness.Mcp/Tools/EvaluateAgentTool.cs`
- Create: `src/Infrastructure/AgentHarness/src/AgentHarness.Mcp/Tools/CompareModelsTool.cs`
- Create: `src/Infrastructure/AgentHarness/src/AgentHarness.Infrastructure/Persistence/Migrations/*AddEvalTables*.cs`
- Create: `src/Infrastructure/AgentHarness/tests/AgentHarness.UnitTests/Services/EvalEngineServiceTests.cs`
- Create: `src/Infrastructure/AgentHarness/tests/AgentHarness.IntegrationTests/EvalEnginePersistenceTests.cs`

**Interfaces:**
- Consumes: eval suite definitions from `EvalSuite`, persisted runs via `IStateStore`, existing `LlmJudgeService` for open-ended grading.
- Produces: `EvalEngineService.RunSuiteAsync(...)`, `EvalEngineService.CompareModelsAsync(...)`, and MCP tools `evaluate-agent` / `compare-models`.

- [ ] **Step 1: Write failing tests for eval execution and pass@k**

Write `EvalEngineServiceTests` so they assert:
- a stored eval suite can be executed against a fake agent/model,
- `pass_at_1` and `pass_at_k` are computed from the run attempts,
- a model comparison returns sorted results and a winner.

Write `EvalEnginePersistenceTests` so they assert:
- `EvalSuite` and `EvalRun` are persisted and reloaded by the state store,
- the new migration creates the eval tables.

- [ ] **Step 2: Run the tests to verify the missing eval engine path fails**

Run: `dotnet test src/Infrastructure/AgentHarness/tests/AgentHarness.UnitTests/His.Hope.AgentHarness.UnitTests.csproj --filter "EvalEngineServiceTests" -v normal`

Expected: fail because the eval engine and models are not implemented yet.

- [ ] **Step 3: Implement models, service, DB wiring, and MCP tools**

Implement `EvalSuite` with `Name`, `Domain`, `Description`, and `DefinitionJson`.

Implement `EvalRun` with `EvalSuiteId`, `TargetAgent`, `TargetModel`, `PassAt1`, `PassAtK`, `JudgeScore`, `Status`, timestamps, and `RawResultJson`.

Implement `EvalEngineService` with these methods:
- `RunSuiteAsync(string suiteName, string targetAgent, string? targetModel, int k, CancellationToken ct = default)`
- `CompareModelsAsync(string suiteName, string targetAgent, IReadOnlyList<string> modelNames, int k, CancellationToken ct = default)`

Implement `EvalRunDto` with these properties:
- `EvalRunId`
- `EvalSuiteName`
- `TargetAgent`
- `TargetModel`
- `PassAt1`
- `PassAtK`
- `JudgeScore`
- `Status`
- `StartedAt`
- `CompletedAt`

Implement `ModelComparisonDto` with these properties:
- `EvalSuiteName`
- `TargetAgent`
- `Results` (`IReadOnlyList<EvalRunDto>`)
- `WinnerModel`

Wire the tables into `HarnessDbContext`, `Configurations.cs`, `IStateStore`, and `StateStore`.

Add the migration files under `src/Infrastructure/AgentHarness/src/AgentHarness.Infrastructure/Persistence/Migrations/`.

Register the service and MCP tools in `Program.cs`.

- [ ] **Step 4: Run the eval tests plus a migration/build check**

Run: `dotnet test src/Infrastructure/AgentHarness/tests/AgentHarness.UnitTests/His.Hope.AgentHarness.UnitTests.csproj --filter "EvalEngineServiceTests" -v normal`

Expected: pass.

Run: `dotnet test src/Infrastructure/AgentHarness/tests/AgentHarness.IntegrationTests/His.Hope.AgentHarness.IntegrationTests.csproj --filter "EvalEnginePersistenceTests" -v normal`

Expected: pass.

- [ ] **Step 5: Commit**

Commit message: `feat(harness): add eval engine and model comparison`

---

### Task 3: Phase 3 instinct optimizer + auto-record hook

**Files:**
- Modify: `src/Infrastructure/AgentHarness/src/AgentHarness.Core/Models/MemoryEntry.cs`
- Modify: `src/Infrastructure/AgentHarness/src/AgentHarness.Infrastructure/Persistence/Configurations.cs`
- Modify: `src/Infrastructure/AgentHarness/src/AgentHarness.Infrastructure/Persistence/Migrations/*`
- Create: `src/Infrastructure/AgentHarness/src/AgentHarness.Application/DTOs/InstinctOptimizationResultDto.cs`
- Create: `src/Infrastructure/AgentHarness/src/AgentHarness.Application/Services/InstinctOptimizer.cs`
- Modify: `src/Infrastructure/AgentHarness/src/AgentHarness.Application/Services/LoopEngineer.cs`
- Modify: `src/Infrastructure/AgentHarness/src/AgentHarness.Mcp/Program.cs`
- Create: `src/Infrastructure/AgentHarness/tests/AgentHarness.UnitTests/Services/InstinctOptimizerTests.cs`
- Update: `src/Infrastructure/AgentHarness/tests/AgentHarness.UnitTests/Services/LoopEngineerTests.cs`

**Interfaces:**
- Consumes: `IStateStore.GetMemoryEntriesAsync`, `IStateStore.SaveMemoryEntryAsync`, `IMemoryService.StoreAsync`.
- Produces: `MemoryEntry.ConfidenceScore`, `MemoryEntry.BoostConfidence(...)`, `MemoryEntry.DecayConfidence(...)`, `MemoryEntry.MergeFrom(...)`, and `InstinctOptimizer.OptimizeAsync(...)`.

Implement `InstinctOptimizationResultDto` with these properties:
- `BoostedCount`
- `DecayedCount`
- `MergedCount`
- `RecordedCount`
- `UpdatedAt`

- [ ] **Step 1: Write failing tests for boost, decay, and merge behavior**

Write `InstinctOptimizerTests` so they assert:
- successful fixes increase instinct confidence,
- stale instincts decay over time,
- duplicate patterns merge into one survivor with combined use count.

Update `LoopEngineerTests` so they assert a successful auto-fix results in a recorded instinct.

- [ ] **Step 2: Run the tests to confirm the optimizer is missing**

Run: `dotnet test src/Infrastructure/AgentHarness/tests/AgentHarness.UnitTests/His.Hope.AgentHarness.UnitTests.csproj --filter "InstinctOptimizerTests|LoopEngineerTests" -v normal`

Expected: fail because confidence mutation and optimizer logic are not implemented yet.

- [ ] **Step 3: Implement the confidence field, optimizer, and loop hook**

Add `ConfidenceScore` to `MemoryEntry` with helper methods:
- `BoostConfidence(decimal amount)`
- `DecayConfidence(decimal factor)`
- `MergeFrom(MemoryEntry other)`

Implement `InstinctOptimizer.OptimizeAsync()` so it:
- boosts entries that were recently used successfully,
- decays stale entries,
- merges duplicated entries for the same agent/error pattern.

Update `LoopEngineer` so a successful auto-fix path records or refreshes the instinct automatically.

Register `InstinctOptimizer` in `Program.cs`.

- [ ] **Step 4: Run the optimizer tests and migration/build checks**

Run: `dotnet test src/Infrastructure/AgentHarness/tests/AgentHarness.UnitTests/His.Hope.AgentHarness.UnitTests.csproj --filter "InstinctOptimizerTests|LoopEngineerTests" -v normal`

Expected: pass.

Run: `dotnet test src/Infrastructure/AgentHarness/tests/AgentHarness.IntegrationTests/His.Hope.AgentHarness.IntegrationTests.csproj -v normal`

Expected: pass.

- [ ] **Step 5: Commit**

Commit message: `feat(harness): add instinct optimizer and auto-record hook`

---

### Task 4: Phase 4 adaptive quality gates + predictive risk

**Files:**
- Create: `src/Infrastructure/AgentHarness/src/AgentHarness.Application/DTOs/QualityGateRecommendationDto.cs`
- Create: `src/Infrastructure/AgentHarness/src/AgentHarness.Application/DTOs/FailureRiskDto.cs`
- Create: `src/Infrastructure/AgentHarness/src/AgentHarness.Application/Services/AdaptiveQualityGates.cs`
- Modify: `src/Infrastructure/AgentHarness/src/AgentHarness.Application/Services/PipelineEngine.cs`
- Modify: `src/Infrastructure/AgentHarness/src/AgentHarness.Mcp/Program.cs`
- Modify: `src/Infrastructure/AgentHarness/src/AgentHarness.Mcp/Tools/GetPipelineStatusTool.cs`
- Create: `src/Infrastructure/AgentHarness/tests/AgentHarness.UnitTests/Services/AdaptiveQualityGatesTests.cs`
- Update: `src/Infrastructure/AgentHarness/tests/AgentHarness.IntegrationTests/PipelineEndToEndTests.cs`

**Interfaces:**
- Consumes: AIS history, eval history, `PipelineRun`, `PipelineDag`, and `QualityGate` records.
- Produces: `AdaptiveQualityGates.PredictFailureAsync(...)`, `AdaptiveQualityGates.RecommendThresholdsAsync(...)`, and `FailureRiskDto` metadata stored on the pipeline run.

- [ ] **Step 1: Write failing tests for threshold recommendations and risk prediction**

Write `AdaptiveQualityGatesTests` so they assert:
- a low-AIS agent gets stricter recommended thresholds than a high-AIS agent,
- failure risk returns a numeric score plus reason text,
- no method auto-passes or skips a gate.

- [ ] **Step 2: Run the tests to verify the adaptive layer is missing**

Run: `dotnet test src/Infrastructure/AgentHarness/tests/AgentHarness.UnitTests/His.Hope.AgentHarness.UnitTests.csproj --filter "AdaptiveQualityGatesTests" -v normal`

Expected: fail because the service does not exist yet.

- [ ] **Step 3: Implement the service and wire it into pipeline execution**

Implement `AdaptiveQualityGates` with these methods:
- `PredictFailureAsync(PipelineRun run, PipelineDag dag, CancellationToken ct = default)`
- `RecommendThresholdsAsync(string agentName, CancellationToken ct = default)`

Implement `FailureRiskDto` with these properties:
- `RiskScore`
- `RiskLevel`
- `Reason`
- `SuggestedModel`

Implement `QualityGateRecommendationDto` with these properties:
- `AgentName`
- `RecommendedGateThreshold`
- `AisScore`
- `HistoricalPassRate`
- `LastUpdatedAt`

Update `PipelineEngine` so it stores risk metadata before execution and after each loop iteration, but never bypasses existing gate logic.

Update `GetPipelineStatusTool` so pipeline risk metadata is visible in the MCP response.

Register the service in `Program.cs`.

- [ ] **Step 4: Run the adaptive-gate tests plus a full harness build**

Run: `dotnet test src/Infrastructure/AgentHarness/tests/AgentHarness.UnitTests/His.Hope.AgentHarness.UnitTests.csproj --filter "AdaptiveQualityGatesTests" -v normal`

Expected: pass.

Run: `dotnet test src/Infrastructure/AgentHarness/tests/AgentHarness.IntegrationTests/His.Hope.AgentHarness.IntegrationTests.csproj --filter "PipelineEndToEndTests" -v normal`

Expected: pass.

- [ ] **Step 5: Commit**

Commit message: `feat(harness): add adaptive quality gate recommendations`

---

### Task 5: Full harness regression verification

**Files:**
- Test: `src/Infrastructure/AgentHarness/tests/AgentHarness.UnitTests/**/*`
- Test: `src/Infrastructure/AgentHarness/tests/AgentHarness.IntegrationTests/**/*`
- Test: `src/Infrastructure/AgentHarness/src/AgentHarness.Mcp/**/*`

**Interfaces:**
- Consumes: the implemented metrics, eval, instinct, and adaptive-gate services.
- Produces: build/test evidence that the new agent-intelligence layer works end-to-end.

- [ ] **Step 1: Build the harness solution components**

Run: `dotnet build src/Infrastructure/AgentHarness/src/AgentHarness.Mcp/His.Hope.AgentHarness.Mcp.csproj -v normal`

Expected: build succeeds with no errors.

- [ ] **Step 2: Run unit and integration tests**

Run: `dotnet test src/Infrastructure/AgentHarness/tests/AgentHarness.UnitTests/His.Hope.AgentHarness.UnitTests.csproj -v normal`

Run: `dotnet test src/Infrastructure/AgentHarness/tests/AgentHarness.IntegrationTests/His.Hope.AgentHarness.IntegrationTests.csproj -v normal`

Expected: both pass.

- [ ] **Step 3: Rebuild and restart the harness container**

Run: `docker compose -f docker/docker-compose.yml build agentharness`

Run: `docker compose -f docker/docker-compose.yml up -d agentharness`

Expected: container reaches `healthy`.

- [ ] **Step 4: Smoke-test the MCP surface from the command line**

Run `tools/list` and confirm these tool names exist:
- `get-agent-profile`
- `evaluate-agent`
- `compare-models`
- `record-instinct`
- `query-instincts`
- `route-llm`

Run `get-agent-profile` for a known agent and confirm it returns an AIS score and history array.

Run `evaluate-agent` on one stored suite and confirm it returns `pass_at_1`, `pass_at_k`, and a persisted run id.

Run `compare-models` on the same suite and confirm the response includes one row per model and a winner model.

- [ ] **Step 5: Final regression summary**

Record any remaining failures with the exact command, failing test name, and first stack trace line.
