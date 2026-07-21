# Task 1 Report: Phase 1 metrics + agent profile

## Status
**DONE**

## Commits
- `b676096` — `feat(harness): add agent profile metrics and AIS scoring`
- `6db0e5a` — `fix(harness): resolve 5 Task 1 review findings`
- pending controller fix — full-history retrieval + metrics recorder layer-boundary cleanup

## What changed

### Created
- `src/Infrastructure/AgentHarness/src/AgentHarness.Application/DTOs/AgentProfileDto.cs`
- `src/Infrastructure/AgentHarness/src/AgentHarness.Application/DTOs/AgentRunSummaryDto.cs`
- `src/Infrastructure/AgentHarness/src/AgentHarness.Application/Services/AgentMetricsService.cs`
- `src/Infrastructure/AgentHarness/src/AgentHarness.Core/Interfaces/IAgentMetricsRecorder.cs`
- `src/Infrastructure/AgentHarness/src/AgentHarness.Infrastructure/Observability/HarnessMetricsRecorder.cs`
- `src/Infrastructure/AgentHarness/src/AgentHarness.Mcp/Tools/GetAgentProfileTool.cs`
- `src/Infrastructure/AgentHarness/tests/AgentHarness.UnitTests/Services/AgentMetricsServiceTests.cs`
- `src/Infrastructure/AgentHarness/tests/AgentHarness.UnitTests/Tools/GetAgentProfileToolTests.cs`

### Modified
- `src/Infrastructure/AgentHarness/src/AgentHarness.Core/Interfaces/IStateStore.cs`
- `src/Infrastructure/AgentHarness/src/AgentHarness.Infrastructure/Persistence/StateStore.cs`
- `src/Infrastructure/AgentHarness/src/AgentHarness.Infrastructure/Observability/HarnessMetrics.cs`
- `src/Infrastructure/AgentHarness/src/AgentHarness.Mcp/Program.cs`
- `src/Infrastructure/AgentHarness/tests/AgentHarness.UnitTests/His.Hope.AgentHarness.UnitTests.csproj`

## Implementation notes
- `AgentMetricsService.GetAgentProfileAsync(string agentName, CancellationToken ct = default)` computes AIS from persisted agent runs, quality gates, memory entries, retry counts, and confidence scores.
- The service reads full persisted agent-run history through `IStateStore.GetAllAgentRunsAsync`, then filters by agent name.
- Metric emission is behind `IAgentMetricsRecorder` in Core, with `HarnessMetricsRecorder` implemented in Infrastructure. The Application layer no longer references Infrastructure.
- `GetAgentProfileTool` serializes the `AgentProfileDto` shape directly using camelCase JSON.

## AIS formula
```text
AIS = (taskCompletionRate × 0.25 + qualityGatePassRate × 0.20 + retryRate × 0.15 + confidenceAccuracy × 0.15 + learningEffectiveness × 0.10 + averageJudgeScore × 0.15) × 100
```

## TDD evidence

### RED
Initial focused command:
```powershell
dotnet test src/Infrastructure/AgentHarness/tests/AgentHarness.UnitTests/His.Hope.AgentHarness.UnitTests.csproj --filter "AgentMetricsServiceTests|GetAgentProfileToolTests" -v normal
```
Expected failure: missing `AgentMetricsService`, DTOs, and `GetAgentProfileTool`.

### GREEN
Final unit-test command:
```powershell
dotnet test src/Infrastructure/AgentHarness/tests/AgentHarness.UnitTests/His.Hope.AgentHarness.UnitTests.csproj -v minimal
```
Result:
```text
Passed!  - Failed: 0, Passed: 18, Skipped: 0, Total: 18, Duration: 196 ms
```

Warnings observed:
- Existing `NU1902` warning for `OpenTelemetry.Exporter.OpenTelemetryProtocol` 1.12.0.
- Existing `ASP0000` warning in `Program.cs` for `BuildServiceProvider` in stdio mode.

## Runtime smoke evidence

Docker build:
```powershell
docker compose -f docker/docker-compose.yml build agentharness
```
Result: `Image docker-agentharness Built`

Restart:
```powershell
docker compose -f docker/docker-compose.yml up -d agentharness
```
Result: `Container his-hope-agentharness Started`

Tool registration:
```powershell
curl -s -X POST http://localhost:5200/mcp -H "Content-Type: application/json" -H "X-API-Key: dev-key-change-in-production" -d '{"jsonrpc":"2.0","id":"1","method":"tools/list","params":{}}'
```
Result: response includes `get-agent-profile`.

Live profile call:
```powershell
curl -s -X POST http://localhost:5200/mcp -H "Content-Type: application/json" -H "X-API-Key: dev-key-change-in-production" -d '{"jsonrpc":"2.0","id":"2","method":"tools/call","params":{"name":"get-agent-profile","arguments":{"agent_name":"dotnet"}}}'
```
Result: response includes `agentName`, `aisScore`, `totalRuns`, `successfulRuns`, and `recentRuns`.

## Self-review findings
- Spec seam present: `AgentMetricsService.GetAgentProfileAsync(...)` and MCP tool `get-agent-profile`.
- DTO shape matches the brief.
- Full persisted agent-run history is used instead of only running pipelines.
- Metrics are exposed through `HarnessMetrics` without violating Application → Infrastructure boundaries.
- No autonomous gate bypassing, model training, UI redesign, or external analytics service added.
