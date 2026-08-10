# BFF Service Level Objectives

| SLI | Target | Window |
|-----|--------|--------|
| Availability | 99.9% | 30 days |
| p95 Latency | < 500ms | 30 days |
| p99 Latency | < 1000ms | 30 days |
| Session hit rate | > 99% | 5 min |
| Auth failure rate | < 0.1/s | 5 min |
| CSRF rejection rate | < 0.05/s | 5 min |
| Aggregation degraded rate | < 5% | 30 days |
| Circuit breaker open duration | < 30s | per event |

Error budget: 43m/month downtime (0.1%)
