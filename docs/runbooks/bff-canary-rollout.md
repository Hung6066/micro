# BFF Canary Rollout Runbook

## Pre-Rollout Checklist
- [ ] All BFF metrics dashboards green
- [ ] No active P1/P2 incidents
- [ ] Release notes reviewed
- [ ] Rollback plan documented

## Canary Steps (per BFF module)
1. Deploy canary v2: `kubectl apply -f k8s/overlays/canary/{module}-bff.yaml`
2. Shift 10% traffic: update TrafficSplit weight to (v1:900m, v2:100m)
3. Observe 30 minutes:
   - Error rate < baseline + 1%
   - p95 latency < baseline + 50ms
   - Session hit rate > 98%
4. Shift 25%: (v1:750m, v2:250m), observe 30 min
5. Shift 50%: (v1:500m, v2:500m), observe 1 hour
6. Full rollout: (v2:1000m), observe 2 hours
7. Remove v1 deployment

## Rollback
```bash
# Immediate rollback — set v2 weight to 0
kubectl patch trafficsplit {module}-bff-canary --type='json' \
  -p='[{"op":"replace","path":"/spec/backends/1/weight","value":"0m"}]'
```

## Auto-Rollback Triggers
| Trigger | Threshold | Action |
|---------|-----------|--------|
| Error rate spike | > 1% above baseline | Auto-rollback via Prometheus alert → webhook |
| Latency spike | p95 > 2x baseline | Auto-rollback |
| Session failures | hit rate < 95% | Auto-rollback |
| Circuit breaker open | > 30s | Auto-rollback |
