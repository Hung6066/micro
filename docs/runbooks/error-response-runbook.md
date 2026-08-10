# Error Response Runbook

> **Document:** RUNBOOK-ERR-001
> **Version:** 1.0
> **Audience:** SRE On-Call, Backend Developer, DevOps
> **Severity:** P0 (Critical) / P1 (High) / P2 (Medium)
> **Last updated:** 2026-07-17

---

## Table of Contents

1. [Receiving an Alert](#1-receiving-an-alert)
2. [Finding the Trace in Jaeger](#2-finding-the-trace-in-jaeger)
3. [Finding the Log in Kibana](#3-finding-the-log-in-kibana)
4. [Checking the Grafana Error Tracking Dashboard](#4-checking-the-grafana-error-tracking-dashboard)
5. [Common Error Patterns and Fixes](#5-common-error-patterns-and-fixes)

---

## 1. Receiving an Alert

### Alert Channels

| Channel | Severity | Example Message |
|---|---|---|
| **Slack `#his-hope-alerts`** | All severities | `[CRITICAL] HighErrorRate - patient-service has error rate > 5%` |
| **Slack `#his-hope-critical`** | Critical only | `🚨 ServiceDown - identity-service is DOWN` |
| **PagerDuty** | Critical only | Pages SRE on-call |
| **Email (oncall@his-hope.com)** | Critical only | Subject: `[ALERT] ServiceDown - patient-service` |

### Initial Triage Checklist

```
□ 1. Acknowledge the alert in PagerDuty / Slack
□ 2. Determine severity (Critical = P0, Warning = P2)
□ 3. Check if this is a known issue (search #his-hope-alerts history)
□ 4. Check if maintenance is in progress (see #his-hope-maintenance)
□ 5. Open the Grafana Error Tracking dashboard
```

### Alert Severity Guide

| Severity | Response Time | Action |
|---|---|---|
| **CRITICAL (P0)** | < 15 minutes | Drop everything, investigate immediately. Involve service owner. |
| **HIGH (P1)** | < 1 hour | Investigate after current critical issues. |
| **WARNING (P2)** | < 4 hours | Investigate during normal business hours. |

---

## 2. Finding the Trace in Jaeger

### Prerequisites

```bash
# Port-forward Jaeger UI
kubectl port-forward svc/jaeger-query -n linkerd-jaeger 16686:16686 &
```

Jaeger UI: http://localhost:16686

### Step 1: Extract the traceId

The ProblemDetails response always includes a `traceId` field. If you have a user-reported error:

1. Ask the user for the **Reference ID** (correlationId) shown in the ErrorBar component
2. Or extract `traceId` from the server response headers

### Step 2: Search in Jaeger

```bash
# Option A: Search by trace ID (direct URL)
# http://localhost:16686/trace/{traceId}

# Option B: Search by service + tags
# Service: patient-service
# Tags: correlation.id=abc123def456
# Min Duration: (leave empty)
# Lookback: Last 15 minutes
```

### Step 3: Analyze the Trace

```
Look for:
  ⚠️  Span with red highlight → indicates error status
  ⏱️  Spans taking unusually long (>500ms)
  🔴 gRPC spans with error codes (NotFound, Internal, etc.)
  🚫 Missing spans → service may not have propagated context

Common trace patterns:
┌─────────────────────────────────────────────────────────┐
│ Normal:                                                   │
│ POST /api/v1/patients [45ms] ✓                           │
│   ├── CreatePatientCommand.Handle [30ms] ✓               │
│   │     ├── db.save [25ms] ✓                             │
│   │     └── cache.remove [3ms] ✓                         │
│   └── eventbus.publish [10ms] ✓                          │
│                                                           │
│ Slow DB:                                                  │
│ POST /api/v1/patients [1250ms] ✗                         │
│   └── CreatePatientCommand.Handle [1200ms] ✗             │
│         └── db.save [1150ms] ✗ ← BOTTLEMECK              │
│                                                           │
│ Missing dependency:                                       │
│ POST /api/v1/appointments [5000ms] ✗                     │
│   ├── CheckPatientExists [timeout] ✗ ← gRPC FAILED       │
│   └── (remaining spans missing)                           │
└─────────────────────────────────────────────────────────┘
```

### Step 4: Export Trace

```bash
# Export trace as JSON for sharing with the team
curl -s "http://localhost:16686/api/traces/{traceId}" > trace-export.json
```

---

## 3. Finding the Log in Kibana

### Prerequisites

```bash
# Port-forward Kibana
kubectl port-forward svc/kibana -n monitoring 5601:5601 &
```

Kibana UI: http://localhost:5601

### Search by Correlation ID

If you have the correlation ID (from ErrorBar, ProblemDetails, or alert context):

```
Kibana Query:
  correlationId: "abc123def456"
  AND @timestamp > now-1h
```

This returns every log entry across all services for that single request.

### Search by Trace ID

```
Kibana Query:
  traceId: "0a1b2c3d4e5f6789"
  AND @timestamp > now-1h
```

### Common Log Queries

| Scenario | Kibana Query |
|---|---|
| All errors in last 15m | `level: "Error" AND @timestamp > now-15m` |
| Errors for a specific service | `service: "patient-service" AND level: "Error" AND @timestamp > now-1h` |
| Slow requests | `properties.ElapsedMilliseconds > 500 AND @timestamp > now-15m` |
| Circuit breaker events | `messageTemplate: "Circuit breaker*" AND @timestamp > now-1h` |
| gRPC errors | `messageTemplate: "gRPC call*" AND level: "Error" AND @timestamp > now-1h` |
| Outbox failures | `messageTemplate: "Outbox*" AND level: "Error" AND @timestamp > now-1h` |
| Authentication failures | `messageTemplate: "Authentication failed*" AND @timestamp > now-1h` |
| Rate limiting hits | `messageTemplate: "Rate limit*" AND @timestamp > now-1h` |

### Structured Log Format

```json
{
  "@timestamp": "2026-07-17T12:00:00.123Z",
  "level": "Error",
  "service": "patient-service",
  "traceId": "0a1b2c3d4e5f6789",
  "correlationId": "abc123def456",
  "messageTemplate": "Unhandled exception occurred: {Message}",
  "properties": {
    "Message": "Patient with ID 1001 not found",
    "Exception": "His.Hope.SharedKernel.Domain.Exceptions.NotFoundException",
    "ElapsedMilliseconds": 1234
  }
}
```

---

## 4. Checking the Grafana Error Tracking Dashboard

### Prerequisites

```bash
# Port-forward Grafana
kubectl port-forward svc/grafana -n monitoring 3000:3000 &
```

Grafana UI: http://localhost:3000/d/error-tracking

### Dashboard Walkthrough

```
┌────────────────────────────────────────────────────────────┐
│  Panel 1: Error Rate by Service                            │
│  ┌────────────────────────────────────────────────────┐   │
│  │  ╱╲          ╱╲                                    │   │
│  │ ╱  ╲  ╱╲    ╱  ╲  ← patient-service spike         │   │
│  │╱    ╲╱  ╲  ╱    ╲                                 │   │
│  │           ╲╱      ╲                                │   │
│  └────────────────────────────────────────────────────┘   │
│  Action: If sustained > 5%, check the corresponding trace  │
├────────────────────────────────────────────────────────────┤
│  Panel 3: Request Latency p50 / p95 / p99                   │
│  ┌────────────────────────────────────────────────────┐   │
│  │  ╱╲                                                  │   │
│  │ ╱  ╲     ╱╲     p99 ← check if correlated with errors│   │
│  │╱    ╲   ╱  ╲                                        │   │
│  │      ╲ ╱    ╲                                       │   │
│  └────────────────────────────────────────────────────┘   │
│  Action: If p99 > 500ms, investigate DB query performance  │
├────────────────────────────────────────────────────────────┤
│  Panel 4: Active Alerts                                    │
│  ┌────────────────────────────────────────────────────┐   │
│  │ HighErrorRate (patient-service)     CRITICAL       │   │
│  │ HighMemoryUsage (identity-service)  WARNING        │   │
│  └────────────────────────────────────────────────────┘   │
├────────────────────────────────────────────────────────────┤
│  Panel 6: DLQ Message Count                                │
│  ┌────────────────────────────────────────────────────┐   │
│  │  DLQ Messages: 42                     [YELLOW]     │   │
│  └────────────────────────────────────────────────────┘   │
│  Action: If > 100, check RabbitMQ for failed messages      │
├────────────────────────────────────────────────────────────┤
│  Panel 7: Errors vs Latency Correlation                    │
│  ┌────────────────────────────────────────────────────┐   │
│  │  ╱╲  errors  ╱╲                                    │   │
│  │ ╱  ╲════════╱  ╲══ latency  ← correlated spike    │   │
│  │╱    ╲      ╱    ╲                                 │   │
│  └────────────────────────────────────────────────────┘   │
│  Action: Correlated spikes suggest overload or deadlock    │
└────────────────────────────────────────────────────────────┘
```

### Quick Diagnostic Actions

| Panel Observation | Action |
|---|---|
| Error rate > 5% for a service | Open Jaeger, search for recent traces with errors |
| p99 latency > 500ms | Check slow DB queries in Kibana (`ElapsedMilliseconds > 500`) |
| Active alerts include `CircuitBreakerOpen` | Check resilience health; service may be degraded |
| DLQ count growing | Inspect DLQ messages in RabbitMQ; check consumer logs |
| Memory > 90% | Scale up or investigate memory leak; check `kubectl top pods` |
| Error + Latency correlated spike | Likely resource exhaustion; check CPU, memory, connection pools |

---

## 5. Common Error Patterns and Fixes

### Pattern 1: HighErrorRate with 500s

**Symptoms:**
- Alert: `HighErrorRate` firing
- Grafana: Spike in Panel 1 (Error Rate by Service)
- Kibana: `level: "Error"` with `status >= 500`

**Common Causes:**

| Cause | Trace Pattern | Kibana Signal | Fix |
|---|---|---|---|
| **DB connection pool exhausted** | `db.save` spans > 5s or timing out | `"Timeout in pool"` or `"Npgsql.ConnectionPool"` | Increase pool size or scale service replicas |
| **Deadlock** | `db.save` spans with high duration | `"deadlock detected"` in logs | Retry logic (already in Polly); reduce transaction scope |
| **gRPC dependency unavailable** | Missing downstream spans or gRPC errors | `"Deadline Exceeded"` or `"Unavailable"` | Check downstream service health; increase timeout |
| **Memory overflow** | All spans slow, GC pressure | `"OutOfMemoryException"` | Increase memory limit; check for memory leak |
| **Unhandled exception** | Span red with exception tag | Exception stack trace in log | Fix the bug; add proper exception handling |

**Triage Steps:**
```
1. Check Grafana: Is it a single service or all services?
   → Single service → investigate that service's logs
   → All services → infrastructure issue (DB, Redis, RabbitMQ)

2. Check Jaeger: What's the common failing operation?
   → db.save slow → DB issue
   → gRPC call failing → downstream service issue
   → Validation → client issue (may be a bad deployment)

3. Check Kibana: What exception type?
   → NotFoundException → client sending bad data
   → DomainException → business rule violation
   → NullReferenceException → bug
   → TimeoutException → dependency slow
```

### Pattern 2: ServiceDown

**Symptoms:**
- Alert: `ServiceDown` firing
- Prometheus: `up == 0` for the target

**Triage Steps:**
```
1. Check pod status:
   kubectl get pods -n his-hope | findstr {service}

2. Check pod logs:
   kubectl logs deploy/his-hope-{service} -n his-hope --tail=50 --previous

3. Check pod events:
   kubectl describe pod his-hope-{service}-xxx -n his-hope

4. Check resource usage:
   kubectl top pod his-hope-{service}-xxx -n his-hope

5. Common causes:
   → OOMKilled: Increase memory limit or fix memory leak
   → CrashLoopBackOff: Read logs, likely unhandled exception at startup
   → ImagePullBackOff: Check container registry access
   → Pending: Check node resources or PVC binding
```

### Pattern 3: DeadLetterQueueGrowth

**Symptoms:**
- Alert: `DeadLetterQueueGrowth` firing
- Grafana Panel 6: DLQ count > 100

**Triage Steps:**
```
1. Check DLQ messages in RabbitMQ:
   kubectl exec his-hope-rabbitmq -- rabbitmqctl list_queues name messages consumers
   → Find queues matching *deadletter* or *dlq*

2. Peek at a failed message:
   kubectl exec his-hope-rabbitmq -- rabbitmqadmin get queue={queue_name} count=1

3. Identify the failing consumer:
   → Check consumer logs for the queue's target service
   → Look for "Failed to process message" or serialization errors

4. Common causes:
   → Schema mismatch: Message format changed but consumer not updated
   → Transient dependency failure: Consumer can't reach DB or downstream service
   → Poison message: One bad message that always fails (move to parking lot)
```

### Pattern 4: OutboxBacklogGrowing

**Symptoms:**
- Alert: `OutboxBacklogGrowing` firing
- Events not reaching downstream consumers

**Triage Steps:**
```
1. Check outbox processor status:
   kubectl logs deploy/his-hope-{service} -n his-hope | findstr "Outbox"

2. Check RabbitMQ connection:
   kubectl exec his-hope-rabbitmq -- rabbitmqctl list_connections

3. Check outbox table directly:
   kubectl exec his-hope-postgres -- psql -U postgres -d {service}db -c
     "SELECT count(*), status FROM outbox_messages GROUP BY status;"

4. Check for stuck messages:
   kubectl exec his-hope-postgres -- psql -U postgres -d {service}db -c
     "SELECT * FROM outbox_messages WHERE processed_at IS NULL
      AND created_at < now() - interval '5 minutes' LIMIT 10;"

5. Common causes:
   → RabbitMQ down or unreachable
   → Consumer not keeping up with publish rate
   → Serialization error on one message blocking the batch
```

### Pattern 5: ApiLatencyHigh

**Symptoms:**
- Alert: `ApiLatencyHigh` firing
- p99 latency > 2s for 5 minutes

**Triage Steps:**
```
1. Identify the slow service from alert labels

2. Check top endpoints in Kibana:
   Query: properties.ElapsedMilliseconds > 2000 AND service = "{service}"
   → Group by properties.RequestPath

3. Check DB query performance:
   Query: properties.ElapsedMilliseconds > 500 AND messageTemplate = "*db*"

4. Common causes:
   → Missing DB index (check slow query log)
   → N+1 query pattern (EF Core lazy loading)
   → Large dataset without pagination
   → gRPC call timeout waiting for downstream
   → Lock contention (concurrent writes to same record)
```

### Pattern 6: CircuitBreakerOpen

**Symptoms:**
- Alert: `CircuitBreakerOpen` firing
- Service returning cached/stale data or 503s

**Triage Steps:**
```
1. Identify the failing downstream dependency from alert labels

2. Check the circuit breaker metrics:
   prometheus_query "circuit_breaker_state{name='{breaker_name}'}"

3. Fix the downstream dependency first:
   → If DB: check connection pool, query performance
   → If gRPC: check downstream service health
   → If Redis: check Redis cluster status

4. The circuit breaker will auto-recover after 30s (half-open → closed on success)
   → If it keeps tripping, the downstream issue is persistent
```

---

## Quick Reference Card

```bash
# === Port Forwards for Debugging ===
kubectl port-forward svc/jaeger-query -n linkerd-jaeger 16686:16686 &
kubectl port-forward svc/kibana -n monitoring 5601:5601 &
kubectl port-forward svc/grafana -n monitoring 3000:3000 &
kubectl port-forward svc/prometheus-server -n monitoring 9090:9090 &

# === Quick Health Check ===
kubectl get pods -n his-hope | findstr "0/1|CrashLoop|Error|Pending"
kubectl top pods -n his-hope --sort-by=cpu | Select-Object -First 5

# === Get Error Logs ===
kubectl logs deploy/his-hope-{service} -n his-hope --tail=100 | findstr "Error|Exception|Failed"

# === Check RabbitMQ ===
kubectl exec his-hope-rabbitmq -n his-hope -- rabbitmqctl list_queues name messages consumers

# === Check DB Connections ===
kubectl exec his-hope-postgres -n his-hope -- psql -U postgres -c "SELECT count(*) FROM pg_stat_activity;"

# === Check Redis ===
kubectl exec his-hope-redis-0 -n his-hope -- redis-cli INFO memory | findstr "used_memory_human"
```

---

> **Last updated**: 2026-07-17 | **Maintainer**: @sre | **Next review**: 2026-08-17
