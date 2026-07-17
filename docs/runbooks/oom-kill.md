# OOM Kill (Memory Limits / Memory Profile) Runbook

| Field | Value |
|-------|-------|
| **Severity** | P0 |
| **Service** | Any His.Hope microservice (most common: patient-service, clinical-service, identity-service) |
| **Owner** | SRE Team, Backend Developer (@dotnet) |
| **Last Updated** | 2026-07-17 |

## Alert Trigger

- `KubePodOOMKilled` — Pod terminated with `OOMKilled` reason
- `ContainerMemoryUsageHigh` — Memory usage > 85% of limit for > 5m
- `NodeMemoryPressure` — Node memory pressure > 90%
- `CRDBMemorySpike` — CockroachDB memory > 80% of limit

## Symptoms

- **Pod restart**: `kubectl get pods` shows `OOMKilled` in `RESTARTS` column
- **Grafana**: Memory usage line hits the `limit` ceiling, then drops to 0 (pod crash)
- **Kibana**: Last log before crash may contain `OutOfMemoryException` or GC pressure warnings
- **Linkerd**: `linkerd-viz` shows the pod disappearing and reappearing with new IP
- **Service degradation**: Intermittent 503 errors while pod is restarting
- **Burst of alerts**: Multiple `OOMKilled` events across replicas (indicates code-level leak)

## Diagnosis

```bash
# 1. Check which pods were OOM-killed
kubectl get pods -n his-hope | findstr "OOMKilled"

# 2. Describe the killed pod (exit code 137 = OOM)
kubectl describe pod his-hope-{service}-{hash} -n his-hope | findstr "OOMKilled|Exit Code|Reason|Memory"

# 3. Check the exact OOM event and memory limit
kubectl get pod his-hope-{service}-{hash} -n his-hope -o json | \
  python -c "import sys,json; p=json.load(sys.stdin); c=p['spec']['containers'][0]; print('Limits:', c['resources'].get('limits',{})); print('Requests:', c['resources'].get('requests',{}))"

# 4. Check memory usage before OOM (if metrics still available)
# Prometheus instant query:
curl -s "http://localhost:9090/api/v1/query?query=container_memory_working_set_bytes{pod=~'his-hope-{service}.*'}" | \
  python -c "import sys,json; data=json.load(sys.stdin); print(json.dumps(data['data']['result'][:5], indent=2))"

# 5. Check GC metrics if the pod is .NET
kubectl exec deploy/his-hope-{service} -n his-hope -- \
  curl -sf http://localhost:9464/metrics | findstr "gc"
# Look for: dotnet_gc_total_allocated_bytes, dotnet_gc_heap_size_bytes

# 6. Check if all replicas are affected (code-level issue) or just one (node-level)
kubectl top pods -n his-hope | findstr "{service}"

# 7. Check node memory pressure
kubectl top nodes
kubectl describe node {node-name} | findstr "MemoryPressure|DiskPressure|PIDPressure"

# 8. Check for memory.heap dump files (if configured)
kubectl exec deploy/his-hope-{service} -n his-hope -- \
  find /tmp -name "*.dmp" -o -name "*.hprof" 2>/dev/null
```

## Mitigation

### Step 1 — Immediate Relief

```bash
# Option A: Temporarily increase memory limit (quick fix)
kubectl set resources deploy/his-hope-{service} -n his-hope \
  --limits memory=2Gi \
  --requests memory=1Gi
# ⚠ This masks the root cause — use only as a bridge

# Option B: Increase replicas to spread memory load
kubectl scale deploy/his-hope-{service} -n his-hope --replicas=5

# Option C: If the pod is stuck CrashLoopBackOff due to OOM on startup
# Set higher memory limits temporarily:
kubectl patch deploy/his-hope-{service} -n his-hope -p \
  '{"spec":{"template":{"spec":{"containers":[{"name":"{service}","resources":{"limits":{"memory":"4Gi"},"requests":{"memory":"2Gi"}}}]}}}}'

# Option D: Evict pods from the hot node
kubectl drain {node-name} --ignore-daemonsets --delete-local-data
```

### Step 2 — Capture Memory Profile (for .NET services)

```bash
# 1. Enable dotnet-counters live
kubectl exec deploy/his-hope-{service} -n his-hope -- \
  dotnet-counters monitor -p 1 --refresh-interval 5 \
    System.Runtime[counter=cpu-usage,gc-heap-size,working-set]

# 2. Capture a memory dump for offline analysis
kubectl exec deploy/his-hope-{service} -n his-hope -- \
  dotnet-dump collect -p 1 -o /tmp/crash-$(date +%s).dmp

# 3. Copy the dump locally
kubectl cp his-hope-{service}-{hash}:/tmp/crash-1234567890.dmp ./crash.dmp -n his-hope

# 4. Analyze the dump locally
dotnet-dump analyze ./crash.dmp
# In the REPL:
#   > dumpheap -stat
#   > gcroot {object-address}
```

### Step 3 — If Node-Level Memory Pressure

```bash
# Cordon the node to prevent further scheduling
kubectl cordon {node-name}

# Optionally drain it
kubectl drain {node-name} --ignore-daemonsets --delete-local-data

# Check what else is running on that node that consumes memory
kubectl get pods -n his-hope --field-selector spec.nodeName={node-name} -o wide
```

## Resolution

### Root Cause Fix

| Cause | Memory Pattern | Fix |
|---|---|---|
| **Large dataset loaded in memory** | Heap grows, then stabilizes at high level | Add pagination; stream data instead of loading all at once |
| **Event/message accumulation** | Memory grows over hours/days | Check `ConcurrentQueue<T>` or `Channel<T>` unbounded growth; add backpressure |
| **Memory leak (static/DI)** | Heap grows monotonically, never GCs | Check static collections, event handler subscriptions, `IHostedService` dispose |
| **Cache without eviction** | Memory climbs to limit, then stays | Set `MemoryCacheOptions.SizeLimit` or `SlidingExpiration` |
| **Image/Render processing** | Spike per-request, memory not released | Call `Dispose()` on `Bitmap`, `MemoryStream`, `HttpResponseMessage` |
| **EF Core tracking** | Slow growth on query-heavy endpoints | Use `.AsNoTracking()` for read-only queries |
| **Node under-provisioned** | All pods on node show high memory | Add nodes to cluster; adjust resource quotas |

### Verification

```bash
# 1. No OOMKilled pods
kubectl get pods -n his-hope | findstr "OOMKilled"
# → Should return nothing

# 2. Memory usage stable below 80% for 30m
# Check Grafana: Memory panel shows flat line below limit

# 3. Pod restarts stable
kubectl get pods -n his-hope | findstr "{service}"
# RESTARTS column should show no increase

# 4. GC heap size healthy (.NET)
kubectl exec deploy/his-hope-{service} -n his-hope -- \
  curl -sf http://localhost:9464/metrics | findstr "dotnet_gc_heap_size_bytes"
# Should be well below memory limit

# 5. Load test
k6 run --vus 50 --duration 60s k6/scripts/{service}-memory-test.js
# Monitor: no OOM, memory < 80% limit
```

## Postmortem

Use the standard incident postmortem template at `docs/postmortem-template.md`.

Key metrics to capture:
- Memory usage graph (24h window) showing the ramp before OOM
- .NET GC heap size and gen-2 collection count
- Working set vs memory limit at time of crash
- Count of `timeout` or `503` errors during pod restart window
- Memory dump analysis results (largest object types)

---

> **Last updated**: 2026-07-17 | **Maintainer**: @dotnet | **Next review**: 2026-09-17
