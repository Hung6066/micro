# Agent Intelligence Platform Design

Date: 2026-07-20

## Goal
Add a lightweight agent intelligence layer on top of the existing .NET Agent Harness so we can:

1. measure how well an agent is performing,
2. benchmark agents and model choices with evals,
3. learn from successful fixes, and
4. adapt quality gates based on historical performance.

This is an additive design. It does not replace the current pipeline engine, loop engineer, or instinct tools.

## Scope

### Phase 1 — Agent Intelligence Metrics
Add an `AgentMetricsService` that derives an Agent Intelligence Score (AIS) from existing data:
- `AgentRun`
- `QualityGate`
- `PipelineRun`
- `MemoryEntry`

Expose the metrics through:
- Prometheus metrics
- a new MCP tool: `get-agent-profile`

### Phase 2 — Eval Engine
Add an `EvalEngineService` that runs eval suites programmatically and records results.

Expose:
- `evaluate-agent`
- `compare-models`

Persist:
- `harness.eval_suites`
- `harness.eval_runs`

### Phase 3 — Learning Loop
Add an `InstinctOptimizer` that boosts, decays, and merges instincts based on use and outcome.

Add one hook:
- successful Loop Engineer fixes should auto-record instincts.

### Phase 4 — Adaptive Quality Gates
Add `AdaptiveQualityGates` that recommends gate thresholds from AIS trends.
Add a predictive gate that warns when a task is likely to fail before execution.

## Non-goals
- No new external service for analytics.
- No replacement of the current pipeline engine.
- No autonomous gate bypassing.
- No production model training system.
- No UI redesign.

## Proposed Architecture

### Core services

#### 1) `AgentMetricsService`
Responsibilities:
- compute AIS per agent,
- aggregate success, retry, duration, and gate-pass data,
- correlate confidence with actual outcomes,
- emit Prometheus metrics.

AIS should be a weighted composite of:
- task completion rate,
- quality gate pass rate,
- retry / intervention rate,
- confidence accuracy,
- instinct reuse effectiveness,
- judge score average.

#### 2) `EvalEngineService`
Responsibilities:
- load eval suites,
- execute eval cases,
- grade outputs using deterministic or LLM-backed graders,
- compute pass@1 / pass@k,
- store results and compare against baselines.

#### 3) `InstinctOptimizer`
Responsibilities:
- increase confidence for instincts that are used successfully,
- decay stale instincts,
- merge duplicates when patterns overlap heavily,
- optionally trigger a record when Loop Engineer applies a successful fix.

#### 4) `AdaptiveQualityGates`
Responsibilities:
- read AIS trends and recent eval history,
- recommend threshold adjustments,
- flag high-risk tasks before dispatch.

## Data Model

### Existing data used
- `AgentRun`: status, confidence, duration, retries, artifact ref
- `QualityGate`: pass/fail and gate metadata
- `PipelineRun`: workflow, outcome, timings
- `MemoryEntry`: error pattern, fix description, use count

### New tables

#### `harness.eval_suites`
Stores reusable eval definitions.
- `id`
- `name`
- `domain`
- `description`
- `definition_json`
- `created_at`
- `updated_at`

#### `harness.eval_runs`
Stores each execution of an eval suite.
- `id`
- `eval_suite_id`
- `target_agent`
- `target_model`
- `pass_at_1`
- `pass_at_k`
- `judge_score`
- `status`
- `started_at`
- `completed_at`
- `raw_result_json`

## MCP Tools

### Phase 1
- `get-agent-profile`

### Phase 2
- `evaluate-agent`
- `compare-models`

### Phase 3
- no new public MCP tool required; learning happens through the existing instinct workflow and Loop Engineer hook.

### Phase 4
- no new public MCP tool required initially; adaptive gates stay internal until proven stable.

## Metrics and Scoring

### AIS
AIS is a 0–100 score computed from existing signals. Initial implementation should use a simple weighted formula so it is easy to explain and test.

Recommended signals:
- completion rate
- first-pass gate rate
- retry rate
- confidence accuracy
- instinct reuse rate
- judge score

### pass@k
Use standard eval-harness meaning:
- pass@1 = first attempt success rate
- pass@k = at least one success in k attempts

## Behavior Rules

1. Metrics must be derived from existing persisted records; do not invent new runtime telemetry paths when the data already exists.
2. Eval results must be reproducible from stored suite definitions.
3. Learning must not auto-apply destructive changes.
4. Adaptive gates may recommend thresholds, but human review remains required for critical paths.
5. Security-sensitive tasks must continue to use redaction and existing guardrails.

## Implementation Phases

### Phase 1
- Add `AgentMetricsService`.
- Add `get-agent-profile` MCP tool.
- Add Prometheus metrics for AIS and related components.

### Phase 2
- Add eval schema and migration.
- Add `EvalEngineService`.
- Add `evaluate-agent` and `compare-models` MCP tools.

### Phase 3
- Add `InstinctOptimizer`.
- Wire Loop Engineer success path to auto-record instincts.

### Phase 4
- Add adaptive gate recommendations.
- Add predictive failure warning logic.

## Testing Strategy

### Phase 1 tests
- unit tests for AIS scoring,
- integration tests for `get-agent-profile`,
- Prometheus metric emission checks.

### Phase 2 tests
- eval suite execution tests,
- pass@k calculation tests,
- migration verification for new eval tables,
- model comparison tests.

### Phase 3 tests
- instinct boost / decay tests,
- merge-dedup tests,
- successful Loop Engineer fix records instinct automatically.

### Phase 4 tests
- threshold recommendation tests,
- predictive warning tests,
- ensure no gate bypass occurs automatically.

## Acceptance Criteria

- AIS can be queried for a specific agent and reflects persisted history.
- Eval suites can be executed and saved with pass@k results.
- Successful Loop Engineer fixes create or update instincts automatically.
- Adaptive gate logic can recommend stricter or looser thresholds based on trend.
- All new behavior is covered by tests and does not break existing harness execution.

## Out of Scope for This Iteration
- distributed analytics warehouse,
- fine-tuning or training new models,
- new UI for metrics dashboards,
- changing the existing orchestration model.
