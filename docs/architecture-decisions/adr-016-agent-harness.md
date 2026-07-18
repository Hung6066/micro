# ADR-016: .NET 8 Hybrid Agent Harness

## Status

Accepted

## Date

2026-07-18

## Context

The His.Hope agent system relies on 16 specialized AI agents coordinated through an `@architect` primary agent and `@orchestrator` pipeline coordinator. As the system matured, several limitations emerged:

1. **No runtime orchestration** — Agents were stateless functions invoked per-session. There was no persistent pipeline state, making multi-step workflows fragile.
2. **No state persistence** — Pipeline progress, agent run history, and accumulated artifacts were lost when a session ended. Resuming a failed pipeline required manual re-invocation.
3. **No self-healing** — When a quality gate failed, the orchestrator relied on human intervention. There was no automated retry, error classification, or fix loop.
4. **No distributed tracing** — With 16 agents across multiple services, correlating events across a pipeline execution was impossible without a centralized store.
5. **OpenCode MCP boundary** — OpenCode supports MCP tools for stateful operations, but it lacks built-in workflow DAG execution, backpressure control, and circuit breaker patterns.

The agent harness needed to bridge the gap between OpenCode's MCP capabilities and a production-grade workflow engine without introducing a completely separate orchestration system.

## Decision

We will build a **.NET 8 hybrid agent harness** — an MCP server implemented in Clean Architecture that provides stateful pipeline orchestration, event-driven agent dispatch, and autonomous self-healing.

### Architecture

```
┌─────────────────────────────────────┐
│         OpenCode (MCP Client)       │
└──────────────┬──────────────────────┘
               │ tools: start_pipeline, get_status, dispatch_agent, cancel_pipeline
               ▼
┌─────────────────────────────────────┐
│      AgentHarness.Mcp (MCP Server)  │
│  ┌───────────────────────────────┐  │
│  │   Tools (MCP Tool Contracts) │  │
│  └──────────┬────────────────────┘  │
│             ▼                        │
│  ┌───────────────────────────────┐  │
│  │  Application (CQRS + MediatR) │  │
│  │  ┌──────────┐ ┌────────────┐  │  │
│  │  │ Commands │ │  Queries   │  │  │
│  │  └──────────┘ └────────────┘  │  │
│  │  ┌────────────────────────┐   │  │
│  │  │ Services:              │   │  │
│  │  │ - PipelineEngine       │   │  │
│  │  │ - LoopEngineer         │   │  │
│  │  │ - BackpressureCtrl     │   │  │
│  │  │ - ChangeScopeAnalyzer  │   │  │
│  │  │ - ConsensusOrchestrator│   │  │
│  │  │ - AgentPoolManager     │   │  │
│  │  └────────────────────────┘   │  │
│  └──────────┬────────────────────┘  │
│             ▼                        │
│  ┌───────────────────────────────┐  │
│  │      Domain (Pure DDD)        │  │
│  │  PipelineRun, AgentRun, DAG   │  │
│  └──────────┬────────────────────┘  │
│             ▼                        │
│  ┌───────────────────────────────┐  │
│  │  Infrastructure               │  │
│  │  - CockroachDB (EF Core)      │  │
│  │  - RabbitMQ EventBus          │  │
│  │  - OpenCode AgentDispatcher   │  │
│  │  - OpenTelemetry Metrics      │  │
│  │  - Polly Resilience           │  │
│  └───────────────────────────────┘  │
└─────────────────────────────────────┘
```

### Key Technology Choices

| Component | Choice | Rationale |
|-----------|--------|-----------|
| Runtime | .NET 8 ASP.NET Core | Existing stack, native AOT, strong typing |
| Architecture | Clean Architecture (DDD, CQRS) | Consistent with all His.Hope services |
| Pipeline orchestration | MediatR + custom DAG engine | No external dependency; lightweight |
| State store | CockroachDB via EF Core | Existing DB infrastructure, CDC support |
| Event bus | RabbitMQ | Existing event infrastructure |
| Agent dispatch | OpenCode MCP `task` tool | Native agent protocol; no translation layer |
| Resilience | Polly v8 | Retry, circuit breaker, timeout policies |
| Observability | OpenTelemetry Metrics & Tracing | Consistent with His.Hope monitoring stack |
| Workflow definition | YAML | Human-readable, version-controllable |

### Pipeline State Machine

```
Pending → Running → [ Plan → Implement → Test → Validate → Commit ] → Completed
                  ↘                                                      ↗
                    Failed ← (any phase gate failure, after retries)
                  ↘
                    Cancelled ← (user-initiated)
```

### Error Classification & Auto-Fix

The `LoopEngineer` classifies errors into categories: `CompilationError`, `TestFailure`, `ContractViolation`, `QualityGateFailure`, `InfrastructureError`, `KnownGotcha`, `LogicError`, `Unknown`. Each category has an `IsAutoFixable` flag. The `ConfidenceScorer` evaluates multiple signals (pattern match, change size, history, reversibility, security boundary) to determine if autonomous fix should proceed. If confidence ≥ 0.8, the loop engineer applies the fix directly. If confidence < 0.8, it escalates to human.

### Safety Constraints

- Safety fence: restricted paths (`vault/`, `/secrets/`, `/certificates/`, `opencode.json`) can never be auto-fixed
- Max 3 iterations per loop engineer session
- Circuit breaker opens after 5 consecutive failures (per agent)
- Backpressure controller: max 10 concurrent pipelines, 20 concurrent agent dispatches

