# High Latency (p99 > 500ms) Runbook

| Field | Value |
|-------|-------|
| **Severity** | P1 (worst-case P0 if SLA breach) |
| **Service** | Any His.Hope microservice |
| **Owner** | SRE Team, Backend Developer (@dotnet) |
| **Last Updated** | 2026-07-17 |

## Alert Trigger

- `ApiLatencyHigh` — p99 > 500ms for 5m
- `gRPCLatencyHigh` — gRPC call duration p99 > 1s for 5m
- `DbQuerySlow` — Individual query duration > 1s in slow query log
- `UserSLAWarning` — Breach of 2s p99 SLA
- `LinkerdLatencyHigh` — Linkerd proxy reporting > 1s p99

## Symptoms

- **Jaeger traces**: DB or gRPC spans significantly slower than normal baseline
- **Grafana**: `http_request_duration_seconds` p99 line spikes above 500ms threshold
- **Kibana**: Queries with `ElapsedMilliseconds > 500` appear frequently
- **UI feedback**: Page loads take > 5s; Angular loading spinners stay visible
- **User complaints**: "The system is slow" reports from clinical staff
- **CockroachDB**: `node_cpu_percent` high, `sql_conns` saturated
- **Outbox**: Event processing delay grows as consumers fall behind

## Diagnosis

```bash
# 1. Identify the slow service and endpoint
kubectl logs deploy/his-hope-{service} -n his-hope --tail=100 | findstr "ElapsedMilliseconds"
# Look for patterns: which endpoint, which DB query, which downstream call

# 2. Check Jaeger for slow traces (see error-response-runbook.md §2)
# Port-forward Jaeger:
kubectl port-forward svc/jaeger-query -n linkerd-jaeger 16686:16686 &

# Search: Service={service}, Min Duration=500ms, Lookback=15m

# 3. Check CockroachDB slow queries
kubectl exec cockroachdb-0 -n his-hope -- \
  cockroach sql --certs-dir=/cockroach/certs --host=cockroachdb-0.cockroachdb \
  -e "SELECT * FROM crdb_internal.node_statement_statistics WHERE service_lat > interval '1s' ORDER BY service_lat DESC LIMIT 20;"

# 4. Check active sessions and locks in CockroachDB
kubectl exec cockroachdb-0 -n his-hope -- \
  cockroach sql --certs-dir=/cockroach/certs --host=cockroachdb-0.cockroachdb \
  -e "SELECT * FROM crdb_internal.cluster_sessions WHERE status = 'Active' ORDER BY elapsed_time DESC;"

# 5. Check for blocking transactions
kubectl exec cockroachdb-0 -n his-hope -- \
  cockroach sql --certs-dir=/cockroach/certs --host=cockroachdb-0.cockroachdb \
  -e "SELECT blocking_session_id, blocked_session_id, duration FROM crdb_internal.cluster_locks;"

# 6. Check Redis latency
kubectl exec redis-0 -n his-hope -- redis-cli -a $REDIS_PASSWORD LATENCY LATEST

# 7. Check RabbitMQ consumer duration
kubectl exec his-hope-rabbitmq-0 -n his-hope -- rabbitmqctl list_queues name consumers messages

# 8. Check node resource usage
kubectl top nodes
kubectl top pods -n his-hope --sort-by=cpu | Select-Object -First 10

# 9. Check Linkerd latency metrics
linkerd viz stat deploy/his-hope-{service} -n his-hope -t http
linkerd viz top deploy/his-hope-{service} -n his-hope

# 10. Check HPA status
kubectl get hpa -n his-hope | findstr "{service}"
```

## Mitigation

### Step 1 — Immediate Relief

```bash
# Option A: Scale up the affected service
kubectl scale deploy/his-hope-{service} -n his-hope --replicas=5

# Option B: Scale up the dependency (DB connection pool only helps so much)
# Increase max connections in CRDB
kubectl exec cockroachdb-0 -n his-hope -- \
  cockroach sql --certs-dir=/cockroach/certs -e "SET CLUSTER SETTING sql.defaults.max_connections = 500;"

# Option C: Restart pods to clear any connection pool issues
kubectl rollout restart deploy/his-hope-{service} -n his-hope
```

