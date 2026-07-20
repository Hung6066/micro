---
name: autonomous-loops
description: "Patterns and architectures for autonomous His.Hope development loops — from sequential agent pipelines to multi-agent DAG orchestration for complex features."
metadata:
  origin: ECC (imported)
---

# Autonomous Loops Skill

Patterns, architectures, and reference implementations for running autonomous development loops. From simple sequential agent calls to full DAG orchestration for hospital information system features.

## When to Use

- Setting up autonomous development workflows that run without human intervention
- Choosing the right loop architecture for your problem (simple vs complex)
- Building CI/CD-style continuous development pipelines
- Running parallel agents with merge coordination
- Implementing quality gates across loop iterations
- Adding cleanup passes to autonomous workflows

## Loop Pattern Spectrum

| Pattern | Complexity | Best For |
|---------|-----------|----------|
| Sequential Pipeline | Low | Daily dev steps, scripted workflows |
| Dispatcher-Orchestrator | Low | His.Hope's native pipeline (@dispatcher → @orchestrator) |
| DAG Orchestration | High | Large features across multiple microservices |

---

## 1. Sequential Pipeline

The simplest loop. Break a task into a sequence of focused steps. Each step is a fresh agent context.

```bash
# daily-dev.sh — Sequential pipeline for a microservice feature

set -e

# Step 1: Plan
task(explore, "Read the feature spec. Analyze current codebase. Return a plan.")

# Step 2: Implement Backend
task(dotnet, "Implement the feature following the plan. Write tests first.")

# Step 3: Implement Frontend
task(angular, "Implement the UI for this feature following the plan.")

# Step 4: Verify
task(qa, "Run full test suite. Check coverage. Report any failures.")

# Step 5: Commit
task(git, "Create conventional commit for all changes.")
```

### Key Design Principles

1. **Each step is isolated** — Fresh context per agent call means no context bleed
2. **Order matters** — Steps execute sequentially. Each builds on the filesystem state left by the previous
3. **Exit codes propagate** — Failures stop the pipeline

### With Agent Routing
```bash
# Research with explore (fast)
task(explore, "Analyze the codebase architecture and write a plan for adding patient notes...")

# Implement with dotnet (domain expert)
task(dotnet, "Implement the patient notes service according to the plan...")

# Review with qa (thorough)
task(qa, "Review all changes for security issues, HIPAA compliance, and edge cases...")
```

---

## 2. Dispatcher-Orchestrator Pipeline

**His.Hope's native pattern.** Uses @dispatcher for classification and @orchestrator for multi-phase execution.

```
User Request → @dispatcher
    │
    ├── PATH_DIRECT (trivial) → @dotnet or @angular → @validate → @git
    │
    ├── PATH_LITE (medium) → @architect delegates → implement → test → validate → git
    │
    └── PATH_FULL (complex) → @orchestrator 5-phase pipeline
         Phase 1: @explore → plan
         Phase 2: @dotnet + @angular (parallel)
         Phase 3: @qa + @testing-backend + @testing-frontend
         Phase 4: @validate + @check-ui + @security
         Phase 5: @git commit
```

### When to Use

| Signal | Path | Model |
|--------|------|-------|
| Single file typo fix | DIRECT | @dotnet or @angular |
| New API endpoint, one service | LITE | @dotnet → @validate → @git |
| New feature across frontend + backend | FULL | @orchestrator 5-phase |
| New microservice | FULL | @orchestrator + @devops + @dba |
| Security patch | FULL | @orchestrator + @security |

---

## 3. DAG Orchestration (Complex Features)

For large features that span multiple microservices with dependencies between them.

### Architecture

```
Feature Spec / RFC
       │
       ▼
  DECOMPOSITION
  Break into work units with dependency DAG
       │
       ▼
┌──────────────────────────────────────────────┐
│  PARALLEL EXECUTION BY DAG LAYER             │
│                                              │
│  Layer 0 (no deps):                          │
│  ┌────────────────┐ ┌────────────────┐       │
│  │ @dotnet: Patient│ │ @dotnet: Billing│      │
│  │ API (no deps)   │ │ Schema (no deps)│      │
│  └────────────────┘ └────────────────┘       │
│                                              │
│  Layer 1 (depends on Layer 0):              │
│  ┌──────────────────────────────────┐        │
│  │ @angular: Patient UI             │        │
│  │ Depends on: Patient API          │        │
│  └──────────────────────────────────┘        │
│                                              │
│  Layer 2 (depends on Layer 1):              │
│  ┌──────────────────────────────────┐        │
│  │ @qa: Integration Tests           │        │
│  └──────────────────────────────────┘        │
└──────────────────────────────────────────────┘
```

### Complexity Tiers

| Tier | Pipeline Stages | Model |
|------|----------------|-------|
| **trivial** | implement → test | gpt-5.4-mini |
| **small** | implement → test → validate | gpt-5.4-mini |
| **medium** | plan → implement → test → validate → security | gpt-5.4-mini + deepseek-v4-pro |
| **large** | explore → plan → implement → test → validate → security → git | full pipeline |

### Merge Queue with Conflict Handling

When parallel agents produce overlapping changes:

```
Agent branch
    │
    ├─ Rebase onto main
    │   └─ Conflict? → Capture conflict context, fix in next pass
    │
    ├─ Run build + tests
    │   └─ Fail? → Capture test output, fix in next pass
    │
    └─ Pass → Merge, push, delete branch
```

---

## Anti-Patterns

1. **No context bridge** — Each fresh agent call has no memory of previous steps
2. **Retrying the same failure** — If a step fails, capture error context before retrying
3. **All agents in one context window** — For complex workflows, separate concerns
4. **No merge strategy for parallel work** — If two agents might edit the same file, plan sequential landing

## Integration with @orchestrator

The @orchestrator pipeline implements the DAG pattern natively:
- Phase dependencies are tracked
- Parallel agent dispatch when possible
- Quality gates at each phase boundary
- Loop engineer for failed gates
