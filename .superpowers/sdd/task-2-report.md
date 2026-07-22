## Task 2 Report: Per-Service SLO Recording Rules

**Status:** Complete
**Commit:** `cfbaeb4` — `feat(monitoring): add per-service SLO recording rules and burn rate alerts for patient/identity/appointment/clinical`
**File:** `k8s/monitoring/prometheus-rules.yaml` (+167 lines)

### What was done

Added new rule group `his-hope.slo.per-service-completion` with **28 recording rules** (7 per service) for the 4 services that previously lacked per-service SLO rules:

| Service | Job Label | Availability SLO | P99 Latency | Error Budget Target |
|---|---|---|---|---|
| patient-service | `patientservice` | 99.9% | < 500ms | 0.001 |
| identity-service | `identityservice` | 99.95% | < 300ms | 0.0005 |
| appointment-service | `appointmentservice` | 99.9% | < 1s | 0.001 |
| clinical-service | `clinicalservice` | 99.99% | < 500ms | 0.0001 |

Each service gets 7 recording rules:
1. `slo:availability:<svc>:ratio_30d`
2. `slo:availability:<svc>:ratio_7d`
3. `slo:availability:<svc>:ratio_1h`
4. `slo:latency_p99:<svc>:ratio_30d`
5. `slo:error_budget_remaining:<svc>`
6. `slo:burn_rate_1h:<svc>`
7. `slo:burn_rate_6h:<svc>`

Added **4 new burn rate alerts** to the existing `his-hope.slo.alerts` group:
- `SLOErrorBudgetBurnCritical_Patient`
- `SLOErrorBudgetBurnCritical_Identity`
- `SLOErrorBudgetBurnCritical_Appointment`
- `SLOErrorBudgetBurnCritical_Clinical`

Each fires when both 1h and 6h burn rates exceed 14.4x (critical threshold), with a 2m `for` duration.

### Verification

- YAML syntax validated with PyYAML
- Rule group count verified: 4 groups (recording_rules, per-service-completion, alerts, business-errors)
- Record/alert counts confirmed: 18 + 28 records, 12 + 5 alerts

### Concerns

None. The new group uses the existing metric patterns (`http_server_request_duration_seconds_count` with `http_response_status_code!~"5.."` and `histogram_quantile`) consistent with the task brief.
