# Chaos Mesh Experiments — His.Hope

## Overview
These Chaos Mesh experiments verify the resilience of the His.Hope test stack and services under failure conditions.

## Experiments

| Experiment | Type | Schedule | Duration | Purpose |
|-----------|------|----------|----------|---------|
| `network-delay` | NetworkChaos | Every 6h | 30s | Test timeout handling, retry logic |
| `pod-failure` | PodChaos | Every 12h | 60s | Test service discovery, circuit breakers |
| `db-partition` | NetworkChaos (partition) | Every 24h | 30s | Test DB connection pool, retry |

## Running Experiments

### Manual Trigger
```bash
kubectl apply -f k8s/chaos/network-delay.yaml
```

### Check Status
```bash
kubectl get chaos -n his-hope
kubectl describe networkchaos test-stack-network-delay -n his-hope
```

### Clean Up
```bash
kubectl delete -f k8s/chaos/
```

## Prerequisites
- Chaos Mesh installed in cluster
- `kubectl` configured for his-hope namespace
- Metrics collection via Prometheus for experiment analysis