### Metrics

| Metric | Type | Purpose |
|--------|------|---------|
| `pipeline.start.count` | Counter | Pipeline execution rate |
| `pipeline.complete.count` | Counter | Pipeline completion rate |
| `agent.dispatch.count` | Counter | Agent dispatch rate |
| `agent.retry.count` | Counter | Agent retry rate |
| `event.published.count` | Counter | Event bus throughput |
| `pipeline.duration.seconds` | Histogram | Pipeline execution duration |
| `agent.duration.seconds` | Histogram | Agent execution duration |
| `pipeline.active` | Gauge | Active pipeline count |

### Quality Gates

| Gate | Auto-fixable? | Escalation |
|------|:---:|:---:|
| dotnet-build | ✅ | After 3 retries |
| angular-build | ✅ | After 3 retries |
| proto-lint / breaking | ✅ | Immediate (breaking) |
| backend-unit-tests | ✅ | After 3 retries |
| contract-tests | ✅ | After 3 retries |
| wcag-accessibility | ❌ | Manual |
| hipaa-compliance | ❌ | Manual |
| vault-secrets | ❌ | Manual |
| backend-coverage | ❌ | Manual (developer decision) |

## Alternatives Considered

### 1. External Python Harness (Temporal / Prefect)

**Pros:** Mature workflow engine, built-in retry, durable execution.
**Cons:** New language/runtime for the team; network boundary between OpenCode MCP (C#) and Python worker; added infrastructure (Temporal cluster). Overkill for the current scale (~100 pipeline executions/day).

**Why not chosen:** The cognitive overhead of maintaining a Python harness alongside the existing .NET stack outweighed benefits. Our pipeline complexity doesn't yet warrant a dedicated workflow engine.

### 2. Pure OpenCode Extensions (custom MCP tools in TypeScript)

**Pros:** No additional runtime; runs entirely within OpenCode.
**Cons:** OpenCode MCP runs as a subprocess; state would be in-memory and lost on restart; limited to single-node execution; no distributed event bus.

**Why not chosen:** Lack of persistence and observability. For a production system handling hospital data, durable state and audit trails are non-negotiable.

### 3. Go Orchestration Engine

**Pros:** Lightweight binary, excellent concurrency, fast startup.
**Cons:** Team expertise is .NET/C#; would need to port Clean Architecture patterns; fewer ecosystem libraries for CQRS/DDD; would diverge from His.Hope technology standard.

**Why not chosen:** The team's strong .NET expertise and existing Clean Architecture conventions made Go a significant productivity loss. The performance characteristics of Go weren't needed for an MCP-bound control plane.

## Consequences

### Positive

- **Stateful pipelines** — Pipeline runs survive session restarts; history is queryable
- **Autonomous self-healing** — Loop Engineer reduces mean-time-to-recovery for common failures
- **Observability** — Full OpenTelemetry integration with existing Prometheus/Grafana stack
- **Consistency** — Same Clean Architecture, CQRS, and .NET patterns as other services
- **Extensibility** — YAML workflow definitions allow non-developers to define pipeline shapes

### Negative

- **CockroachDB dependency** — Adding another database consumer; requires migration management
- **RabbitMQ topology** — Event bus needs exchanges/queues for agent events; adds operational surface
- **Startup latency** — .NET 8 JIT compilation adds ~2s startup time (acceptable for a control plane)
- **Learning curve** — Operator team needs to understand DAG execution model

### Neutral

- **Coupling to OpenCode MCP protocol** — If OpenCode changes MCP tool contract, harness adapters must update
- **Dual dispatch** — Agents are dispatched both through OpenCode `task` tool (direct) and harness (stateful). The harness is the preferred path for pipelines; direct dispatch remains available for ad-hoc tasks

## Technical Details

### Database Schema (CockroachDB)

```sql
CREATE TABLE pipeline_runs (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    workflow_id STRING NOT NULL,
    status STRING NOT NULL DEFAULT 'Pending',
    dag_definition JSONB,
    parameters JSONB,
    triggered_by STRING NOT NULL,
    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    started_at TIMESTAMPTZ,
    completed_at TIMESTAMPTZ,
    timeout_at TIMESTAMPTZ,
    metadata JSONB
);

CREATE TABLE agent_runs (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    pipeline_run_id UUID NOT NULL REFERENCES pipeline_runs(id),
    agent_name STRING NOT NULL,
    phase STRING NOT NULL,
    status STRING NOT NULL DEFAULT 'Pending',
    input JSONB,
    output JSONB,
    error STRING,
    started_at TIMESTAMPTZ,
    completed_at TIMESTAMPTZ,
    retry_count INT NOT NULL DEFAULT 0,
    CONSTRAINT fk_pipeline FOREIGN KEY (pipeline_run_id) REFERENCES pipeline_runs(id)
);
```

### Resilience Configuration (Polly)

```
Retry: 3 attempts, exponential backoff (100ms, 500ms, 2s)
Circuit Breaker: 5 failures → open for 30s → half-open
Timeout: 30s per agent dispatch, 5m per pipeline
Bulkhead: 10 concurrent pipelines, 20 concurrent agents
```

## References

- ADR-001: Microservice Architecture
- ADR-003: Event-Driven Architecture with Outbox Pattern
- ADR-008: Observability Stack (OpenTelemetry + Prometheus + Grafana)
- ADR-012: Database Migration Strategy
- ADR-014: Multi-Region Deployment
