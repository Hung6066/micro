# Agent Harness & Loop Engineer — Design Spec

**Date:** 2026-07-18
**Status:** Draft — Awaiting User Review
**Author:** Lead System Architect
**Scope:** OpenCode agent system infrastructure upgrade for His.Hope

---

## 1. Problem Statement

### Current State

The His.Hope agent system operates with **no runtime harness**. The orchestrator and dispatcher are purely conceptual — defined in markdown documentation and interpreted by the LLM at runtime. Agent communication relies entirely on OpenCode's built-in `task` tool for subagent dispatch.

**Pain points:**

| Pain Point | Impact |
|---|---|
| **No state persistence** | Pipeline state lives only in LLM context; lost on session restart |
| **No retry/loop** | If a quality gate fails, human must manually re-trigger |
| **No feedback loops** | Agents cannot learn from each other's failures within a pipeline run |
| **Always runs full pipeline** | A CSS-only change still triggers all 5 phases including backend tests |
| **No confidence tracking** | No way to know if an agent's output is trustworthy |
| **No self-healing** | When tests fail, there's no automated fix → re-test cycle |
| **No workflow customization** | All changes follow the same rigid 5-phase pipeline |

### Goal

Build a **hybrid agent harness** (.NET 8 MCP server) that provides runtime orchestration, autonomous error correction via a **Loop Engineer** agent, adaptive pipeline execution, and a **workflow framework** for defining custom agent pipelines — while preserving the existing OpenCode agent ecosystem.

---

## 2. Architecture Overview

### 2.1 Hybrid Model

```
┌─────────────────────────────────────────────────────────────────┐
│                     OpenCode Agent System                        │
│                                                                  │
│  ┌──────────┐ ┌──────────┐ ┌────────┐ ┌──────────────────────┐  │
│  │ @angular │ │ @dotnet  │ │  @dba  │ │ ... 17 agents total  │  │
│  └────┬─────┘ └────┬─────┘ └───┬────┘ └──────────┬───────────┘  │
│       │             │           │                  │             │
│       └─────────────┼───────────┼──────────────────┘             │
│                     │           │               │                │
│              ┌──────▼───────────▼───────────────▼──────┐         │
│              │         Agent Harness (.NET 8)          │         │
│              │  ┌──────────────────────────────────┐   │         │
│              │  │  MCP Server (gRPC endpoint)      │   │         │
│              │  │  Tools: start/stop/query/cancel  │   │         │
│              │  └────────────┬─────────────────────┘   │         │
│              │  ┌────────────▼─────────────────────┐   │         │
│              │  │  Pipeline Engine (DAG scheduler) │   │         │
│              │  └────────────┬─────────────────────┘   │         │
│              │  ┌────────────▼─────────────────────┐   │         │
│              │  │  Loop Engineer (autonomous fix)  │   │         │
│              │  │  Analyze → Plan → Autofix → Loop │   │         │
│              │  └────────────┬─────────────────────┘   │         │
│              │  ┌────────────▼─────────────────────┐   │         │
│              │  │  State Store (CockroachDB)       │   │         │
│              │  └──────────────────────────────────┘   │         │
│              └─────────────────────────────────────────┘         │
│                                                                  │
│  ┌──────────────────────────────────────────────────────────┐   │
│  │           Workflow Framework (Phase 2)                    │   │
│  │  ┌──────────────────┐  ┌────────────────────────────┐    │   │
│  │  │  YAML DSL Parser │  │  C# Fluent API Builder     │    │   │
│  │  └────────┬─────────┘  └──────────┬─────────────────┘    │   │
│  │           └──────────┬────────────┘                      │   │
│  │                      ▼                                   │   │
│  │           ┌──────────────────┐                           │   │
│  │           │ Workflow Compiler│                           │   │
│  │           └──────────────────┘                           │   │
│  └──────────────────────────────────────────────────────────┘   │
└─────────────────────────────────────────────────────────────────┘
```

### 2.2 Key Principles

1. **Harness augments, not replaces** — Existing orchestrator/dispatcher remain; harness provides runtime execution layer
2. **Hybrid runtime** — Harness is .NET 8 code (persistent, stateful); agents remain LLM-based within OpenCode
3. **Reuses infrastructure** — CockroachDB for state, RabbitMQ for events, Linkerd for mTLS, Vault for secrets
4. **Zero-trust** — All harness ↔ agent communication via MCP protocol with mTLS
5. **Full Core scope** — Agent pool scaling, circuit breakers, distributed execution, plugin system

