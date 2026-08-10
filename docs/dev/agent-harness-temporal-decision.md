# Agent Harness Temporal Decision

> Date: 2026-07-19
> Scope: `src/Infrastructure/AgentHarness`

## Decision

Keep the local polling `PipelineEngine` as the production default and keep Temporal as an opt-in durable execution backend behind `AgentHarness__UseTemporal=true`.

## Rationale

- The current callback execution model depends on external OpenCode agents polling `get-pending-tasks` and completing work through `complete-task`.
- The local engine is already integrated with checkpointing, quality gates, HITL, and existing MCP tools.
- The Temporal worker builds, but the repo does not yet include production Kubernetes manifests, SLO dashboards, or load-test evidence proving Temporal should become the default.
- Removing Temporal now would discard a useful durable-execution path before benchmark data exists.

## Production rule

Temporal must remain disabled unless all of the following are true:

1. `AgentHarness__UseTemporal=true` is explicitly set.
2. A Temporal server and `AgentHarness.TemporalWorker` deployment are installed in the target environment.
3. A load/crash-recovery test has verified workflow resumption, activity heartbeats, and external-agent callback semantics.
4. Monitoring includes Temporal workflow/activity failure alerts.

## Follow-up to make Temporal the default

- Add Kubernetes manifests for Temporal server/worker or reference a managed Temporal Cloud namespace.
- Add a k6 or xUnit integration test that starts a pipeline, kills the harness process, and verifies workflow recovery.
- Add dashboard panels for workflow latency, activity retries, and stuck workflows.
- After the above passes, flip `AgentHarness__UseTemporal=true` in production config.
