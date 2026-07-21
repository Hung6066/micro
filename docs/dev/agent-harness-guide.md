# Agent Harness & Loop Engineer — Developer Guide

> Version: 1.0 | Last updated: 2026-07-18 | Maintainer: @architect

---

## Table of Contents

1. [What Is the Agent Harness?](#1-what-is-the-agent-harness)
2. [How It Works](#2-how-it-works)
3. [MCP Tools Reference](#3-mcp-tools-reference)
4. [Loop Engineer](#4-loop-engineer)
5. [Workflows](#5-workflows)
6. [End-to-End Example](#6-end-to-end-example)
7. [Troubleshooting](#7-troubleshooting)
8. [FAQ](#8-faq)

---

## 1. What Is the Agent Harness?

The **Agent Harness** is a .NET 8 MCP server that provides **runtime orchestration** for the OpenCode agent system. It runs alongside OpenCode and manages:

- **Pipeline execution** — runs the 5-phase pipeline (Plan → Implement → Test → Validate → Commit) with state persistence
- **Loop Engineer** — automatically fixes failed quality gates without human intervention
- **Workflow engine** — selects the right workflow for each change (11 built-in workflows)
- **Resilience** — retries, circuit breakers, backpressure, timeouts

### When Do You Interact With It?

| Scenario | Interaction |
|----------|-------------|
| You submit a feature request | Harness starts a pipeline automatically via `@dispatcher` → `@orchestrator` |
| A test fails | Loop Engineer auto-fixes (if confident) or escalates to you |
| You want to check pipeline status | Call `harness_get_status` |
| You need to cancel a stuck pipeline | Call `harness_cancel_pipeline` |
| You're writing a custom workflow | Add a YAML file to `workflows/` |

### What Changed vs Before

| Before | Now |
|--------|-----|
| Pipeline state lived in LLM context only | State persisted in PostgreSQL (`harnessdb`) |
| All changes ran the same 5-phase pipeline | 11 workflows — only runs what's needed |
| Failed test = human must re-trigger | Loop Engineer auto-fixes (60%+ success rate) |
| No confidence tracking | Confidence scored per phase, gates gate commits |
| No pipeline history | Every run logged — queryable via `get_status` |

---

## 2. How It Works

### High-Level Flow

```
Your Feature Request
        │
        ▼
┌──────────────────┐
│ @dispatcher      │  Analyzes request scope, selects workflow
│ classifies scope │  PATH_DIRECT / PATH_LITE / PATH_FULL
└──────┬───────────┘
       │
       ▼
┌──────────────────┐
│ @orchestrator    │  Calls harness_start_pipeline
│ starts pipeline  │  Harness persists run to DB
└──────┬───────────┘
       │
       ▼
┌──────────────────────────────────────────────────────┐
│              Pipeline Engine (DAG)                    │
│                                                       │
│  Plan ──► Implement ──► Test ──► Validate ──► Commit │
│              │           │            │               │
│              ▼           ▼            ▼               │
│         If ANY gate fails: Loop Engineer activates    │
│         Auto-fix → loop back → re-test               │
│         Escalate if confidence < 0.8 or max loops     │
└──────────────────────────────────────────────────────┘
       │
       ▼
    Complete ✓ or Escalate ⚠
```

### Pipeline States

```
Pending ──► Running ──► Completed
  │            │
  └──► Cancelled    └──► Failed
```

---

## 3. MCP Tools Reference

The harness exposes MCP tools over HTTP at `localhost:5200` (Docker) or via OpenCode's native MCP integration. Current reachable tools include pipeline lifecycle, pending task polling, task completion, artifact storage, timeline retrieval, and human-in-the-loop approval operations.

### 3.1 `harness_start_pipeline`

Start a new pipeline execution.

**HTTP:** `POST /mcp/start-pipeline`

**Parameters:**

| Field | Type | Required | Default | Description |
|-------|------|----------|---------|-------------|
| `workflow_id` | string | **Yes** | — | One of: `default-full-pipeline`, `code-review`, `frontend-change`, `hotfix`, `docs-only`, `proto-change`, `migration`, `security-patch`, `dependency-update`, `new-service`, `infra-change` |
| `triggered_by` | string | No | `"system"` | Who/what triggered the pipeline |
| `params` | object | No | `{}` | Key-value parameters passed to workflow agents |

**Example:**

```bash
curl -X POST http://localhost:5200/mcp/start-pipeline \
  -H "Content-Type: application/json" \
  -d '{
    "workflow_id": "code-review",
    "triggered_by": "user",
    "params": {
      "branch": "feature/patient-search",
      "priority": "high"
    }
  }'
```

**Response:**

```json
{
  "pipeline_run_id": "1fc8584c-596e-4094-b029-e33a2a6bab01",
  "status": "Completed",
  "workflow_id": "code-review"
}
```

### 3.2 `harness_get_status`

Query the current state of a pipeline.

**HTTP:** `POST /mcp/get-status`

**Parameters:**

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `pipeline_run_id` | string (GUID) | **Yes** | UUID returned by `start_pipeline` |

**Example:**

```bash
curl -X POST http://localhost:5200/mcp/get-status \
  -H "Content-Type: application/json" \
  -d '{"pipeline_run_id": "1fc8584c-596e-4094-b029-e33a2a6bab01"}'
```

**Response:**

```json
{
  "pipeline_run_id": "1fc8584c-596e-4094-b029-e33a2a6bab01",
  "status": "Running",
  "workflow_id": "code-review",
  "started_at": "2026-07-18T04:50:00Z",
  "completed_at": null
}
```

**Error (not found):**

```json
{
  "error": "Pipeline run '00000000-0000-0000-0000-000000000000' not found."
}
```

### 3.3 `harness_dispatch_agent`

Dispatch a specific agent within a pipeline context. Usually called by the pipeline engine, but can be used manually.

**HTTP:** `POST /mcp/dispatch-agent`

**Parameters:**

| Field | Type | Required | Default | Description |
|-------|------|----------|---------|-------------|
| `pipeline_run_id` | string (GUID) | **Yes** | — | Parent pipeline run |
| `agent_name` | string | **Yes** | — | e.g., `dotnet`, `angular`, `dba` |
| `task_description` | string | **Yes** | — | What the agent should do |
| `max_retries` | int | No | 3 | Retry count on transient failures |
| `timeout` | int (seconds) | No | 600 | Agent execution timeout |

**Example:**

```bash
curl -X POST http://localhost:5200/mcp/dispatch-agent \
  -H "Content-Type: application/json" \
  -d '{
    "pipeline_run_id": "1fc8584c-596e-4094-b029-e33a2a6bab01",
    "agent_name": "dotnet",
    "task_description": "Implement patient search endpoint with CQRS pattern",
    "max_retries": 3,
    "timeout": 900
  }'
```

**Response:**

```json
{
  "agent_run_id": "a1b2c3d4-...",
  "status": "Running"
}
```

### 3.4 `harness_cancel_pipeline`

Cancel a running pipeline.

**HTTP:** `POST /mcp/cancel-pipeline`

**Parameters:**

| Field | Type | Required | Default | Description |
|-------|------|----------|---------|-------------|
| `pipeline_run_id` | string (GUID) | **Yes** | — | UUID of running pipeline |
| `reason` | string | No | `"Cancelled by user"` | Why it was cancelled |

**Example:**

```bash
curl -X POST http://localhost:5200/mcp/cancel-pipeline \
  -H "Content-Type: application/json" \
  -d '{
    "pipeline_run_id": "1fc8584c-596e-4094-b029-e33a2a6bab01",
    "reason": "Stuck in loop, manual fix needed"
  }'
```

**Response:**

```json
{
  "success": true,
  "message": "Pipeline '1fc8584c-596e-4094-b029-e33a2a6bab01' cancelled successfully."
}
```

### 3.5 Health Check

**HTTP:** `GET /health`

```bash
curl http://localhost:5200/health
# {"status":"healthy","service":"agent-harness"}
```

---

## 4. Loop Engineer

The **Loop Engineer** is an autonomous fix agent that activates when a quality gate fails during a pipeline. It analyzes the failure, generates a fix, and applies it — all without human intervention.

### When Does It Activate?

1. A pipeline phase completes (e.g., Implement → Test)
2. A quality gate evaluates the output and **fails** (e.g., test failure, contract violation)
3. If the workflow has a `loop` section with `on_failure: any_gate`, the Loop Engineer kicks in

### The Fix Cycle

```
Gate FAIL
   │
   ▼
┌──────────────┐
│ 1. ANALYZE   │  Reads error logs, identifies root cause
│              │  Classifies error type (see below)
└──────┬───────┘
       ▼
┌──────────────┐
│ 2. PLAN FIX  │  Generates fix strategy
│              │  Scores confidence (0.0 – 1.0)
└──────┬───────┘
       ▼
┌─────────────────────────────────────┐
│      Confidence > 0.8?              │
│  YES ───────────┐      ┌── NO       │
│                 ▼      ▼            │
│          ┌──────────┐               │
│          │ AUTOFIX  │  Escalates    │
│          │ - Edit   │  to you or    │
│          │ - Build  │  @architect   │
│          │ - Test   │               │
│          └────┬─────┘               │
│               ▼                     │
│          ┌──────────┐               │
│          │ VERIFY   │               │
│    YES ──│ Passed?  │── NO          │
│          └──────────┘               │
│               │                     │
│               ▼                     │
│    Loops back to ANALYZE            │
│    (max 3 iterations)               │
└─────────────────────────────────────┘
```

### Error Classification

| Category | Auto-fixable? | Examples |
|----------|---------------|----------|
| **CompilationError** | ✅ High | Build failure, syntax error, missing import |
| **TestFailure** | ✅ If known pattern | Unit test assertion failed, snapshot mismatch |
| **ContractViolation** | ✅ Simple mismatches | gRPC field renamed, protobuf type mismatch |
| **QualityGateFailure** | ⚠️ Conditional | Security gate, migration safety, code style |
| **InfrastructureError** | ❌ Escalate | DB connection, K8s deploy, RabbitMQ down |
| **KnownGotcha** | ✅ High | Matched from `docs/knowledge/` (known bugs) |
| **LogicError** | ❌ Escalate | Semantic bug, wrong algorithm |
| **Unknown** | ❌ Escalate | Cannot classify error |

### Confidence Scoring

The Loop Engineer calculates confidence using 5 weighted signals:

```
Signal 1: Error matches known pattern         weight 0.4
Signal 2: Fix is small (<50 lines, <3 files)  weight 0.2 (small) / 0.1 (medium)
Signal 3: Previous similar fix succeeded       weight 0.2
Signal 4: Fix is reversible                    weight 0.1
Signal 5: Fix doesn't touch security/PHI       weight 0.1
```

| Score | Action |
|-------|--------|
| ≥ 0.8 | Auto-fix applied automatically |
| 0.5 – 0.8 | Escalated with suggested fix |
| < 0.5 | Escalated to human immediately |

### What the Loop Engineer Can NOT Do

These are hard guardrails — the Loop Engineer will **never**:

- Edit files in `vault/`, `**/secrets/**`, `**/certificates/**`
- Modify migration files already deployed
- Bypass quality gates (only fixes code to pass them)
- Commit directly (always goes through Phase 5 `@git`)
- Change agent definitions in `opencode.json`
- Edit more than **5 files** or **200 lines** per cycle
- Run more than **3 loop iterations** per error

### How to Know the Loop Engineer Ran

Check the pipeline status:

```bash
curl -X POST http://localhost:5200/mcp/get-status \
  -H "Content-Type: application/json" \
  -d '{"pipeline_run_id": "1fc8584c-596e-4094-b029-e33a2a6bab01"}'
```

If the loop engineer intervened, you'll see extra `agent_runs` records with `agent_name: "loop-engineer"` and the fix results.

---

## 5. Workflows

The harness selects the right workflow based on what files changed. Each workflow defines which agents run, in what order, and with what quality gates.

### 5.1 Built-in Workflows

| Workflow | Trigger | Description |
|----------|---------|-------------|
| `default-full-pipeline` | Any `src/` change (fallback) | Standard 5-phase pipeline |
| `code-review` | `src/Backend/**/*.cs` | Backend code changes |
| `frontend-change` | `src/Frontend/**/*.ts` | Frontend-only changes |
| `hotfix` | Manual trigger only | Production hotfix — skip non-critical gates |
| `docs-only` | `docs/**` | Docs change — skip implement/test |
| `proto-change` | `protos/**` | gRPC contract changes |
| `migration` | `**/Migrations/*.cs` | DB migration — extra safety checks |
| `security-patch` | `vault/**`, `**/Auth/**` | Security fix — full security gates |
| `dependency-update` | `*.csproj`, `package.json` | Dependency bump — rebuild + smoke test |
| `new-service` | New microservice directory | Full pipeline + consensus mode |
| `infra-change` | `k8s/**`, `cicd/**` | Infrastructure changes — DevOps focus |

### 5.2 Workflow Selection Priority

1. **Manual override** — If `workflow_id` is passed explicitly to `start_pipeline`, use it
2. **Most specific match** — Check changed files against each workflow's `triggers.paths`
3. **Fallback** — `default-full-pipeline`

### 5.3 Creating a Custom Workflow

Create a YAML file in `src/Infrastructure/AgentHarness/workflows/`.

**Example — Database-only migration workflow:**

```yaml
name: migration
description: "Database migration — extra safety checks"
version: "1.0"
triggers:
  paths: ["**/Migrations/*.cs"]

pipeline:
  plan:
    - agent: dba
      task: "Review migration changes"
      timeout_minutes: 5

  implement:
    - agent: dotnet
      depends_on: [dba]
      task: "Apply migration and update entities"

  test:
    - agent: testing-backend
      depends_on: [dotnet]
      gates: ["integration-tests"]

  validate:
    - agent: validate
      depends_on: [testing-backend]
      gates: ["migration-safety"]

  commit:
    agent: git
    depends_on: [validate]
    mode: create_pr
    require_confidence: 0.8

loop:
  agent: loop-engineer
  max_iterations: 3
  strategy: autofix_then_escalate
  on_failure: any_gate
```

**Key YAML fields:**

| Field | Description |
|-------|-------------|
| `name` | Unique workflow identifier — used as `workflow_id` |
| `triggers.paths` | Glob patterns that activate this workflow |
| `triggers.exclude` | Glob patterns to exclude |
| `pipeline.<phase>.<agent>` | Agents to run in each phase |
| `pipeline.<phase>.parallel` | Run multiple agents concurrently |
| `depends_on` | Wait for these agents before starting |
| `gates` | Quality gates to evaluate after output |
| `loop` | Loop Engineer configuration |
| `commit.mode` | `direct_commit`, `auto_pr`, `create_pr` |

---

## 6. End-to-End Example

Here's what happens when you submit a backend change request:

### Step 1: You Submit a Request

```
User: "Add patient search endpoint with pagination"
```

### Step 2: @dispatcher Classifies

```yaml
scope: backend, moderate complexity
path: PATH_LITE
agents: [@dotnet, @testing-backend, @validate]
workflow: code-review
```

### Step 3: Harness Starts Pipeline

```
harness_start_pipeline
  workflow_id: "code-review"
  params: { feature: "patient-search", scope: "backend" }
```

### Step 4: DAG Execution

```
┌──────────────────────┐
│ PLAN                 │
│ @explore analyzes    │  Creates implementation plan
│ scope + generates    │
│ plan                 │
└──────────┬───────────┘
           ▼
┌──────────────────────┐
│ IMPLEMENT            │
│ @dotnet writes:      │
│ - PatientSearchQuery │
│ - PatientSearchDto   │
│ - Controller endpoint│
└──────────┬───────────┘
           ▼
┌──────────────────────┐
│ TEST                 │
│ @testing-backend     │  Unit tests fail!
│ runs xUnit suite     │  "SearchQuery_ReturnsPagedResults failed"
└──────────┬───────────┘
           ▼
┌────────────────────────────────────┐
│ LOOP ENGINEER ACTIVATES            │
│                                    │
│ 1. ANALYZE:                        │
│    "Assert.AreEqual(10, results.Count)"  │
│    → Actual: 8, Expected: 10       │
│    → Error: TestFailure             │
│    → File: PatientSearchQueryTests.cs│
│                                    │
│ 2. PLAN:                           │
│    "Pagination: change expected     │
│     count from 10 to pageSize"     │
│    → Confidence: 0.92              │
│                                    │
│ 3. AUTOFIX:                        │
│    Edit: PatientSearchQueryTests.cs│
│    "Assert.AreEqual(pageSize, ...)"│
│                                    │
│ 4. VERIFY:                         │
│    Re-run tests → PASS ✓           │
└────────────────────────────────────┘
           ▼
┌──────────────────────┐
│ VALIDATE             │
│ @validate checks:    │
│ - Build integrity    │  PASS
│ - API contract       │  PASS
│ - Migration safety   │  N/A (no migrations)
│ - Secrets check      │  PASS
└──────────┬───────────┘
           ▼
┌──────────────────────┐
│ COMMIT               │
│ @git creates PR      │
│ Title: "Add patient  │
│ search endpoint"     │
└──────────────────────┘
```

### Step 5: You See the Result

```
✅ Pipeline completed
  workflow: code-review
  run_id: 1fc8584c-...
  status: Completed
  artifacts: PR #142
  loop_engineer: 1 fix applied (test assertion corrected)
```

---

## 7. Troubleshooting

### 7.1 Pipeline Stuck in "Running" State

**Check status:**

```bash
curl -X POST http://localhost:5200/mcp/get-status \
  -d '{"pipeline_run_id": "..."}'
```

**If stuck > 5 minutes:**

```bash
curl -X POST http://localhost:5200/mcp/cancel-pipeline \
  -d '{"pipeline_run_id": "...", "reason": "Stuck > 5 min"}'
```

Then re-trigger.

### 7.2 Loop Engineer Escalates Too Often

The Loop Engineer escalates when it can't confidently fix the issue. Common causes:

| Cause | Fix |
|-------|-----|
| Error is a new pattern not in knowledge base | Add pattern to `docs/knowledge/` |
| Fix touches guarded files (secrets, migrations) | Review escalated fix manually |
| Change is too large (> 5 files or > 200 lines) | Break into smaller changes |

Check `docker logs his-hope-agentharness | grep "EscalationReason"` for details.

### 7.3 Wrong Workflow Selected

If the wrong workflow is triggered:

1. Check which files changed — does the trigger pattern match?
2. Add `exclude` paths to the workflow YAML if needed
3. If manual, explicitly pass `workflow_id` to `start_pipeline`

### 7.4 Service Not Responding

```bash
# Check if container is running
docker ps | grep agentharness

# Check health
curl http://localhost:5200/health

# Check logs
docker logs his-hope-agentharness --tail 50
```

### 7.5 Pipeline Started but No Agents Ran

The harness handles orchestration; agents are still dispatched via OpenCode's `task` tool. If no agents ran:

1. Check `harness_get_status` — what status is the pipeline?
2. Check OpenCode logs — were the task dispatches received?
3. Check harness logs — `docker logs his-hope-agentharness | grep DispatchAgent`

---

## 8. FAQ

**Q: Do I need to interact with the harness directly?**
A: Usually no. The `@dispatcher` and `@orchestrator` agents call the harness for you. You only need direct HTTP calls for debugging or manual cancellation.

**Q: Where is pipeline state stored?**
A: In PostgreSQL database `harnessdb` on Docker (port 5433). The `harness` schema contains tables: `pipeline_runs`, `agent_runs`, `quality_gate_results`, `artifacts`.

**Q: Can I re-run a failed pipeline?**
A: Yes — start a new pipeline with the same `workflow_id`. Each run gets a unique `pipeline_run_id`.

**Q: Does the Loop Engineer create git commits?**
A: No. It edits files directly but **never commits**. Only `@git` (Phase 5) commits after all gates pass.

**Q: How do I add a new error pattern for the Loop Engineer?**
A: Add it to `docs/knowledge/` with the format `{error-signature} → {fix-strategy} → {success-rate}`. The knowledge index is checked at `docs/knowledge/INDEX.md`.

**Q: What happens if RabbitMQ is down?**
A: The harness falls back to `NullEventBus` — events are logged but not published. The pipeline continues but with degraded event tracking. You'll see "Event bus initialized: NullEventBus" in logs.

**Q: Can I run the harness without Docker?**
A: Yes — you need PostgreSQL on port 5433 and RabbitMQ on port 5672. Run:

```bash
dotnet run --project src/Infrastructure/AgentHarness/src/AgentHarness.Mcp
```

The default config in `McpServerConfig.cs` points to localhost.

---

*For operations-specific concerns (deployments, scaling, monitoring), see the [Operations Runbook](agent-harness-runbook.md).*
*For design decisions, see [ADR-016](../architecture-decisions/adr-016-agent-harness.md).*
*For the full design spec, see [Design Spec](../../docs/superpowers/specs/2026-07-18-agent-harness-loop-engineer-design.md).*