---

## 3. Phase 1: Harness Core

### 3.1 Technology Stack

| Layer | Technology |
|---|---|
| Language | C# 12 / .NET 8 |
| Architecture | Clean Architecture (Domain → Application → Infrastructure → MCP) |
| Database | CockroachDB (via EF Core) |
| Communication | gRPC (internal) + MCP (external to OpenCode) |
| Event Bus | RabbitMQ (reusing existing cluster) |
| Resilience | Polly (circuit breaker, retry, timeout, bulkhead) |
| Observability | OpenTelemetry → Jaeger (traces) + Prometheus (metrics) |
| Secret Management | HashiCorp Vault |
| Container | Distroless .NET 8 image |

### 3.2 Project Structure

```
src/Infrastructure/AgentHarness/
├── AgentHarness.sln
├── src/
│   ├── AgentHarness.Core/              # Domain layer
│   │   ├── Models/
│   │   │   ├── AgentRun.cs
│   │   │   ├── PipelineRun.cs
│   │   │   ├── QualityGate.cs
│   │   │   ├── Artifact.cs
│   │   │   └── AgentDefinition.cs
│   │   ├── Events/
│   │   │   ├── AgentStarted.cs
│   │   │   ├── AgentCompleted.cs
│   │   │   ├── AgentFailed.cs
│   │   │   ├── GatePassed.cs
│   │   │   └── GateFailed.cs
│   │   ├── Interfaces/
│   │   │   ├── IAgentDispatcher.cs
│   │   │   ├── IPipelineEngine.cs
│   │   │   ├── IStateStore.cs
│   │   │   └── IEventBus.cs
│   │   └── ValueObjects/
│   │       ├── AgentRunId.cs
│   │       ├── PipelineRunId.cs
│   │       └── ConfidenceScore.cs
│   │
│   ├── AgentHarness.Application/       # CQRS via MediatR
│   │   ├── Commands/
│   │   │   ├── StartPipeline/
│   │   │   ├── DispatchAgent/
│   │   │   ├── RetryAgent/
│   │   │   └── CancelPipeline/
│   │   ├── Queries/
│   │   │   ├── GetPipelineStatus/
│   │   │   └── GetAgentRunHistory/
│   │   ├── Behaviors/
│   │   │   ├── CircuitBreakerBehavior.cs
│   │   │   ├── RetryBehavior.cs
│   │   │   └── TimeoutBehavior.cs
│   │   └── Services/
│   │       ├── PipelineEngine.cs
│   │       ├── AgentPoolManager.cs
│   │       └── BackpressureController.cs
│   │
│   ├── AgentHarness.Infrastructure/
│   │   ├── Persistence/
│   │   │   ├── HarnessDbContext.cs
│   │   │   └── Migrations/
│   │   ├── Dispatch/
│   │   │   └── OpenCodeAgentDispatcher.cs
│   │   ├── EventBus/
│   │   │   └── RabbitMQEventBus.cs
│   │   ├── Observability/
│   │   │   ├── HarnessMetrics.cs
│   │   │   └── HarnessTracing.cs
│   │   └── Plugins/
│   │       ├── IAgentPlugin.cs
│   │       └── PluginLoader.cs
│   │
│   └── AgentHarness.Mcp/               # MCP Server
│       ├── Program.cs
│       ├── Tools/
│       │   ├── StartPipelineTool.cs
│       │   ├── GetStatusTool.cs
│       │   ├── DispatchAgentTool.cs
│       │   └── CancelPipelineTool.cs
│       └── McpServerConfig.cs
│
└── tests/
    ├── AgentHarness.UnitTests/
    ├── AgentHarness.IntegrationTests/
    └── AgentHarness.ContractTests/
```

### 3.3 Database Schema (CockroachDB)

