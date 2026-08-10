# CockroachDB Node Down / Replication Lag Runbook

| Field | Value |
|-------|-------|
| **Severity** | P0 |
| **Service** | CockroachDB (all service databases) |
| **Owner** | SRE Team, DBA |
| **Last Updated** | 2026-07-17 |

## Alert Trigger

- `CockroachNodeDown` — A CRDB node has been unreachable for > 30s
- `CockroachReplicationLag` — Max replication lag > 5s across any range
- `CockroachUnderreplicatedRanges` — Number of under-replicated ranges > 0 for > 5m

## Symptoms

- Application errors: `failed to connect to CockroachDB` or `node is not accepting clients`
- Grafana: Node count drops in `CockroachDB / Overview` dashboard
- `kubectl get pods -n his-hope` shows CRDB pod in `CrashLoopBackOff`, `Error`, or `Pending`
- Slow queries across all services (patient, appointment, billing, etc.)
- `gRPC` spans in Jaeger showing `failed to reach cluster` on DB operations
- PagerDuty alert firing for multiple services simultaneously

## Diagnosis

```bash
# 1. Check CockroachDB pod status
kubectl get pods -n his-hope -l app=cockroachdb

# 2. Get detailed pod info on the failed node
kubectl describe pod cockroachdb-{N} -n his-hope

# 3. Check CRDB logs for the failing node
kubectl logs cockroachdb-{N} -n his-hope --tail=100

# 4. Connect to a healthy CRDB node and check cluster health
kubectl exec cockroachdb-0 -n his-hope -- \
  cockroach node status --certs-dir=/cockroach/certs --host=cockroachdb-0.cockroachdb

# 5. Check under-replicated ranges
kubectl exec cockroachdb-0 -n his-hope -- \
  cockroach sql --certs-dir=/cockroach/certs --host=cockroachdb-0.cockroachdb \
  -e "SELECT range_id, replicas, lease_holder FROM crdb_internal.ranges WHERE array_length(replicas, 1) < 3;"

# 6. Check replication lag
kubectl exec cockroachdb-0 -n his-hope -- \
  cockroach sql --certs-dir=/cockroach/certs --host=cockroachdb-0.cockroachdb \
  -e "SELECT node_id, cast(actual_timestamps['now'] - actual_timestamps['min'] AS interval) AS replication_lag FROM crdb_internal.cluster_liveness;"

# 7. Check cluster settings
kubectl exec cockroachdb-0 -n his-hope -- \
  cockroach sql --certs-dir=/cockroach/certs --host=cockroachdb-0.cockroachdb \
  -e "SHOW ALL CLUSTER SETTINGS;" | findstr "replication"

# 8. Check disk space on the failing node
kubectl exec cockroachdb-{N} -n his-hope -- df -h /cockroach

# 9. Check if the node was decommissioned
kubectl exec cockroachdb-0 -n his-hope -- \
  cockroach node status --decommission --certs-dir=/cockroach/certs --host=cockroachdb-0.cockroachdb
```

## Mitigation

### Step 1 — Isolate the node (if still serving)

```bash
# Decommission the failing node to safely drain replicas
kubectl exec cockroachdb-0 -n his-hope -- \
  cockroach node decommission {N} --certs-dir=/cockroach/certs --host=cockroachdb-0.cockroachdb

# Verify decommissioning status
kubectl exec cockroachdb-0 -n his-hope -- \
  cockroach node status --decommission --certs-dir=/cockroach/certs --host=cockroachdb-0.cockroachdb
```

### Step 2 — Restore replication factor

```bash
# Check if ranges are re-replicating to remaining nodes
kubectl exec cockroachdb-0 -n his-hope -- \
  cockroach sql --certs-dir=/cockroach/certs --host=cockroachdb-0.cockroachdb \
  -e "SELECT count(*) AS underreplicated FROM crdb_internal.ranges WHERE array_length(replicas, 1) < 3;"
```

### Step 3 — Force traffic away from failed node

```bash
# Suspend the node's Kubernetes service endpoint (if using Headless Service)
kubectl label pod cockroachdb-{N} -n his-hope cockroach.io/health=unhealthy

# Or scale up remaining nodes if auto-scaling is available
kubectl scale statefulset cockroachdb -n his-hope --replicas=5
```

### Step 4 — If the node is permanently lost, replace it

```bash
# Delete the failed PVC
kubectl delete pvc datadir-cockroachdb-{N} -n his-hope

# Delete the pod so StatefulSet recreates it
kubectl delete pod cockroachdb-{N} -n his-hope
```

## Resolution

### Root Cause Fix

1. **Disk full**: Clean old logs, increase PV size, or set up log rotation:
   ```bash
   kubectl exec cockroachdb-{N} -n his-hope -- \
     cockroach sql --certs-dir=/cockroach/certs -e "SET CLUSTER SETTING server.log_max_size = '200MiB';"
   ```

2. **Node restart / OOM**: Increase memory limits in the StatefulSet manifest.

3. **Network partition**: Check Cilium network policies and node-to-node connectivity:
   ```bash
   kubectl exec cockroachdb-0 -n his-hope -- \
     curl -sf http://cockroachdb-1.cockroachdb:8080/health
   ```

4. **PVC corruption**: Restore from backup:
   ```bash
   kubectl exec cockroachdb-0 -n his-hope -- \
     cockroach sql --certs-dir=/cockroach/certs \
     -e "RESTORE FROM 's3://his-hope-crdb-backups/latest' AS OF SYSTEM TIME '-10s';"
   ```

### Verification

```bash
# 1. All pods Running/Ready
kubectl get pods -n his-hope -l app=cockroachdb

# 2. All nodes healthy
kubectl exec cockroachdb-0 -n his-hope -- \
  cockroach node status --certs-dir=/cockroach/certs --host=cockroachdb-0.cockroachdb

# 3. Zero under-replicated ranges
kubectl exec cockroachdb-0 -n his-hope -- \
  cockroach sql --certs-dir=/cockroach/certs --host=cockroachdb-0.cockroachdb \
  -e "SELECT count(*) FROM crdb_internal.ranges WHERE array_length(replicas, 1) < 3;"

# 4. Replication lag < 1s
kubectl exec cockroachdb-0 -n his-hope -- \
  cockroach sql --certs-dir=/cockroach/certs --host=cockroachdb-0.cockroachdb \
  -e "SELECT max(datediff('second', actual_timestamps['min'], actual_timestamps['now'])) AS max_lag_seconds FROM crdb_internal.cluster_liveness;"

# 5. Applications can connect
kubectl exec deploy/his-hope-patient-service -n his-hope -- \
  curl -sf http://localhost:8080/health/ready
```

## Postmortem

Use the standard incident postmortem template at `docs/postmortem-template.md`.

Key metrics to capture:
- Time from alert to mitigation
- Number of under-replicated ranges over time
- Disk IOPS and latency on the failed node prior to failure
- Whether `kv.replication_critical_signal` was emitted before the failure

---

> **Last updated**: 2026-07-17 | **Maintainer**: @dba | **Next review**: 2026-09-17