### Step 2 — Throttle Non-Critical Traffic

```bash
# Apply stricter rate limiting to non-clinical endpoints
# In YARP Gateway:
kubectl edit configmap yarp-config -n his-hope
# Add rate limit policy: PermitLimit=50, Window=00:01:00

# Or use Linkerd request mirroring/retry limiting:
linkerd viz -n his-hope routes deploy/his-hope-{service} --to deploy/his-hope-{dependency}
```

### Step 3 — Enable Query Tracing (if DB bound)

```bash
# Enable CockroachDB tracing for slow queries
kubectl exec cockroachdb-0 -n his-hope -- \
  cockroach sql --certs-dir=/cockroach/certs \
  -e "SET CLUSTER SETTING sql.trace.stmt.enable_threshold = '500ms';"

# Check the trace after enabling
kubectl exec cockroachdb-0 -n his-hope -- \
  cockroach sql --certs-dir=/cockroach/certs \
  -e "SELECT * FROM crdb_internal.node_queries ORDER BY start DESC LIMIT 20;"
```

## Resolution

### Root Cause Fix

| Cause | Jaeger Signal | Kibana Signal | Fix |
|---|---|---|---|
| **Missing index** | DB span > 1s, full table scan | `"full scan"` in query plan | Add B-tree index on filtered columns |
| **N+1 query** | 100+ DB spans in a single trace | `"Npgsql.CommandExecution"` × many | Use `Include()` or `ProjectTo()` in EF Core |
| **Lock contention** | DB spans with high `elapsed`, overlapping timestamps | `"deadlock detected"` | Check transaction isolation; reduce scope |
| **Connection pool exhaustion** | DB spans show `"timeout in pool"` | `"Npgsql.ConnectionPool"` | Increase `MaxPoolSize` or add replicas |
| **CPU starvation** | All spans uniformly slow | High `cpu_percent` in Prometheus | Add CPU limits; scale horizontally |
| **gRPC serialization** | Client span long, server span short | Large payload in `properties` | Reduce message size; enable compression |
| **Slow external API (FHIR)** | Span to external endpoint > 2s | `"HttpClient timeout"` | Add circuit breaker; cache responses |

### Verification

```bash
# 1. p99 latency < 200ms
# Check Grafana: http://localhost:3000/d/latency/{service}

# 2. No slow queries in CockroachDB
kubectl exec cockroachdb-0 -n his-hope -- \
  cockroach sql --certs-dir=/cockroach/certs \
  -e "SELECT count(*) FROM crdb_internal.node_statement_statistics WHERE service_lat > interval '1s';"

# 3. No blocking transactions
kubectl exec cockroachdb-0 -n his-hope -- \
  cockroach sql --certs-dir=/cockroach/certs \
  -e "SELECT count(*) FROM crdb_internal.cluster_locks WHERE duration > interval '30s';"

# 4. Application health endpoint responds in < 100ms
time curl -sf http://localhost:{port}/health/ready

# 5. Load test with k6 (run in test cluster)
k6 run --vus 20 --duration 30s k6/scripts/{service}-smoke-test.js
# Verify p95 < 300ms
```

## Postmortem

Use the standard incident postmortem template at `docs/postmortem-template.md`.

Key metrics to capture:
- Latency histogram (p50, p95, p99) 1h before to 1h after
- DB query plan for the slowest query (use `EXPLAIN ANALYZE`)
- Connection pool metrics (`kubectl exec` to check `pg_stat_activity`)
- Whether HPA triggered and if it was fast enough
- Linkerd per-route latency breakdown

---

> **Last updated**: 2026-07-17 | **Maintainer**: @dotnet | **Next review**: 2026-09-17