```sql
CREATE TABLE pipeline_runs (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    workflow_id VARCHAR(256) NOT NULL,
    status VARCHAR(32) NOT NULL,      -- pending, running, completed, failed, cancelled
    dag_definition JSONB NOT NULL,
    started_at TIMESTAMPTZ,
    completed_at TIMESTAMPTZ,
    timeout_at TIMESTAMPTZ,
    metadata JSONB,
    created_at TIMESTAMPTZ DEFAULT now()
);

CREATE TABLE agent_runs (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    pipeline_run_id UUID REFERENCES pipeline_runs(id),
    agent_name VARCHAR(128) NOT NULL,
    task_description TEXT NOT NULL,
    status VARCHAR(32) NOT NULL,
    attempt_number INT DEFAULT 1,
    confidence_score DECIMAL(3,2),
    started_at TIMESTAMPTZ,
    completed_at TIMESTAMPTZ,
    output_artifact_ref VARCHAR(512),
    error_message TEXT,
    retry_count INT DEFAULT 0,
    max_retries INT DEFAULT 3,
    circuit_state VARCHAR(16) DEFAULT 'closed',
    created_at TIMESTAMPTZ DEFAULT now()
);

CREATE TABLE quality_gate_results (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    pipeline_run_id UUID REFERENCES pipeline_runs(id),
    agent_run_id UUID REFERENCES agent_runs(id),
    gate_id VARCHAR(128) NOT NULL,
    gate_name VARCHAR(256) NOT NULL,
    severity VARCHAR(16) NOT NULL,     -- block, warn, info
    passed BOOLEAN NOT NULL,
    output TEXT,
    checked_at TIMESTAMPTZ DEFAULT now()
);

CREATE TABLE artifacts (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    agent_run_id UUID REFERENCES agent_runs(id),
    artifact_type VARCHAR(64) NOT NULL,
    content_type VARCHAR(128),
    storage_ref VARCHAR(512) NOT NULL,
    size_bytes BIGINT,
    created_at TIMESTAMPTZ DEFAULT now()
);

CREATE TABLE agent_pool (
    agent_name VARCHAR(128) PRIMARY KEY,
    active_instances INT DEFAULT 0,
    max_instances INT DEFAULT 5,
    min_instances INT DEFAULT 1,
    circuit_state VARCHAR(16) DEFAULT 'closed',
    failure_count INT DEFAULT 0,
    last_failure_at TIMESTAMPTZ,
    updated_at TIMESTAMPTZ DEFAULT now()
);
```

### 3.4 gRPC Service Contract

```protobuf
// File: src/Shared/Protos/agent_harness.proto

service AgentHarnessService {
    rpc StartPipeline(StartPipelineRequest) returns (StartPipelineResponse);
    rpc GetPipelineStatus(GetPipelineStatusRequest) returns (PipelineStatus);
    rpc CancelPipeline(CancelPipelineRequest) returns (CancelPipelineResponse);
    rpc DispatchAgent(DispatchAgentRequest) returns (DispatchAgentResponse);
    rpc ListAgentRuns(ListAgentRunsRequest) returns (ListAgentRunsResponse);
    rpc GetArtifact(GetArtifactRequest) returns (GetArtifactResponse);
    rpc StreamPipelineEvents(PipelineEventsRequest) returns (stream PipelineEvent);
}

message StartPipelineRequest {
    string workflow_id = 1;
    map<string, string> parameters = 2;
    string triggered_by = 3;
}

message DispatchAgentRequest {
    string agent_name = 1;
    string task_description = 2;
    string context_from = 3;
    int32 max_retries = 4;
    int32 timeout_seconds = 5;
}

message PipelineEvent {
    oneof event {
        AgentStartedEvent agent_started = 1;
        AgentCompletedEvent agent_completed = 2;
        AgentFailedEvent agent_failed = 3;
        GatePassedEvent gate_passed = 4;
        GateFailedEvent gate_failed = 5;
        PipelineCompletedEvent pipeline_completed = 6;
    }
}
```

### 3.5 MCP Server Tools

| Tool | Input | Output | Description |
|---|---|---|---|
| `harness_start_pipeline` | workflow_id, params, trigger | pipeline_run_id | Start new pipeline |
| `harness_get_status` | pipeline_run_id | Full status DAG + gate results | Query pipeline state |
| `harness_dispatch_agent` | agent_name, task, context | agent_run_id | Harness dispatches agent to OpenCode |
| `harness_cancel_pipeline` | pipeline_run_id | success/fail | Cancel running pipeline |
| `harness_get_agent_output` | agent_run_id | artifact content | Retrieve agent output |

### 3.6 Resilience Patterns

**Circuit Breaker (Polly):**
- Agent fails 3 times → circuit OPEN (5 min cooldown) → no dispatch
- After cooldown → HALF-OPEN → 1 test request → success → CLOSED

