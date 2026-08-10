# Service Unavailable (Liveness Probe Failure / Pod Crash) Runbook

| Field | Value |
|-------|-------|
| **Severity** | P0 |
| **Service** | Any His.Hope microservice |
| **Owner** | SRE Team, Backend Developer (@dotnet) |
| **Last Updated** | 2026-07-17 |

## Alert Trigger

- `KubePodCrashLooping` — Pod restarting repeatedly
- `KubePodNotReady` — Pod not ready for > 5m
- `ServiceDown` — Prometheus target `up == 0`
- `HighErrorRate` — Error rate > 5% sustained (secondary indicator)
- `ProbeFailing` — Liveness or readiness probe failing

## Symptoms

- **PagerDuty** alert: `ServiceDown - {service} is DOWN`
- **Grafana**: `kube_pod_status_ready` drops to 0 for the service
- **Kibana**: Service logs stop appearing
- **Slack**: Users report 502/503 errors in the UI
- **Linkerd**: mTLS handshake failures on inbound service ports
- **Jaeger**: Traces missing for the service; spans from upstream show `UNAVAILABLE`

## Diagnosis

```bash
# 1. Check pod status
kubectl get pods -n his-hope | findstr "{service}"

# 2. Describe failing pod
kubectl describe pod his-hope-{service}-{hash} -n his-hope

# 3. Check crash reason (OOMKilled, Error, ExitCode)
kubectl get pod his-hope-{service}-{hash} -n his-hope -o jsonpath='{.status.containerStatuses[0].lastState.terminated}'

# 4. View logs from the crashed instance
kubectl logs his-hope-{service}-{hash} -n his-hope --previous --tail=50

# 5. Check pod events
kubectl get events -n his-hope --sort-by='.lastTimestamp' | findstr "{service}"

# 6. Check resource usage pre-crash
kubectl top pod his-hope-{service}-{hash} -n his-hope

# 7. Check readiness probe configuration
kubectl get pod his-hope-{service}-{hash} -n his-hope -o json | \
  python -c "import sys,json; pod=json.load(sys.stdin); c=pod['spec']['containers'][0]; print(c.get('readinessProbe', {})); print(c.get('livenessProbe', {}))"

# 8. Verify upstream service dependencies
kubectl exec deploy/his-hope-{upstream-service} -n his-hope -- \
  curl -sf http://his-hope-{service}:{port}/health/ready
# If curl fails, the service is genuinely unreachable

# 9. Check service endpoints
kubectl get endpoints his-hope-{service} -n his-hope

# 10. Check Linkerd metrics
linkerd viz stat deploy/his-hope-{service} -n his-hope
```

## Mitigation

### Step 1 — Restore Availability (Immediate)

```bash
# Option A: Scale down to 0, then scale back up (forces fresh pods)
kubectl scale deploy/his-hope-{service} -n his-hope --replicas=0
sleep 10
kubectl scale deploy/his-hope-{service} -n his-hope --replicas=3

# Option B: Delete the stuck pod (if it's a single bad pod)
kubectl delete pod his-hope-{service}-{hash} -n his-hope --force --grace-period=0
```

### Step 2 — Rollback if Recent Deployment

```bash
# Check rollout history
kubectl rollout history deploy/his-hope-{service} -n his-hope

# Rollback to the previous revision
kubectl rollout undo deploy/his-hope-{service} -n his-hope

# Wait for rollout to complete
kubectl rollout status deploy/his-hope-{service} -n his-hope --watch
```

### Step 3 — Bypass Bad Node (if node-level issue)

```bash
# Taint the bad node to stop scheduling
kubectl taint nodes {node-name} his-hope/out-of-service=true:NoExecute

# Or cordon it entirely
kubectl cordon {node-name}
```

### Step 4 — Route Around the Service (if partial outage)

```bash
# Temporarily disable the service in YARP Gateway
kubectl edit configmap yarp-config -n his-hope
# Remove or comment out the failing upstream

# Reload YARP
kubectl rollout restart deploy/his-hope-yarp -n his-hope
```

## Resolution

### Root Cause Fix

| Cause | Fix |
|---|---|
| **OOMKilled** | Increase memory limit in deployment manifest; investigate memory leak |
| **Startup exception** | Fix the unhandled exception; add robust startup health checks |
| **Config missing** | Check ConfigMap and Secret existence; fix config mount paths |
| **DB connection failure on startup** | Add retry logic in `Program.cs` for DB connection; check DB pod status |
| **gRPC dependency not ready** | Configure startup dependency checks; use `WaitForStartup` pattern |
| **ImagePullBackOff** | Fix image tag, registry credentials, or pull policy |
| **CrashLoopBackOff — probe timeout** | Increase `initialDelaySeconds` or `timeoutSeconds` in probe config |
| **Sidecar container failure (Linkerd)** | Check `linkerd-await` sidecar; restart with `linkerd inject` |

### Verification

```bash
# 1. Pod is Running and Ready
kubectl get pods -n his-hope | findstr "{service}" | findstr "Running"

# 2. Health endpoints respond
kubectl exec deploy/his-hope-{service} -n his-hope -- \
  curl -sf http://localhost:{port}/health/ready

# 3. Liveness probe passing
kubectl get pod his-hope-{service}-{hash} -n his-hope -o jsonpath='{.status.containerStatuses[0].ready}'
# → true

# 4. Traffic flows
kubectl exec deploy/his-hope-identity-service -n his-hope -- \
  curl -sf http://his-hope-{service}:{port}/api/v1/health

# 5. No error rate in Grafana for 5 minutes
# Check: /d/service-overview/{service} → Error Rate panel

# 6. All replicas report Ready
kubectl get endpoints his-hope-{service} -n his-hope
```

## Postmortem

Use the standard incident postmortem template at `docs/postmortem-template.md`.

Key metrics to capture:
- Time from first probe failure to pod eviction (`kubectl get events`)
- Resource utilization (CPU/memory) prior to crash
- Startup duration vs probe `initialDelaySeconds`
- Whether HorizontalPodAutoscaler triggered any scaling action
- Linkerd metrics for `request_total` just before failure

---

> **Last updated**: 2026-07-17 | **Maintainer**: @sre | **Next review**: 2026-09-17
