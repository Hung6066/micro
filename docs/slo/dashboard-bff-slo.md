# Dashboard BFF — Service Level Objectives

| SLI | Target | Measurement Window |
|-----|--------|-------------------|
| Availability | 99.9% | 30-day rolling |
| Latency (p95) | < 1s | 5-minute window |
| Error rate | < 5% | 5-minute window |

**Error Budget:** 43.8 minutes/month downtime allowed.

**Alerting:**
- DashboardBffDown: critical, pages on-call
- DashboardBffHighErrorRate: warning, Slack notification
- DashboardBffHighLatency: warning, Slack notification