**Agent Pool (Dynamic Scaling):**
- Queue depth > 5 → scale up +1 instance (up to max_instances)
- Idle > 2 minutes → scale down to min_instances

**Backpressure:**
- Pipeline queue > 10 → reject new pipelines (HTTP 429)
- Agent queue > 20 → throttle dispatch

**Retry Policy:**
- Transient errors (network, timeout) → exponential backoff (1s, 2s, 4s)
- Logic errors (code bugs) → no retry, escalate to Loop Engineer
- Default max retries: 3

---

## 4. Phase 1: Loop Engineer

### 4.1 Role Definition

The Loop Engineer is an **autonomous fix agent** that:
1. Intercepts failed quality gates
2. Classifies the error
3. Generates a fix strategy with confidence scoring
4. Applies the fix directly (if confidence > 0.8) or escalates

### 4.2 Loop Flow

```
Agent Output ──▶ Quality Gate FAIL ──▶ Loop Engineer
     ▲                                      │
     │                               ┌──────▼──────┐
     │                               │ 1. ANALYZE   │
     │                               │ - Error type │
     │                               │ - Read logs  │
     │                               │ - Identify   │
     │                               │   file(s)    │
     │                               └──────┬───────┘
     │                                      ▼
     │                               ┌──────▼──────┐
     │                               │ 2. PLAN FIX  │
     │                               │ - Match      │
     │                               │   patterns   │
     │                               │ - Strategy   │
     │                               │ - Confidence │
     │                               └──────┬───────┘
     │                                      ▼
     │                               ┌──────▼──────┐
     │                          YES  │ CONFIDENCE  │  NO
     │                         ┌─────│   > 0.8?   │─────┐
     │                         │     └─────────────┘     │
     │                         ▼                         ▼
     │                   ┌──────────┐              ┌──────────┐
     │                   │ AUTOFIX  │              │ ESCALATE │
     │                   │ - Edit   │              │ to human │
     │                   │ - Build  │              │ or arch. │
     │                   │ - Test   │              └──────────┘
     │                   └────┬─────┘
     │                        ▼
     │                   ┌──────────┐
     │              YES  │ VERIFY?  │  NO
     │            ┌──────│ Passed?  │──────┐
     │            │      └──────────┘      │
     │            ▼                        ▼
     │      ┌──────────┐            ┌──────────┐
     │      │ RECORD   │       YES  │ LOOP < 3?│  NO
     │      │ in KB    │      ┌────│          │────┐
     │      │ CONTINUE │      │    └──────────┘    │
     │      └──────────┘      ▼                    ▼
     └─────────────────── LOOP BACK          ┌──────────┐
                                             │ MAX LOOPS│
                                             │ Give up, │
                                             │ escalate │
                                             └──────────┘
```

### 4.3 Error Classification

| Category | Description | Auto-fixable |
|---|---|---|
| `CompilationError` | Build failure, syntax error | ✅ High confidence |
| `TestFailure` | Unit/integration test failure | ✅ If pattern matched |
| `ContractViolation` | API contract mismatch, schema fail | ✅ If simple mismatch |
| `QualityGateFailure` | Security gate, migration safety, code style | ⚠️ Conditional |
| `InfrastructureError` | DB connection, K8s deploy fail, RabbitMQ | ❌ Escalate |
| `KnownGotcha` | Matched in knowledge base | ✅ High confidence |
| `LogicError` | Semantic bug | ❌ Escalate |
| `Unknown` | Cannot classify | ❌ Escalate immediately |

### 4.4 Confidence Scoring

```csharp
// Weighted scoring model
// Confidence = sum(signal_i × weight_i)

Signal 1: Error matches known pattern    → weight 0.4
Signal 2: Fix is small (<50 lines, <3 files) → weight 0.2 (small) / 0.1 (medium)
Signal 3: Previous similar fix succeeded → weight 0.2
Signal 4: Fix is reversible              → weight 0.1
Signal 5: Fix doesn't touch security/PHI → weight 0.1

Autofix threshold:  ≥ 0.8
Suggest threshold:  0.5–0.8
Escalate:           < 0.5
```

### 4.5 Guardrails (Hard Constraints)

**Never:**
- Edit files under `vault/`, `**/secrets/**`, `**/certificates/**`
- Modify migration files already deployed to production
- Bypass quality gates — only fix code to pass them
- Commit directly — always through Phase 5 orchestrator
- Change agent definitions in `opencode.json`

