# Agent Harness — Operations Runbook

> Version: 1.0 | Last updated: 2026-07-18 | Maintainer: @devops

---

## Table of Contents

1. [Architecture Overview](#1-architecture-overview)
2. [Starting and Stopping](#2-starting-and-stopping)
3. [Monitoring Dashboards](#3-monitoring-dashboards)
4. [Common Issues](#4-common-issues)
5. [Database Backup and Restore](#5-database-backup-and-restore)
6. [Configuration Reference](#6-configuration-reference)

---

## 1. Architecture Overview

The Agent Harness is a .NET 8 ASP.NET Core MCP server that provides stateful pipeline orchestration for the His.Hope agent system. It runs as a Kubernetes deployment in the `hishop` namespace.

### Component Diagram

```
┌──────────────┐     MCP/HTTP      ┌──────────────────┐
│   OpenCode   │ ◄──────────────► │ AgentHarness.Mcp  │
│  (MCP Client)│    port 5200     │   (MCP Server)    │
└──────────────┘                   └────────┬─────────┘
                                            │
                    ┌───────────────────────┼───────────────────────┐
                    │                       │                       │
                    ▼                       ▼                       ▼
            ┌──────────────┐       ┌──────────────┐       ┌──────────────┐
            │ PostgreSQL + │       │   RabbitMQ   │       │  OpenCode    │
            │   pgvector   │       │  (Events)    │       │  (Agents)    │
            │  (State)     │       │  port 5672   │       │  (poll MCP)  │
            └──────────────┘       └──────────────┘       └──────────────┘
```

### Key Components

| Component | Description |
|-----------|-------------|
| **Mcp** | MCP/HTTP tool contracts exposed to OpenCode: pipeline lifecycle, pending-task polling, task completion, artifact storage, timeline, and HITL approvals |
| **Application** | CQRS handlers, pipeline engine, loop engineer, backpressure controller, change scope analyzer |
| **Core** | Domain models: `PipelineRun`, `AgentRun`, `PipelineDag`, `QualityGate`, events |
| **Infrastructure** | PostgreSQL/pgvector persistence, RabbitMQ event bus, OpenCode callback dispatcher, OpenTelemetry metrics |

### State Machine

```
Pending ──► Running ──► Completed
  │            │
  └──► Cancelled    └──► Failed
```

### Data Flow

1. OpenCode calls MCP tool → `AgentHarness.Mcp` receives request
2. Request validated by FluentValidation → routed via MediatR to handler
3. Handler creates/updates domain model → persisted to PostgreSQL/pgvector
4. Events published to RabbitMQ for async processing
5. Agent run is persisted as `Running`; external OpenCode agents poll `get-pending-tasks` and report through `complete-task`
6. Metrics emitted via OpenTelemetry to Prometheus

---

## 2. Starting and Stopping

### Local Development (Docker Compose)

```bash
# Start harness with dependencies
docker compose -f docker/docker-compose.yml up -d agentharness postgres rabbitmq

# Check logs
docker compose logs -f agentharness

# Stop
docker compose down
```

### Kubernetes

```bash
# Deploy
kubectl apply -f k8s/agent-harness/

# Check status
kubectl -n hishope get pods -l app=agent-harness
kubectl -n hishope rollout status deployment/agent-harness

# Restart (without downtime due to 2 replicas)
kubectl -n hishope rollout restart deployment/agent-harness

# Scale
kubectl -n hishope scale deployment/agent-harness --replicas=3

# Check logs
kubectl -n hishope logs -l app=agent-harness -f

# Stop (scale to 0)
kubectl -n hishope scale deployment/agent-harness --replicas=0

# Remove entirely
kubectl delete -f k8s/agent-harness/
```

### Health Checks

```
GET /health          → Liveness: returns 200 OK
GET /health/ready    → Readiness: returns 200 once the harness process is ready
GET /health/startup  → Startup: returns 200 once the harness process has started
```

---

## 3. Monitoring Dashboards

### Grafana Dashboard

The **Agent Harness — Pipeline Operations** dashboard (`k8s/agent-harness/grafana-dashboard.json`) provides:

| Panel | What to Watch |
|-------|---------------|
| Pipeline Execution Rate | Sustained drops may indicate upstream issues |
| Pipeline Success Rate | Below 90% → investigate errors |
| Active Pipelines | Above 8 → backpressure may activate (limit is 10) |
| Agent Dispatch Latency | p99 > 30s → agent dispatch bottleneck |
| Circuit Breaker Status | Any `open` → immediate investigation |
| Loop Engineer Fix Rate | Below 50% → review error classification |
| Backpressure Rejections | Any → scale up or reduce concurrency |

### Key Metrics

```
# Harness HTTP target health
up{job=~".*agent-harness.*"}

# Agent dispatch throughput (when custom OTel meter is scraped)
rate(agent_dispatch_count_total[5m])
```

### Prometheus Alerts

| Alert | Severity | Threshold | Action |
|-------|----------|-----------|--------|
| `AgentHarnessTargetDown` | Critical | Prometheus target down for 2m | Check pod readiness, service, and scrape configuration |

---

## 4. Common Issues

### 4.1 Circuit Breaker Open

**Symptoms:**
- `HarnessCircuitBreakerOpen` alert firing
- Agent dispatch attempts fail immediately
- Logs show "Circuit breaker is OPEN for agent X"

**Causes:**
- 5+ consecutive failures for the same agent
- Agent not responding (timeout)
- OpenCode MCP client unresponsive

**Resolution:**

```bash
# 1. Check which agent is affected
kubectl -n hishope logs deployment/agent-harness | grep "circuit"

# 2. Verify the agent is healthy (test a simple dispatch manually)
# 3. If agent is healthy, the circuit will auto-reset after 30s (half-open)
# 4. If circuit remains open, restart the harness
kubectl -n hishope rollout restart deployment/agent-harness

# 5. If repeated, investigate agent health and network connectivity
kubectl -n hishope describe deployment/agent-harness
```

**Prevention:**
- Ensure OpenCode MCP client is responsive
- Monitor agent dispatch timeouts
- Adjust circuit breaker thresholds if false positives occur

### 4.2 Backpressure Rejections

**Symptoms:**
- `HarnessBackpressureActive` alert firing
- New pipeline start requests return "HTTP 429: Too many pipelines"
- Increased latency on pipeline operations

**Causes:**
- More than 10 concurrent pipelines
- Long-running pipelines (duration > 5 minutes)
- Agent dispatches queued (limit: 20 concurrent)

**Resolution:**

```bash
# 1. Check current active pipeline count
kubectl exec -n hishope deployment/agent-harness -- curl localhost:5200/metrics | grep pipeline_active

# 2. Scale up harness deployment
kubectl -n hishope scale deployment/agent-harness --replicas=3

# 3. Check for stuck pipelines
kubectl -n hishope logs deployment/agent-harness | grep "timeout"

# 4. If needed, cancel stuck pipelines via MCP tool:
# harness_cancel_pipeline { pipeline_run_id: "..." }
```

**Prevention:**
- Implement pipeline timeouts (default 5 minutes)
- Monitor queue depth trends
- Consider increasing `MaxPipelineQueue` and `MaxAgentQueue` in `BackpressureController`

### 4.3 Loop Engineer Escalation Rate High

**Symptoms:**
- `HarnessLoopEngineerEscalationRate` alert firing
- Recurring "Escalated" outcomes in fix history
- Human intervention requests increasing

**Causes:**
- New error patterns not in `ErrorClassifier.PatternMap`
- Safety fence violations (auto-fix prevented)
- Confidence too low for auto-fix decisions

**Resolution:**

```bash
# 1. Check escalation reasons
kubectl -n hishope logs deployment/agent-harness | grep "EscalationReason"

# 2. Review error patterns — add missing patterns to ErrorClassifier.PatternMap
# 3. Adjust ConfidenceScorer weights if needed
# 4. Consider adding new KnownGotcha patterns for recurring issues
```

**Prevention:**
- Regularly review loop engineer outcomes
- Update error pattern map with new known issues
- Monitor confidence score distribution

### 4.4 Pipeline Stuck in Running State

**Symptoms:**
- `HarnessPipelineStuck` alert firing
- Pipeline duration exceeds expected by 3x
- No agent completion events being processed

**Causes:**
- Agent dispatch failed but event not published
- RabbitMQ message lost
- OpenCode task never completed

**Resolution:**

```bash
# 1. Find the stuck pipeline
kubectl -n hishope logs deployment/agent-harness | grep "WARN" | head -20

# 2. Check RabbitMQ for unprocessed messages
# (RabbitMQ management UI or CLI)

# 3. Cancel the stuck pipeline via MCP tool
# harness_cancel_pipeline { pipeline_run_id: "..." }

# 4. If RabbitMQ issue, restart event bus consumer
kubectl -n hishope rollout restart deployment/agent-harness

# 5. Verify pipeline state in database
kubectl -n hishope exec <cockroach-pod> -- cockroach sql --execute \
  "SELECT id, workflow_id, status, started_at FROM hishope.pipeline_runs WHERE status = 'Running';"
```

**Prevention:**
- Implement timeout on pipeline runs (configurable per workflow)
- Add dead-letter queue for failed events
- Monitor RabbitMQ consumer lag

### 4.5 Database Connection Issues

**Symptoms:**
- Harness fails to start: "Unable to connect to CockroachDB"
- Intermittent `PipelineRun` persistence failures
- Migration errors on startup

**Causes:**
- CockroachDB pod restarting
- Network policy blocking egress to port 26257
- Connection string misconfiguration
- Migration version mismatch

**Resolution:**

```bash
# 1. Verify CockroachDB connectivity
kubectl -n hishope exec deployment/agent-harness -- \
  nc -zv cockroachdb-public.hishop.svc.cluster.local 26257

# 2. Check CockroachDB pods
kubectl -n hishope get pods -l app=cockroachdb

# 3. Verify network policy allows egress
kubectl -n hishope describe networkpolicy agent-harness-netpol

# 4. Check migration history
kubectl -n hishope exec deployment/agent-harness -- \
  curl localhost:5200/health/ready

# 5. If migration failed, check logs for specific error
kubectl -n hishope logs deployment/agent-harness | grep "migration"
```

---

## 5. Database Backup and Restore

### Backup Harness State

```bash
# Create CockroachDB backup of harness database
cockroach sql --execute "BACKUP DATABASE hishope_harness TO 's3://hishop-backups/harness/${BACKUP_DATE}';"

# Or use kubectl for ad-hoc backup
kubectl -n hishope exec <cockroach-pod> -- \
  cockroach sql --execute "BACKUP DATABASE hishope_harness TO 'nodelocal://1/harness-backup-${BACKUP_DATE}';"
```

### Restore

```bash
# Restore from backup
cockroach sql --execute "
  RESTORE DATABASE hishope_harness FROM 's3://hishop-backups/harness/${BACKUP_DATE}';
"

# Verify restore
kubectl -n hishope exec <cockroach-pod> -- \
  cockroach sql --execute \
  "SELECT count(*) FROM hishope_harness.pipeline_runs;"
```

### Automated Schedule

Backups are handled by the existing CockroachDB backup CronJob in `k8s/jobs/`. Harness state is included in the cluster-wide backup policy.

---

## 6. Configuration Reference

### Environment Variables

| Variable | Default | Description |
|----------|---------|-------------|
| `ConnectionStrings__HarnessDb` | — | CockroachDB connection string |
| `AgentHarness__Port` | 5200 | HTTP listen port |
| `AgentHarness__RabbitMQHost` | localhost | RabbitMQ hostname |
| `AgentHarness__RabbitMQPort` | 5672 | RabbitMQ port |
| `AgentHarness__RabbitMQVHost` | / | RabbitMQ virtual host |
| `AgentHarness__RabbitMQUsername` | guest | RabbitMQ username |
| `AgentHarness__RabbitMQPassword` | guest | RabbitMQ password |
| `AgentHarness__MaxPipelineQueue` | 10 | Max concurrent pipelines |
| `AgentHarness__MaxAgentQueue` | 20 | Max concurrent agent dispatches |
| `AgentHarness__PipelineTimeoutMinutes` | 5 | Default pipeline timeout |
| `AgentHarness__CircuitBreakerThreshold` | 5 | Failures before circuit opens |
| `AgentHarness__CircuitBreakerDurationSeconds` | 30 | Circuit open duration |
| `AgentHarness__LoopEngineerMaxIterations` | 3 | Max auto-fix iterations |
| `AgentHarness__LoopEngineerConfidenceThreshold` | 0.8 | Min confidence for auto-fix |
| `OTEL_EXPORTER_OTLP_ENDPOINT` | http://localhost:4317 | OpenTelemetry endpoint |
| `ASPNETCORE_ENVIRONMENT` | Production | ASP.NET environment |

### App Settings Structure

```json
{
  "ConnectionStrings": {
    "HarnessDb": "Host=...;Database=hishop_harness;Username=...;Password=..."
  },
  "AgentHarness": {
    "Port": 5200,
    "RabbitMQHost": "rabbitmq.hishop.svc.cluster.local",
    "RabbitMQPort": 5672,
    "MaxPipelineQueue": 10,
    "MaxAgentQueue": 20,
    "PipelineTimeoutMinutes": 5,
    "CircuitBreakerThreshold": 5,
    "CircuitBreakerDurationSeconds": 30,
    "LoopEngineerMaxIterations": 3,
    "LoopEngineerConfidenceThreshold": 0.8
  }
}
```

### Kubernetes Secrets Required

| Secret Name | Key | Description |
|-------------|-----|-------------|
| `harness-db-credentials` | `connection-string` | CockroachDB connection string |
| `harness-rabbitmq-credentials` | `username` | RabbitMQ username |
| `harness-rabbitmq-credentials` | `password` | RabbitMQ password |

---

## Appendix: Quick Reference

### Useful kubectl Commands

```bash
# Get all harness resources
kubectl -n hishope get all -l app=agent-harness

# Watch pod status
kubectl -n hishope get pods -l app=agent-harness -w

# Get metrics endpoint
kubectl -n hishope port-forward deployment/agent-harness 5200:5200
# Then visit http://localhost:5200/metrics

# Execute SQL against harness database
kubectl -n hishope exec <cockroach-pod> -- \
  cockroach sql --database hishope_harness --execute "SELECT * FROM pipeline_runs ORDER BY created_at DESC LIMIT 10;"

# Restart harness
kubectl -n hishope rollout restart deployment/agent-harness

# View harness config
kubectl -n hishope describe configmap agent-harness-config

# View harness secrets
kubectl -n hishope get secret harness-db-credentials -o yaml
```

### Log Level Toggle

```bash
# Set to Debug for troubleshooting
kubectl -n hishope set env deployment/agent-harness ASPNETCORE_LOGLEVEL__Default=Debug

# Reset to Info
kubectl -n hishope set env deployment/agent-harness ASPNETCORE_LOGLEVEL__Default=Info
```

### Health Check

```bash
# Forward port
kubectl -n hishope port-forward deployment/agent-harness 5200:5200

# Check health
curl http://localhost:5200/health
curl http://localhost:5200/health/ready
curl http://localhost:5200/health/startup
```
