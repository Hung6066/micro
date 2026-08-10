# Failed Deployment / Rollback Runbook

| Field | Value |
|-------|-------|
| **Severity** | P0 (active deployment failure) to P2 (degraded rollout) |
| **Service** | Any His.Hope microservice |
| **Owner** | SRE Team, DevOps (@devops), Backend Developer (@dotnet) |
| **Last Updated** | 2026-07-17 |

## Alert Trigger

- `DeploymentFailed` — ArgoCD app synced to `Degraded` or `OutOfSync`
- `KubeRolloutStuck` — Rollout not progressing for > 5m
- `PodCrashLooping` — New pods failing readiness probes
- `HighErrorRateAfterDeploy` — Error rate > 5% on new revision (canary analysis fail)
- `LatencySpikeAfterDeploy` — p99 latency > 2x baseline on new revision
- `ArgoCDSyncFailed` — Unable to sync application state

## Symptoms

- **ArgoCD UI**: Application shows `Degraded` or `OutOfSync` status
- **Grafana**: Error rate or latency spikes immediately after deployment timestamp
- **Kibana**: New pod logs show `StartupException`, `ConfigurationException`, or `NullReferenceException`
- **Kubernetes**: `kubectl rollout status` hangs or shows `CrashLoopBackOff`
- **Linkerd**: `linkerd-viz stat` shows `success_rate < 100%` on new pods
- **Canary analysis**: Flagger/Smi metrics show failure threshold breached
- **Slack**: `#his-hope-deployments` channel shows `❌ Rollout failed` message

## Diagnosis

```bash
# 1. Check the deployment status
kubectl rollout status deploy/his-hope-{service} -n his-hope

# 2. Check deployment history
kubectl rollout history deploy/his-hope-{service} -n his-hope

# 3. Check pod status post-deploy
kubectl get pods -n his-hope | findstr "{service}"

# 4. Describe a failing pod
kubectl describe pod his-hope-{service}-{hash} -n his-hope

# 5. Check previous pod logs (the one that was running before deploy)
kubectl logs his-hope-{service}-{hash} -n his-hope --previous --tail=50

# 6. Check ArgoCD application status
argocd app get his-hope-{service} --grpc-web

# 7. Check canary analysis (if Flagger/Argo Rollouts)
kubectl get canary his-hope-{service} -n his-hope -o yaml

# 8. Check the diff between revisions
kubectl diff -f k8s/overlays/production/{service}-deployment.yaml

# 9. Check pod resource usage on new revision
kubectl top pods -n his-hope | findstr "{service}"

# 10. Check if the issue is configuration-related
kubectl get configmap his-hope-{service}-config -n his-hope -o yaml | grep -i "connectionstring\|endpoint\|url"
kubectl get secret his-hope-{service}-secrets -n his-hope -o jsonpath='{.data}' | python -c "import sys,json; d=json.load(sys.stdin); print({k:'<redacted>' for k in d.keys()})"
```

## Mitigation

### Step 1 — Rollback via kubectl (Immediate)

```bash
# Option A: Rollback to the previous revision
kubectl rollout undo deploy/his-hope-{service} -n his-hope

# Option B: Rollback to a specific revision
kubectl rollout undo deploy/his-hope-{service} -n his-hope --to-revision={N}

# Wait for the rollout to complete
kubectl rollout status deploy/his-hope-{service} -n his-hope --watch

# Verify pods are healthy
kubectl get pods -n his-hope | findstr "{service}"
```

### Step 2 — Rollback via ArgoCD

```bash
# Option A: Rollback via ArgoCD CLI
argocd app rollback his-hope-{service} {revision} --grpc-web

# Option B: Sync to a specific revision from Git
argocd app sync his-hope-{service} --revision {previous-commit-hash} --grpc-web

# Option C: Manually set the desired revision (if auto-sync is off)
argocd app set his-hope-{service} --revision {previous-commit-hash} --grpc-web
argocd app sync his-hope-{service} --grpc-web
```

### Step 3 — Rollback via Argo Rollouts (if using blue/green or canary)

```bash
# Abort the current canary/blue-green rollout
kubectl argo rollouts abort his-hope-{service} -n his-hope

# Promote the previous stable replica set
kubectl argo rollouts promote his-hope-{service} -n his-hope

# Or manually scale down new and scale up old
kubectl scale rs/his-hope-{service}-{new-hash} -n his-hope --replicas=0
kubectl scale rs/his-hope-{service}-{old-hash} -n his-hope --replicas=3
```