**Scope Limits:**
- Max 3 loop iterations per error
- Max 5 file edits per autofix cycle
- Max 200 lines changed per autofix cycle
- Only edit files in `src/`

**Rollback:**
- Every autofix is saved to artifact store before application
- 2 consecutive failed autofixes → auto-rollback + escalate

**Knowledge Capture:**
- Successful fixes → automatically added to `.capture/` for human review
- Pattern: `{error-signature} → {fix-strategy} → {success-rate}`

### 4.6 Agent Definition

```json
{
  "loop-engineer": {
    "description": "Autonomous fix agent. Analyzes failed quality gates, classifies errors, generates and applies fixes with confidence scoring. Escalates when below confidence threshold or max loops reached.",
    "model": "deepseek-v4-pro",
    "permission": "allow",
    "tools": ["filesystem-edit", "filesystem-read", "bash", "grep", "glob"],
    "mcp": ["filesystem", "db-identity", "db-patient", "db-appointment",
            "db-clinical", "db-lab", "db-billing", "db-pharmacy"],
    "loop_config": {
      "max_iterations": 3,
      "confidence_threshold": 0.8,
      "max_files_per_cycle": 5,
      "max_lines_per_cycle": 200,
      "timeout_seconds": 600
    },
    "safety_fences": {
      "readonly_paths": ["vault/", "**/secrets/**", "**/certificates/**"],
      "no_edit_paths": ["**/Migrations/*.cs", "opencode.json"],
      "scope_paths": ["src/"]
    }
  }
}
```

---

## 5. Phase 2: Enhanced Orchestrator

### 5.1 Conditional DAG

Instead of always running 5 phases linearly, the orchestrator analyzes the change scope and builds a dynamic DAG — skipping phases and agents that aren't triggered.

**Change Scope → Agent Mapping:**

| Change Path Pattern | Agents Triggered |
|---|---|
| `src/Backend/**/*.cs` | dotnet, dba (if migration needed) |
| `src/Frontend/**/*.ts` | angular |
| `k8s/**` | devops |
| `protos/**` | dotnet, angular |
| `cicd/**` | devops |
| `docs/**` | docs |
| `vault/**` | security |

**Phase Skip Logic:**

- No backend changes → skip testing-backend, dba
- No frontend changes → skip testing-frontend, check-ui
- No proto changes → skip contract validation
- No migration files → skip migration-safety gate
- Docs-only → skip implement, test phases entirely

### 5.2 Adaptive Loop Back

