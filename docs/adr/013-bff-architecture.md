# ADR 013: Backend-for-Frontend (BFF) Architecture

**Status**: Accepted

**Date**: 2026-07-21

## Context

The original architecture routed all frontend requests through a single YARP ApiGateway directly to backend microservices. This required the Angular SPA to manage JWT Bearer tokens, handle token refresh, and construct `Authorization` headers — complex client-side auth logic.

As the system grew to 7+ services, the frontend needed to aggregate data from multiple backends (e.g., patient timeline combining Patient, Clinical, Lab, and Pharmacy data). The gateway didn't support this aggregation, pushing orchestration to the frontend.

## Decision

Adopt a BFF (Backend-for-Frontend) pattern with YARP edge routing:

```
Angular SPA → ApiGateway (YARP) → BFF layer → Backend Services
                                        ↓
                                  Session Auth
                                  (HttpOnly cookie)
```

Each service domain gets a dedicated BFF that handles:
1. **Session-based auth** — HttpOnly cookie replaces Bearer token
2. **Reverse proxy** — Pass-through routes for CRUD operations (YARP via `AddBffProxy`)
3. **Data aggregation** — gRPC fan-out with partial degradation (via `IAggregationHandler`)

### BFF Modules

| BFF | Port | Type | Services |
|-----|------|------|----------|
| Identity (ApiGateway) | 5000 | Edge proxy | IdentityService |
| PatientBff | 5100 | Proxy + Aggregation | Patient, Clinical, Lab, Pharmacy |
| ClinicalBff | 5200 | Proxy + Aggregation | Clinical, Patient |
| LabBff | 5300 | Proxy only | Lab |
| BillingBff | 5400 | Proxy + Aggregation | Billing |
| PharmacyBff | 5500 | Proxy + Aggregation | Pharmacy, Clinical |
| DashboardBff | 5600 | Aggregation only | Patient, Clinical, Lab, Billing, Pharmacy, Appointment |

### Aggregation Pattern

All BFFs use `IAggregationHandler` with `ParallelAggregationExecutor` for concurrent fan-out:

```csharp
public interface IAggregationHandler
{
    string Route { get; }
    string Method { get; }
    Task<AggregationResult> HandleAsync(AggregationContext context);
}
```

Partial degradation: if one service fails, the BFF returns 200 with the remaining data plus a `degraded` array describing failures.

## Consequences

**Positive:**
- Frontend no longer manages JWT tokens — auth is entirely HttpOnly cookie-based
- Aggregation logic lives on the server, reducing frontend complexity
- Partial degradation improves resilience — one failing service doesn't break the page
- Each BFF can scale independently based on its traffic pattern
- gRPC internal communication is faster and type-safe

**Negative:**
- 6 additional services to deploy and monitor
- Added latency for pass-through requests (BFF adds ~3-5ms)
- Session state requires Redis cluster
- BFFs need access to all downstream gRPC services
