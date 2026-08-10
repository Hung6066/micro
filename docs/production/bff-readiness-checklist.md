# BFF Production Readiness Checklist

## Security (PHASE 8.6)
- [ ] Cookie security audit passed (HttpOnly, Secure, SameSite)
- [ ] No JWT in browser responses
- [ ] CSRF enforced on all mutations
- [ ] CORS restricted to production domains
- [ ] Security headers present (HSTS, CSP, X-Frame-Options)
- [ ] Dependency scan: 0 critical/high CVEs
- [ ] No hardcoded secrets (all via Vault)
- [ ] Network policies enforce least-privilege
- [ ] Vault policies enforce least-privilege per BFF
- [ ] HIPAA audit trail active on all BFFs

## Observability (PHASE 8.1-8.2)
- [ ] Prometheus metrics exposed on all 7 BFFs
- [ ] Grafana dashboard deployed with real-time data
- [ ] SLO alerts configured (error budget, latency, session hit rate)
- [ ] Circuit breaker state alert active
- [ ] OpenTelemetry traces flowing to Jaeger

## Resilience (PHASE 8.3-8.4)
- [ ] k6 load test: 200 RPS sustained, p95 < 500ms
- [ ] k6 stress test: breaking point documented
- [ ] Chaos Mesh: Redis kill — circuit breaker verified
- [ ] Chaos Mesh: downstream latency — degradation verified
- [ ] Chaos Mesh: BFF pod kill — failover verified
- [ ] All Chaos experiments run and documented

## Deployment (PHASE 8.7)
- [ ] Canary TrafficSplit configured for all 7 BFFs
- [ ] Canary rollout runbook tested
- [ ] Rollback time < 30 seconds verified
- [ ] ArgoCD sync healthy on all BFF applications
- [ ] Tekton CI pipeline passes for all BFFs

## Operations
- [ ] Runbook drill completed (Redis failure scenario)
- [ ] On-call team briefed on BFF architecture
- [ ] Escalation path documented for BFF incidents
- [ ] Log retention configured (90d non-PHI, 6yr PHI)
- [ ] Backup verification: Redis RDB snapshots valid

## Documentation
- [ ] ADR-013: BFF Architecture — approved
- [ ] API docs: all BFF endpoints documented
- [ ] SLO document: targets published
- [ ] Security review document: filed
- [ ] Runbooks: canary rollout, incident response, DR

## Signoff
- [ ] Security team: ________________ Date: ______
- [ ] DevOps/SRE: ________________ Date: ______
- [ ] Engineering lead: ________________ Date: ______
