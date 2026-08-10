# BFF Architecture — Operations Runbook

| Field | Value |
|-------|-------|
| **Service** | BFF Layer (6 modules) |
| **Owner** | Backend Team (@dotnet) |
| **Last Updated** | 2026-07-21 |

## Architecture Overview

```
                         ┌─────────────┐
                         │  Angular SPA │
                         └──────┬──────┘
                                │ Session cookie (HttpOnly)
                                ▼
                         ┌─────────────┐
                         │  ApiGateway  │  YARP reverse proxy
                         │  (:5000)     │
                         └──────┬──────┘
                    ┌───────────┼───────────┐
                    ▼           ▼           ▼
             ┌──────────┐ ┌──────────┐ ┌──────────┐
             │PatientBff│ │ClinicalBff│ │  LabBff  │
             │ (:5100)  │ │ (:5200)  │ │ (:5300)  │
             └────┬─────┘ └────┬─────┘ └────┬─────┘
                  │            │             │
             ┌────▼─────┐ ┌───▼──────┐ ┌───▼──────┐
             │BillingBff│ │PharmacyBff│ │Dashboard │
             │ (:5400)  │ │ (:5500)   │ │ (:5600)  │
             └──────────┘ └──────────┘ └──────────┘
                      │          │           │
                      ▼          ▼           ▼
              ┌───────────────────────────────┐
              │   Backend Services (gRPC)      │
              │ Patient, Clinical, Lab,        │
              │ Billing, Pharmacy, Appointment │
              └───────────────────────────────┘
```

Each BFF connects to Redis for session data and uses gRPC to communicate with backend services.

## Health Checks

All BFFs expose:
- `GET /health` — Liveness probe (always 200 if process is running)
- `GET /health/ready` — Readiness probe (checks Redis connectivity)

```bash
# Check all BFFs
for port in 5100 5200 5300 5400 5500 5600; do
  curl -s -o /dev/null -w "%{http_code}" http://localhost:$port/health
done
```

## Verification

### Verify BFF is routing correctly

```bash
# Check a BFF proxy route (replace patient-bff with appropriate BFF)
curl -s -b "hishop_sid=..." http://patient-bff:5100/api/v1/bff/patients/search | jq .

# Check an aggregation endpoint
curl -s -b "hishop_sid=..." http://dashboard-bff:5600/api/v1/dashboard/stats | jq .
```

### Verify degraded response handling

```bash
# If a downstream service is down, aggregation endpoints should return 200
# with a degraded array, not 502
curl -s -b "hishop_sid=..." http://dashboard-bff:5600/api/v1/dashboard/stats | jq '.degraded'
```

## Adding a New BFF

1. **Create project** — Copy existing BFF `.csproj`, reference `His.Hope.Bff.Core`
2. **Add gRPC clients** — Register in `Program.cs` via `AddGrpcClient<T>`
3. **Create handlers** — Implement `IAggregationHandler` for aggregation endpoints
4. **Add YARP proxy** — Call `services.AddBffProxy(config)` and `app.MapBffReverseProxy()` for pass-through routes
5. **K8s manifest** — Create `k8s/base/<bff-name>.yaml` (deployment + service on new port)
6. **Vault policy** — Create `vault/policies/<bff-name>.hcl`
7. **ApiGateway** — Add cluster + route in `src/ApiGateway/appsettings.json`
8. **Docker Compose** — Add service definition for local development

## Rollback Procedure

If a BFF experiences issues:

1. **Temporary**: Re-enable direct YARP routes in `src/ApiGateway/appsettings.json` and redeploy
2. **Permanent**: Revert the BFF migration PR and redeploy ApiGateway

```yaml
# Rollback: add direct route to PatientService
"patients": {
  "ClusterId": "patients",
  "Match": { "Path": "/api/v1/patients/{**catch-all}" }
}
```

## Monitoring

Key metrics:
- **BFF error rate** — Per-BFF HTTP 5xx rate
- **Aggregation degradation rate** — Endpoints returning partial data
- **gRPC client latency** — P99 latency for each downstream call
- **Redis session latency** — Session lookup time (should be < 5ms)

Grafana dashboard: `BFF Overview` (includes all 6 BFF modules)