When a quality gate fails:
1. Loop Engineer analyzes failure
2. If autofix applied → pipeline loops back to implement phase with new context
3. Loop back maintains state (don't re-run already-passed gates)
4. Max 3 loop iterations; after that → escalate to human

### 5.3 Confidence-Based Decisions

| Pipeline Confidence | Action |
|---|---|
| > 0.9 | Auto-commit (trivial changes only) |
| 0.7 – 0.9 | Commit with PR for light human review |
| 0.5 – 0.7 | Create PR, require human approval |
| < 0.5 | Stop pipeline, escalate entirely |

### 5.4 Multi-Agent Consensus (Light)

Activated only for critical decisions: architecture changes, new services, security/auth changes, schema changes.
- Primary agent creates proposal
- Loop Engineer reviews + critiques
- If disagreement → spawn secondary agent with different approach
- Loop Engineer merges best parts or selects winner
- Cost: ~3x tokens; Benefit: higher quality

### 5.5 Time-Bounded Execution

| Phase | Default Timeout | On Timeout |
|---|---|---|
| Plan | 5 min | Escalate |
| Implement | 15 min per agent | Skip agent, continue |
| Test | 10 min per suite | Mark as failed |
| Validate | 5 min per gate | Skip gate (warn) / Fail phase (block) |
| Loop Engineer | 10 min per iteration | Escalate |
| **Pipeline total** | **60 min** | Graceful stop, commit partial results |

---

## 6. Phase 2-3: Workflow Framework

### 6.1 YAML DSL

```yaml
name: code-review
description: "Standard code review pipeline for backend changes"
version: "1.0"

triggers:
  paths: ["src/Backend/**/*.cs", "src/Shared/**/*.cs"]
  exclude_paths: ["**/Migrations/*.cs"]

pipeline:
  plan:
    agent: plan
    timeout: 5m

  implement:
    parallel:
      - agent: dotnet
        task: "Implement feature from plan"
        timeout: 15m
      - agent: dba
        task: "Generate migration if needed"
        condition:
          paths_changed: "**/Domain/**/*.cs"

  test:
    parallel:
      - agent: testing-backend
        depends_on: dotnet
      - agent: qa
        depends_on: [dotnet, dba]

  validate:
    parallel:
      - agent: validate
        depends_on: [testing-backend, qa]
        gates: [api-contract, fluent-validation, migration-safety, build-integrity]
      - agent: security
        depends_on: dotnet
        condition:
          paths_changed: "**/Auth/**"
        gates: [secrets-check, jwt-audit]

  loop:
    agent: loop-engineer
    on_failure: any_gate
    max_iterations: 3
    strategy: autofix_then_escalate

  commit:
    agent: git
    depends_on: [validate, security]
    mode: auto_pr
    require_confidence: 0.7
```

### 6.2 C# Fluent API

```csharp
public class MultiServiceDeploymentWorkflow : IWorkflowDefinition
{
    public void Configure(IWorkflowBuilder wf)
    {
        wf.Name("multi-service-deployment")
          .Description("Coordinated deployment across multiple microservices")
          .TriggerOn(paths: "protos/**", "src/Shared/**");

        wf.Plan(p => p.WithAgent("plan")
            .WithContext(ctx => ctx.IncludeServices("patient", "clinical", "appointment"))
            .Timeout(10, TimeUnit.Minutes));

        wf.Implement(imp =>
        {
            imp.AddAgent("dotnet-patient").WithPriority(Priority.High);
            imp.AddAgent("dotnet-clinical").DependsOn("dotnet-patient");
            imp.AddAgent("dotnet-appointment");
            imp.AddAgent("angular").DependsOn("dotnet-patient", "dotnet-clinical", "dotnet-appointment");
        });

        wf.Test(t =>
        {
            t.AddAgent("testing-backend-patient").DependsOn("dotnet-patient");
            t.AddAgent("testing-backend-clinical").DependsOn("dotnet-clinical");
            t.AddAgent("testing-backend-appointment").DependsOn("dotnet-appointment");
            t.AddAgent("qa").DependsOnAll();
        });

        wf.Validate(v =>
        {
            v.AddGate("api-contract", g => g.ForServices("patient", "clinical", "appointment")
                .WithBufBreakingChanges(true).Severity(GateSeverity.Block));
            v.AddGate("migration-safety", g => g.ForServices("patient", "clinical")
                .WithBackwardCompatible(true).Severity(GateSeverity.Block));
        });

        wf.Loop(l => l.WithAgent("loop-engineer")
            .OnFailure(g => g.Severity == GateSeverity.Block)
            .WithStrategy(s => s.AutoFix(0.8).ThenEscalate("deploy-blockers"))
            .MaxIterations(5));

        wf.Commit(c => c.WithMode(m => m
            .AutoCommitWhen(0.9).CreatePrWhen(0.7).EscalateWhen(0.0))
            .WithConsensus(cs => cs.Enabled(true).TriggerWhen(0.6).MinAgents(2)));
    }
}
```

### 6.3 Workflow Registry

Workflows are discovered automatically:
- YAML workflows loaded from `workflows/` directory
- C# workflows discovered via assembly scanning
- Same name: code workflow overrides YAML
- Auto-select: workflow with matching trigger scope → default fallback

### 6.4 Built-in Workflows

| Workflow | Trigger | Description |
|---|---|---|
| `default-full-pipeline` | (fallback) | Standard 5-phase pipeline |
| `code-review` | `src/Backend/**/*.cs` | Backend code changes |
| `frontend-change` | `src/Frontend/**/*.ts` | Frontend-only changes |
| `hotfix` | Manual trigger | Production hotfix — skip non-critical gates |
| `docs-only` | `docs/**` | Documentation — skip implement/test |
| `proto-change` | `protos/**` | gRPC contract changes |
| `migration` | `**/Migrations/*.cs` | Database migration — extra safety checks |
| `security-patch` | `vault/**`, `**/Auth/**` | Security fix — full security gates |
| `dependency-update` | `*.csproj`, `package.json` | Dependency update — rebuild + smoke test |
| `new-service` | New microservice directory | Full pipeline + consensus mode |
| `infra-change` | `k8s/**`, `cicd/**` | Infrastructure changes — DevOps focus |

---

## 7. Implementation Plan

### Phase 1 (Weeks 1-3): Harness Core + Loop Engineer

| Week | Milestone | Deliverables |
|---|---|---|
| **W1** | Harness Foundation | Project scaffold, Core domain models, DB schema + migrations, gRPC contracts, MCP server skeleton |
| **W2** | Harness Runtime | Pipeline DAG engine, Agent pool manager, Circuit breaker/retry/backpressure, OpenCode dispatcher integration, RabbitMQ event bus |
| **W3** | Loop Engineer | Error classifier, Fix planner, Confidence scorer, Autofix engine, Guardrails, Knowledge base integration, Agent definition |

**Verification:** Autofix success rate > 60% for known patterns.

### Phase 2 (Weeks 4-6): Enhanced Orchestrator + Workflow Framework

| Week | Milestone | Deliverables |
|---|---|---|
| **W4** | Conditional DAG | Change scope analyzer, DAG builder, Phase skip logic, Triggered agent detection |
| **W5** | Adaptive Pipeline | Loop-back engine, Re-planning, Confidence tracker, Time-bounded execution, Multi-agent consensus |
| **W6** | Workflow Framework | YAML DSL parser, C# Fluent API, Workflow registry, YAML→DAG compiler, 11 built-in workflows |

**Verification:** Pipeline skips unnecessary phases; loop back works end-to-end.

### Phase 3 (Weeks 7-8): Production Readiness

| Week | Focus |
|---|---|
| **W7** | Observability: Prometheus metrics, Grafana dashboards, Jaeger traces, Alert rules |
| **W8** | Documentation: ADRs, runbooks, dev guide, migration guide |

### Agent Assignments

| Phase | Agents |
|---|---|
| Phase 1 | @dotnet (Harness + Loop Engineer), @dba (Schema), @devops (MCP deploy, RabbitMQ), @docs (ADR) |
| Phase 2 | @dotnet (DAG + Adaptive + Workflow), @devops (CI/CD integration), @docs (DSL reference, migration guide) |
| Phase 3 | @devops (Observability), @qa (Chaos testing), @docs (Runbooks), @security (Audit guardrails) |

---

## 8. Success Criteria

| Criterion | Target | Measurement |
|---|---|---|
| Pipeline state survives session restart | 100% | Integration test |
| Unnecessary phases skipped correctly | > 95% accuracy | Change scope test suite |
| Loop Engineer autofix success (known patterns) | > 60% | Benchmark suite |
| Loop Engineer autofix success (all errors) | > 40% | Production monitoring |
| Pipeline confidence score correlates with actual quality | ρ > 0.7 | Historical analysis |
| Loop back reduces human intervention | > 50% reduction | Compare pre/post metrics |
| Workflow YAML can define complete pipeline | 100% feature parity | DSL test suite |
| Harness handles 10 concurrent pipelines | No degradation | Load test (k6) |
| Agent circuit breaker prevents cascade failure | 100% isolation | Chaos test (Chaos Mesh) |

---

## 9. Risks & Mitigations

| Risk | Likelihood | Impact | Mitigation |
|---|---|---|---|
| Loop Engineer applies wrong fix | Medium | High | Confidence threshold + guardrails + rollback |
| Harness becomes single point of failure | Low | Critical | Stateless design, CockroachDB persistence, multiple instances |
| OpenCode API changes break dispatch | Medium | Medium | Version-locked MCP protocol, contract tests |
| YAML DSL too limited for complex workflows | Medium | Medium | C# Fluent API escape hatch, YAML→C# compiler |
| Token costs increase with loop iterations | High | Medium | Max loop limit, confidence threshold, scope limits |
| Migration from current orchestrator to new | Medium | Medium | Phase 2 runs both in parallel, gradual cutover |

---

## 10. Open Questions

1. **Loop Engineer model:** Use `deepseek-v4-pro` (consistent with architect/plan) or a specialized fine-tuned model?
2. **Artifact storage:** CockroachDB BLOB columns or external object store (MinIO/S3)?
3. **Harness deployment:** Standalone K8s deployment or sidecar to OpenCode?
4. **Consensus mode cost:** Is the 3x token multiplier acceptable for critical workflows? Should it be opt-in only?

---

*End of design spec. Awaiting user review and approval before transitioning to implementation plan.*
