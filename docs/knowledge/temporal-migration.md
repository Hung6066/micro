# Temporal.io Distributed Execution — Architecture & Migration

## Architecture

```
┌──────────────────────────────────────────────────┐
│               External Agents                     │
│   (OpenCode Angular, .NET, etc.)                  │
│         ▲                    │                     │
│         │  poll              │ complete-task       │
│         │  (get-pending)     ▼                     │
│    ┌────┴──────────────────────────┐              │
│    │   Agent Harness MCP Server    │              │
│    │   (Port 5200)                 │              │
│    │                               │              │
│    │  IPipelineEngine              │              │
│    │   ├─ PipelineEngine (polling) │ ← fallback   │
│    │   └─ TemporalPipelineEngine   │ ← Temporal   │
│    │      (starts Temporal wf)     │              │
│    └────────┬──────────────────────┘              │
│             │ StartWorkflow / Cancel / Query       │
│             ▼                                      │
│    ┌──────────────────────────────────┐            │
│    │      Temporal Cluster            │            │
│    │  ┌──────────────────────────┐   │            │
│    │  │ Temporal Server (7233)   │   │            │
│    │  │ Frontend │ History │     │   │            │
│    │  │ Matching │ Worker        │   │            │
│    │  └──────────┬───────────────┘   │            │
│    │             │                    │            │
│    │  ┌──────────▼───────────────┐   │            │
│    │  │ Temporal Web UI (8233)   │   │            │
│    │  └──────────────────────────┘   │            │
│    └──────────────────────────────────┘            │
│             │                                      │
│    ┌────────▼──────────────────────────┐           │
│    │   Temporal Worker (Port 5270)     │           │
│    │                                   │           │
│    │  PipelineWorkflow (orchestration) │           │
│    │    │ ExecutePipelineAsync         │           │
│    │    │  ├─ phase loop (5 phases)    │           │
│    │    │  │  └─ ExecutePhaseAsync     │           │
│    │    │  ├─ quality gate evaluation  │           │
│    │    │  └─ LoopEngineer (fix loop)  │           │
│    │                                   │           │
│    │  AgentActivities (worker impl)    │           │
│    │    │  ExecutePhaseAsync           │           │
│    │    │    ├─ DispatchAgent          │           │
│    │    │    └─ Poll with heartbeats   │           │
│    │    │  EvaluatePhaseGatesAsync     │           │
│    │    │  CheckAllGatesPassedAsync    │           │
│    │    │  RunLoopEngineerAsync        │           │
│    │    └─ ResetFailedNodesAsync       │           │
│    └──────────────────────────────────┘            │
└──────────────────────────────────────────────────┘
```

## Data Flow (Temporal mode)

```
StartAsync()
  │
  ▼
TemporalClient.StartWorkflowAsync(PipelineWorkflow)
  │  workflowId = "pipeline-{run.Id}"
  │  taskQueue = "agent-harness"
  │
  ▼
PipelineWorkflow.ExecutePipelineAsync(run)
  │
  ├── loop 0..2 (MaxLoops=3)
  │   │
  │   ├── for each phase [Plan, Implement, Test, Validate, Commit]
  │   │   │
  │   │   ├── Activity: ExecutePhaseAsync(pipelineRunId, phase, loop)
  │   │   │   ├── Rebuild DAG from stored PipelineRun
  │   │   │   ├── Get phase nodes
  │   │   │   ├── For each node:
  │   │   │   │   ├── Create AgentRun → Dispatch → Save to DB
  │   │   │   │   └── Poll DB w/ heartbeat (5s intervals)
  │   │   │   └── Return PhaseResult
  │   │   │
  │   │   ├── Activity: EvaluatePhaseGatesAsync(pipelineRunId, phase)
  │   │   │   └── Creates QualityGates in DB per node + phase
  │   │   │
  │   │   └── Activity: SaveCheckpointAsync(pipelineRunId, phase, loop)
  │   │       └── Creates PipelineCheckpoint in DB
  │   │
  │   ├── Activity: CheckAllGatesPassedAsync
  │   │   └── Returns bool
  │   │
  │   └── If gates failed && loop < MaxLoops-1:
  │       ├── Activity: RunLoopEngineerAsync
  │       │   └── Returns LoopEngineerResult (CanContinue / EscalationReason)
  │       └── Activity: ResetFailedNodesAsync
  │
  └── Return PipelineRun (Completed / Failed)
```

## Key Differences from Polling Engine

| Concern | Polling Engine (PipelineEngine) | Temporal (PipelineWorkflow) |
|---------|-------------------------------|-----------------------------|
| **Execution** | `Task.Run` + in-memory loop | Temporal Worker — durable, survives crash |
| **Agent wait** | Polls DB every 5s | Polls DB w/ heartbeats (Temporal retries on crash) |
| **Checkpoint** | Manual `PipelineCheckpoint` | Temporal event history (built-in) |
| **Retry** | Custom retry via Polly | Temporal RetryPolicy (exponential backoff) |
| **Timeout** | Manual deadline check | Activity StartToCloseTimeout |
| **Observability** | Serilog + OpenTelemetry | Temporal Web UI + OpenTelemetry |
| **Crash recovery** | Resume from DB checkpoint | Temporal replays event history |
| **Concurrency** | ConcurrentDictionary gauge | Temporal task queue + worker pool |

## Configuration

### Flag: `AgentHarness__UseTemporal`
- `false` (default): Uses existing `PipelineEngine` with polling loop
- `true`: Uses `TemporalPipelineEngine` — starts Temporal workflow instead

### Environment Variables

| Variable | Default | Description |
|----------|---------|-------------|
| `AgentHarness__UseTemporal` | `false` | Enable Temporal execution |
| `AgentHarness__TemporalServerUrl` | `localhost:7233` | Temporal gRPC endpoint |
| `Temporal__ServerUrl` | `localhost:7233` | (Worker) Temporal gRPC endpoint |

## Migration Path

### Phase 1: Deploy Temporal (infra only)
```bash
# Start Temporal alongside existing services
docker compose --profile temporal up -d temporal

# Verify Temporal Web UI at http://localhost:8233
```

### Phase 2: Deploy Temporal Worker
```bash
docker compose --profile temporal up -d temporal-worker
```

### Phase 3: Enable Temporal on Agent Harness
```bash
# Stop and restart with Temporal enabled
AGENT_HARNESS_USE_TEMPORAL=true docker compose up -d agentharness
```

### Phase 4: Monitor and verify
- Check Temporal Web UI (http://localhost:8233) for workflow execution
- Verify pipeline runs complete successfully
- Monitor worker health (http://localhost:5270/health)

### Phase 5: Remove polling fallback (future)
Once Temporal mode is stable, `PipelineEngine` and `PipelineCheckpoint` can be removed.

## Running Locally

### With Temporal (full stack):
```bash
# Start all services
docker compose --profile temporal up -d

# Enable Temporal mode
$env:AGENT_HARNESS_USE_TEMPORAL="true"
```

### Without Temporal (existing behavior):
```bash
docker compose up -d
```
