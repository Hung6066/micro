# Identity Service SLO Runbook

## SLO Targets

| Metric | Target | Alert Threshold |
|--------|--------|-----------------|
| Token issue P99 latency | < 50ms | > 100ms for 5min |
| Introspection P99 latency | < 5ms | > 10ms for 5min |
| Login success rate | > 99% | < 95% for 5min |
| Service availability | 99.9% | < 99% for 1min |

## Alerts

### TokenIssueLatencyHigh
- **Condition:** `histogram_quantile(0.99, rate(identity_tokens_issue_duration_ms_bucket[5m])) > 100`
- **Severity:** Warning
- **Runbook:** Check Vault connectivity, database connection pool, Redis latency

### LoginSuccessRateLow
- **Condition:** Login success rate < 95% for 5min
- **Severity:** Critical
- **Runbook:** Check for brute force attack (login_attempts table), Vault health, user lockout spikes

### IntrospectionLatencyHigh
- **Condition:** `histogram_quantile(0.99, rate(identity_introspection_duration_ms_bucket[5m])) > 10`
- **Severity:** Warning
- **Runbook:** Check Redis for token blacklist performance, DB connection pool

### VaultUnhealthy
- **Condition:** `healthcheck_status{name="vault-transit"} == 0`
- **Severity:** Critical
- **Runbook:** Verify Vault pod, check transit key, rotate if needed. Token signing will fail.
