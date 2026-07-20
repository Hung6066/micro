---
name: continuous-learning-v2
description: Instinct-based learning for His.Hope agents. After every fix, record the pattern. Before starting work, query past instincts. Patterns auto-cluster into reusable skills over time.
metadata:
  origin: ECC (imported), adapted for His.Hope harness
---

# Continuous Learning v2 — Instinct-Based Learning

Automatically learns from every development session. After a bug fix or pattern discovery, **record the instinct**. Before starting work on a similar issue, **query past instincts**. The system clusters related instincts into reusable skills over time.

## How It Works

```
┌─────────────────────────────────────────────────────────┐
│  SESSION FLOW                                           │
│                                                         │
│  Session Start → Query Similar Instincts                │
│       │                                                 │
│       ▼                                                 │
│  Work on Feature / Fix Bug                              │
│       │                                                 │
│       ▼                                                 │
│  [On Success] Record Instinct                           │
│       │    ┌─ Pattern: "What error/issue was solved"    │
│       │    ├─ Category: "build|runtime|test|security"   │
│       │    ├─ Fix: "What was the solution"              │
│       │    └─ Agent: "dotnet|angular|qa|..."            │
│       │                                                 │
│       ▼                                                 │
│  Instinct stored in CockroachDB with vector embedding   │
│  → Automatically matched against future similar issues  │
└─────────────────────────────────────────────────────────┘
```

## MCP Tools

These tools are exposed by the Agent Harness MCP server (port 5200):

### `record-instinct`
Save a learned pattern after a successful fix.

**Parameters:**
- `agent_name` (string, required) — Which agent learned this (e.g., "dotnet", "angular")
- `error_pattern` (string, required) — What error or issue was encountered
- `error_category` (string, required) — Category: "build" | "runtime" | "test" | "security" | "config" | "migration" | "other"
- `fix_description` (string, required) — How the issue was resolved
- `fix_artifact_ref` (string, optional) — Link to the fix commit, PR, or file
- `confidence` (number, optional) — 0.0 to 1.0 (default 0.85)

**Returns:** `{ instinct_id, status }`

### `query-instincts`
Search for past instincts similar to the current issue.

**Parameters:**
- `error_pattern` (string, required) — The error or issue description to match
- `agent_name` (string, optional) — Filter by agent type
- `min_confidence` (number, optional) — Minimum similarity (0.0-1.0, default 0.3)

**Returns:** `{ results: [{ instinct_id, error_pattern, fix_description, confidence, use_count, agent_name }] }`

## When to Record Instincts

Record an instinct whenever you:
- **Fix a build error** — Record the error message and how you fixed it
- **Resolve a runtime exception** — Record the stack trace pattern and fix
- **Fix a failing test** — Record the test failure and resolution
- **Patch a security vulnerability** — Record the CVE or issue pattern
- **Solve a configuration problem** — Record the config issue and solution
- **Complete a complex migration** — Record the migration pattern
- **Discover a project-specific gotcha** — Record the nuance for future agents

## When to Query Instincts

Query before starting any work that matches these signals:
- User reports an error you've seen before
- Build fails with an unfamiliar error
- Tests fail mysteriously
- You're about to implement something the Loop Engineer might have seen

## Workflow for Agents

### After a Successful Fix
```markdown
1. Identify the root error pattern (be specific, include error codes)
2. Call `record-instinct` with:
   - agent_name: [your agent name]
   - error_pattern: [specific error message or pattern]
   - error_category: [build|runtime|test|security|config|migration]
   - fix_description: [what you did to fix it]
   - fix_artifact_ref: [optional commit/file reference]
3. Acknowledge: "Instinct recorded. Future agents will benefit from this fix."
```

### Before Starting Work
```markdown
1. Query past instincts with `query-instincts`
2. Check if similar issues were solved before
3. If a high-confidence match exists, apply the known fix
4. If no match, proceed with normal debugging
```

## Confidence Scoring

Instincts gain confidence over time through repeated use:

| Use Count | Confidence Boost | Behavior |
|-----------|-----------------|----------|
| 1 hit | 0.70 base | Tentative — verify before applying |
| 2-3 hits | 0.80 | Reliable — apply with confidence |
| 4+ hits | 0.90+ | Highly reliable — auto-apply |

Instincts with < 0.30 similarity are not returned by queries.

## Instinct Lifecycle

```
Created (first record)
    │
    ▼
Matured (used 3+ times, confidence ≥ 0.80)
    │
    ▼
Promoted (appears in 2+ agent types → cross-domain knowledge)
    │
    ▼
Evolved (clustered with related instincts → reusable skill)
```

## Evolution (Manual)

Periodically, cluster related instincts into reusable skills:

```bash
# List all instincts by category
agent-harness: query-instincts -c "build" -all

# Identify top patterns
# Create a skill if 3+ instincts share a common pattern
# Promote cross-cutting instincts to CLAUDE.md or docs/knowledge/
```

## Integration with @orchestrator

The orchestrator pipeline automatically:
1. **Phase 3 (Test)**: If tests fail → record the failure pattern
2. **Loop Engineer**: Automatically queries instincts when fixing failed gates
3. **Phase 5 (Commit)**: Records the final fix pattern as an instinct

No manual intervention needed — the harness's built-in LoopEngineer service handles this.

## Example

```markdown
## Agent A encounters build error:
> error CS0246: The type or namespace name 'MediatR' could not be found

## Agent A queries instincts:
→ Returns: "Missing MediatR NuGet package — run 'dotnet add package MediatR'"
  (confidence: 0.92, used 7 times, learned by: dotnet)

## Agent A applies fix:
dotnet add package MediatR

## Agent A records:
record-instinct(agent:"dotnet", pattern:"CS0246 MediatR not found",
                category:"build", fix:"dotnet add package MediatR",
                confidence:0.92)

## Next time any agent hits CS0246 MediatR:
→ Auto-suggested without re-debugging!
```

## Why This Matters for His.Hope

In a hospital information system with 7 microservices, 15 MCP servers, and specialized agents:
- **@dotnet** learns from .NET build errors, EF Core migration issues
- **@angular** learns from Angular compilation errors, NgRx state issues
- **@dba** learns from CockroachDB migration problems, query performance
- **@devops** learns from Kubernetes deployment errors, Linkerd config issues
- **@qa** learns from test infrastructure flakiness, Playwright issues

Instincts are shared across all agents via the harness's CockroachDB-backed memory store.
