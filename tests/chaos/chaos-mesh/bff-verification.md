# BFF Chaos Mesh Experiments — Verification Matrix

## Overview

This document defines the expected behavior for each BFF chaos experiment.
Run these experiments against staging (not production). Verify results using:
- Prometheus metrics (`bff_*` series)
- Alertmanager alerts
- BFF application logs
- k6 load test results (pre/post experiment)

## Experiment 1: Redis Kill

| Aspect | Expected Behavior |
|--------|------------------|
| **What happens** | Redis pod is killed for 30s |
| **BFF response** | Session-dependent requests return **503 Service Unavailable** |
| **Circuit breaker** | Opens within 5–10s of Redis becoming unreachable |
| **Angular behavior** | Retries with exponential backoff (up to 3 attempts) |
| **Alert** | `BffCircuitBreakerOpen` fires within 10s |
| **After recovery** | Circuit breaker closes within 15s of Redis being healthy |
| **Data loss** | None — sessions are ephemeral, re-created on next login |
| **k6 verification** | Run `bff-load.js` during experiment: expect spike in 503s, then recovery |

### Verification Steps

```bash
# 1. Apply the experiment
kubectl apply -f tests/chaos/chaos-mesh/bff-experiments.yaml -l chaos-mesh/target=redis

# 2. Run k6 load test in parallel
k6 run tests/load/k6/bff-load.js --duration 60s

# 3. Check alerts
kubectl get alertmanager -n his-hope | grep BffCircuitBreakerOpen

# 4. Verify circuit breaker metrics
curl -s http://localhost:5000/metrics | grep bff_circuit_breaker

# 5. Cleanup
kubectl delete -f tests/chaos/chaos-mesh/bff-experiments.yaml -l chaos-mesh/target=redis
```

## Experiment 2: Downstream Latency (5s)

| Aspect | Expected Behavior |
|--------|------------------|
| **What happens** | 5000ms + 1000ms jitter latency on all backend service pods |
| **BFF response** | Returns **200 OK with `X-BFF-Degraded: true`** header for aggregation endpoints |
| **Timeout handling** | Requests exceeding BFF client timeout return **504 Gateway Timeout** |
| **Circuit breaker** | Per-downstream circuit breakers may open for slow services |
| **Alert** | `BffHighLatency` fires when p(99) exceeds 2s threshold |
| **After recovery** | Latency normalizes within 30s of experiment end |
| **Data loss** | None — requests complete eventually or timeout cleanly |

### Verification Steps

```bash
# 1. Apply the experiment
kubectl apply -f tests/chaos/chaos-mesh/bff-experiments.yaml -l chaos-mesh/target=backend

# 2. Run k6 load test
k6 run tests/load/k6/bff-load.js --duration 90s

# 3. Check degraded header
curl -sI http://localhost:5000/api/v1/patients/search?q=test | grep X-BFF-Degraded

# 4. Verify latency metrics
kubectl exec -n his-hope prometheus-0 -- wget -qO- 'http://localhost:9090/api/v1/query?query=bff_duration{p95>2000}'

# 5. Cleanup
kubectl delete -f tests/chaos/chaos-mesh/bff-experiments.yaml -l chaos-mesh/target=backend
```

## Experiment 3: Packet Loss (30%)

| Aspect | Expected Behavior |
|--------|------------------|
| **What happens** | 30% packet loss with 50% correlation on all BFF pods |
| **BFF response** | Most requests succeed after retries; some return degraded |
| **Retry behavior** | HTTP client retries up to 3 times with 100ms backoff |
| **Alert** | **None expected** if retry logic handles gracefully |
| **After recovery** | Immediate normalization |
| **Data loss** | None — retry logic ensures eventual delivery |

### Verification Steps

```bash
# 1. Apply the experiment
kubectl apply -f tests/chaos/chaos-mesh/bff-experiments.yaml -l chaos-mesh/target=bff

# 2. Monitor retry counts
curl -s http://localhost:5000/metrics | grep bff_retry_count

# 3. Run spike test to verify resilience under load + packet loss
k6 run tests/load/k6/spike-test.js --duration 60s

# 4. Check error rate stayed below 1%
k6 run tests/load/k6/bff-load.js --duration 60s --summary-export results.json

# 5. Cleanup
kubectl delete -f tests/chaos/chaos-mesh/bff-experiments.yaml -l chaos-mesh/target=bff
```

## Experiment 4: BFF Pod Kill

| Aspect | Expected Behavior |
|--------|------------------|
| **What happens** | One patient-bff pod is killed |
| **BFF response** | Other replicas serve traffic — **no impact on API availability** |
| **Service mesh** | Linkerd reroutes traffic to remaining replicas seamlessly |
| **Alert** | **None** — PodRestart counter increments but no PagerDuty alert |
| **After recovery** | Kubernetes reschedules the killed pod within 15–30s |
| **Data loss** | None — in-flight requests fail and are retried by Angular |

### Verification Steps

```bash
# 1. Apply the experiment
kubectl apply -f tests/chaos/chaos-mesh/bff-experiments.yaml -l chaos-mesh/target=patient-bff

# 2. Verify no downtime during experiment
k6 run tests/load/k6/bff-load.js --duration 30s --summary-export results.json

# 3. Check pod status
kubectl get pods -n his-hope -l app.kubernetes.io/name=patient-bff

# 4. Verify Linkerd reroute
kubectl exec -n his-hope deploy/patient-bff -- cat /tmp/linkerd-proxy.log | grep rerouted

# 5. Cleanup
kubectl delete -f tests/chaos/chaos-mesh/bff-experiments.yaml -l chaos-mesh/target=patient-bff
```

## Summary Table

| Experiment | Duration | Expected HTTP Status | Alert Expected | Data Loss Risk |
|-----------|----------|---------------------|----------------|----------------|
| Redis kill (30s) | 30s | 503 for session requests | `BffCircuitBreakerOpen` | None (ephemeral) |
| Downstream 5s latency | 60s | 200+degraded / 504 timeout | `BffHighLatency` | None |
| 30% packet loss | 45s | 200 (after retries) | None (if retry works) | None |
| BFF pod kill | 10s | 200 (other replicas) | None (Linkerd reroutes) | None (retried) |

## Running the Full Suite

```bash
# Sequential run — each experiment waits for recovery before next
for exp in redis backend bff patient-bff; do
  echo "=== Running experiment: $exp ==="
  kubectl apply -f tests/chaos/chaos-mesh/bff-experiments.yaml -l chaos-mesh/target=$exp
  sleep 5
  k6 run tests/load/k6/bff-load.js --duration 30s --summary-export results-$exp.json
  kubectl delete -f tests/chaos/chaos-mesh/bff-experiments.yaml -l chaos-mesh/target=$exp
  echo "=== Recovery window (30s) ==="
  sleep 30
done

echo "All experiments complete. Check results-*.json for pass/fail."
```