### Step 4 — Emergency: Direct YARP Gateway Rollback (if service is essential)

```bash
# Point API Gateway to the old deployment (bypass Kubernetes Service)
kubectl edit configmap yarp-config -n his-hope
# Change: http://his-hope-{service}:{port}/api/v1/
# To:     http://his-hope-{service}-v{N}:{port}/api/v1/  (if old version has different service name)

# Restart YARP to pick up config
kubectl rollout restart deploy/his-hope-yarp -n his-hope
```

## Resolution

### Root Cause Fix

```bash
# 1. Save the failing pod's logs for analysis
kubectl logs his-hope-{service}-{hash} -n his-hope --previous > deploy-failure-$(date -u +%Y%m%dT%H%M%S).log

# 2. Identify the root cause
```

| Cause | Symptom | Fix |
|---|---|---|
| **Missing or wrong ConfigMap/Secret** | `ConfigurationException` on startup | Verify `kubectl get configmap` and `kubectl get secret` exist; check key names |
| **DB migration not applied** | `DbUpgradeRequiredException` or FK errors | Run pending migrations; add migration as pre-deploy hook |
| **Breaking API change** | gRPC clients fail with `Unimplemented` or `NotFound` | Ensure backward-compatible proto changes; use interface segregation |
| **Missing environment variable** | `ArgumentNullException` at startup | Check `deployment.yaml` env section matches `appsettings.json` |
| **New dependency not deployed** | `Connection refused` to new service | Deploy new dependency first; update dependency order |
| **Image tag doesn't exist** | `ImagePullBackOff` | Verify image exists in registry: `docker manifest inspect {image}:{tag}` |
| **Resource limits too low** | `OOMKilled` or `CrashLoopBackOff` on startup | Increase memory/cpu limits; check if new code uses more memory |
| **Health probe too aggressive** | Pods restarting in loop; probe fails before app is ready | Increase `initialDelaySeconds` or `failureThreshold` |
| **Canary analysis fails** | Flagger/Argo Rollouts aborts deployment | Check metric thresholds; ensure baseline metrics exist |

### Verification

```bash
# 1. All pods Running and Ready
kubectl get pods -n his-hope | findstr "{service}" | findstr "Running"

# 2. Rollout status succeeded
kubectl rollout status deploy/his-hope-{service} -n his-hope
# → deployment "his-hope-{service}" successfully rolled out

# 3. Health endpoint responds
kubectl exec deploy/his-hope-{service} -n his-hope -- \
  curl -sf http://localhost:{port}/health/ready

# 4. Error rate back to baseline
# Grafana: Check error rate panel for the service

# 5. Traffic flowing through all endpoints
kubectl exec deploy/his-hope-identity-service -n his-hope -- \
  curl -sf http://his-hope-{service}:{port}/api/v1/health

# 6. ArgoCD sync status Healthy
argocd app get his-hope-{service} --grpc-web | findstr "Health Status"
# → Healthy

# 7. No canary/rollout in progress
kubectl get canary his-hope-{service} -n his-hope 2>/dev/null | findstr "Promoted"
```

## Postmortem

Use the standard incident postmortem template at `docs/postmortem-template.md`.

### Deployment Rollback Checklist

```
□ 1. Determine if this was a code, config, or infra change
□ 2. Check if the same issue exists in staging/pre-prod
□ 3. Verify CI/CD pipeline caught the failure (or why it didn't)
□ 4. Review PR for missing pre-deploy checklist items
□ 5. Add automated canary analysis if not present
□ 6. Update deployment runbook with lessons learned
```

### Key Metrics

- Time from deploy start to failure detection
- Time from detection to complete rollback
- Number of affected requests (p95 latency, error count during window)
- Whether the canary (if any) detected the failure before full rollout
- Git commit hash of the failed deploy

### Prevention

```
□ Add pre-deploy integration tests for ConfigMap/Secret validation
□ Implement progressive delivery: 1% → 10% → 50% → 100% with automated analysis
□ Add deployment freeze window during production hours
□ Ensure DB migrations are backward-compatible (add-only columns; no destructive changes)
□ Document breaking change detection in CI pipeline
```

---

> **Last updated**: 2026-07-17 | **Maintainer**: @devops | **Next review**: 2026-09-17
