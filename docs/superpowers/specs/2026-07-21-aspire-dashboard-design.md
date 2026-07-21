# Aspire-Like Dashboard for His.Hope

**Date:** 2026-07-21  
**Status:** Draft — Pending Implementation  
**Author:** Lead System Architect  
**Related ADRs:** ADR 013 (BFF Architecture), ADR 003 (Linkerd), ADR 004 (gRPC)

---

## 1. Overview

Build a standalone dashboard web application inspired by [.NET Aspire](https://aspire.dev/) to provide a unified view and control of all His.Hope microservices, databases, and infrastructure components across all environments (dev, staging, production).

### Goals
- **Unified visibility:** Single pane of glass showing all services, databases, and infrastructure health
- **Service lifecycle control:** Start/Stop/Restart services directly from the dashboard (dev + staging)
- **Observability integration:** Aggregate logs, traces, and metrics from the existing observability stack
- **Environment context:** Switch between dev/staging/production with appropriate permissions
- **Dev + Ops friendly:** Serve both developers debugging locally and SREs monitoring production

### Non-Goals (v1)
- No new database or persistent storage (all data from observability stack)
- No alerting configuration (use existing Alertmanager)
- No replacement for Grafana/Jaeger/Kibana (dashboard is a lightweight aggregator, not a full observability platform)
- No service deployment (CI/CD stays in Tekton/ArgoCD)

---

## 2. Architecture

### 2.1 System Context

```
┌──────────────────────────────────────────────────────────────────┐
│                    His.Hope Aspire Dashboard                      │
│                                                                   │
│  ┌──────────────────────┐     ┌──────────────────────────────┐   │
│  │  Dashboard UI        │     │  Backstage (existing)        │   │
│  │  Angular 19 SPA      │     │  Developer Portal            │   │
│  │  Port: 4201          │     │  Port: 7007                  │   │
│  └────────┬─────────────┘     └──────────────────────────────┘   │
│           │ REST + WebSocket                                       │
│  ┌────────▼────────────────────────────────────────────────────┐  │
│  │  SystemDashboard.Bff  (Port 5700)                            │  │
│  │                                                              │  │
│  │  ┌──────────────┐ ┌───────────┐ ┌──────────┐ ┌──────────┐  │  │
│  │  │ Resource     │ │ Logs      │ │ Traces   │ │ Metrics  │  │  │
│  │  │ Aggregator   │ │ Aggregator│ │ Aggregator│ │ Aggregator│ │  │
│  │  └──────┬───────┘ └─────┬─────┘ └────┬─────┘ └────┬─────┘  │  │
│  │  ┌──────▼──────────────────────────────────────────┐       │  │
│  │  │  Lifecycle Controller                            │       │  │
│  │  │  ┌──────────────────┐ ┌──────────────────────┐  │       │  │
│  │  │  │ Docker API       │ │ Kubernetes API       │  │       │  │
│  │  │  │ (local dev)      │ │ (staging/prod)       │  │       │  │
│  │  │  └──────────────────┘ └──────────────────────┘  │       │  │
│  │  └─────────────────────────────────────────────────┘       │  │
│  └────────┼──────────────┼────────────┼───────────┼──────────┘  │
│           │              │            │           │              │
│  ┌────────▼──┐ ┌────────▼──┐ ┌───────▼──┐ ┌─────▼──────────┐   │
│  │ Consul    │ │Elastic-   │ │ Jaeger   │ │ Prometheus     │   │
│  │ Discovery │ │search     │ │          │ │                │   │
│  │ :8500     │ │ :9200     │ │ :16686   │ │ :9090          │   │
│  └───────────┘ └───────────┘ └──────────┘ └────────────────┘   │
│                                                                   │
│  ┌────────────────────────────────────────────────────────────┐  │
│  │ 7 Microservices │ 7 BFFs │ 8 DBs │ Redis │ RabbitMQ │ ...  │  │
│  └────────────────────────────────────────────────────────────┘  │
└──────────────────────────────────────────────────────────────────┘
```

### 2.2 Technology Stack

| Component | Technology | Rationale |
|-----------|-----------|-----------|
| **Dashboard UI** | Angular 19 standalone components | Reuse existing design system, Material theme, and Angular patterns from main SPA |
| **Backend** | .NET 8 ASP.NET Core (BFF pattern) | Follow ADR 013, reuse infrastructure libs (Polly, OpenTelemetry, HealthChecks) |
| **Real-time logs** | WebSocket (SignalR) | Low latency log streaming without polling |
| **Service Discovery** | Consul | Already deployed, provides health checks + service registry |
| **Metrics** | Prometheus HTTP API | Existing Prometheus instance, no new infrastructure |
| **Logs** | Elasticsearch REST API | Existing ELK stack |
| **Traces** | Jaeger gRPC/HTTP API | Existing Jaeger deployment |
| **Lifecycle (dev)** | Docker Compose CLI | `docker-compose start/stop <service>` |
| **Lifecycle (staging/prod)** | Kubernetes API | `kubectl scale` / `kubectl rollout restart` |

### 2.3 Project Structure

```
src/
└── Bff/
    └── SystemDashboard.Bff/          # New BFF (port 5700)
        ├── Program.cs
        ├── Properties/
        │   └── launchSettings.json
        ├── Aggregators/
        │   ├── ResourceAggregator.cs         # Service/db/infra discovery + health
        │   ├── LogsAggregator.cs             # Structured logs from Elasticsearch
        │   ├── TracesAggregator.cs           # Distributed traces from Jaeger
        │   ├── MetricsAggregator.cs          # Metrics from Prometheus
        │   └── LifecycleController.cs        # Start/Stop/Restart services
        ├── Models/
        │   ├── Resource.cs                   # Service, Database, Infrastructure models
        │   ├── ServiceState.cs               # Running, Stopped, Degraded, Unknown
        │   ├── LogEntry.cs                   # Structured log entry
        │   ├── TraceSummary.cs               # Trace summary for list view
        │   ├── TraceDetail.cs                # Full trace with spans
        │   └── MetricsSnapshot.cs            # Metric data point
        ├── Hubs/
        │   └── LogStreamHub.cs               # SignalR hub for real-time logs
        └── Services/
            ├── IConsulDiscoveryService.cs    # Consul service discovery
            ├── IElasticsearchQueryService.cs # ES log queries
            ├── IJaegerQueryService.cs        # Jaeger trace queries
            ├── IPrometheusQueryService.cs    # Prometheus metric queries
            └── IServiceLifecycleService.cs   # Docker/K8s lifecycle operations

dashboard-app/                              # Separate Angular workspace
├── angular.json
├── package.json
├── src/
│   ├── app/
│   │   ├── core/
│   │   │   ├── services/                   # resource, logs, traces, metrics, lifecycle
│   │   │   ├── guards/                     # AuthGuard, EnvironmentGuard
│   │   │   └── models/                     # TypeScript interfaces
│   │   ├── features/
│   │   │   ├── resources/
│   │   │   │   ├── resource-list/          # Main resource grid
│   │   │   │   └── resource-detail/        # Service detail panel
│   │   │   ├── logs/
│   │   │   │   ├── log-list/               # Log table with filters
│   │   │   │   └── log-stream/             # Real-time log viewer
│   │   │   ├── traces/
│   │   │   │   ├── trace-list/             # Trace search results
│   │   │   │   └── trace-detail/           # Waterfall view
│   │   │   └── metrics/
│   │   │       ├── metrics-overview/       # Summary cards
│   │   │       └── metrics-chart/          # Time-series charts
│   │   └── shared/
│   │       ├── service-status-badge/       # Running/Stopped/Degraded badge
│   │       ├── environment-selector/       # Dev/Staging/Production dropdown
│   │       ├── log-level-filter/           # Error/Warn/Info/Debug filter
│   │       └── resource-card/              # Reusable resource card component
│   └── environments/
│       ├── environment.ts                  # Dev environment config
│       └── environment.prod.ts             # Production environment config
```

### 2.4 Configuration & Service Discovery

The BFF uses `appsettings.json` with environment-specific overrides:

```json
{
  "Environment": "Development",
  "Consul": {
    "Address": "http://localhost:8500"
  },
  "Elasticsearch": {
    "Url": "http://localhost:9200",
    "LogIndex": "his-hope-logs-*"
  },
  "Jaeger": {
    "QueryUrl": "http://localhost:16686"
  },
  "Prometheus": {
    "Url": "http://localhost:9090"
  },
  "Docker": {
    "ComposeProjectName": "his-hope",
    "SocketPath": "npipe:////./pipe/docker_engine"  // Windows; "/var/run/docker.sock" on Linux
  },
  "Kubernetes": {
    "Enabled": false,
    "Namespace": "his-hope"
  }
}
```

- **Local dev:** App reads from `appsettings.Development.json`. Docker lifecycle via compose CLI or Docker API.
- **Staging/Production:** App reads from `appsettings.Staging.json` / `appsettings.Production.json`. Kubernetes lifecycle via `kubectl`. Config values overridden by Kubernetes ConfigMap/Secrets at deploy time.
- Consul, ES, Jaeger, Prometheus endpoints are configurable per environment (different instances for dev vs prod).

### 2.5 Key Design Decisions

| Decision | Rationale |
|----------|-----------|
| No database for BFF | All data is ephemeral — sourced from observability stack. No persistence needed. |
| Separate Angular workspace | Avoids coupling with main hospital SPA. Different routing, auth, and deployment. |
| SignalR for log streaming | Native .NET real-time without additional infrastructure (vs raw WebSocket). |
| Consul for service discovery | Already deployed; provides DNS + HTTP health check API. |
| Direct Prometheus/ES/Jaeger queries | No intermediary — BFF translates API responses. Avoids data duplication. |
| Environment context via header/cookie | Simple `X-Environment` header or query param; no complex multi-tenancy in v1. |

---

## 3. API Design

### 3.1 REST Endpoints

All endpoints under `SystemDashboard.Bff` (port 5700), prefixed with `/api`.

#### Resources

| Method | Path | Description |
|--------|------|-------------|
| `GET` | `/api/resources` | List all resources (services, databases, infra) with status |
| `GET` | `/api/resources/{name}` | Get resource detail (env vars, endpoints, health checks) |
| `POST` | `/api/resources/{name}/start` | Start a stopped service |
| `POST` | `/api/resources/{name}/stop` | Stop a running service |
| `POST` | `/api/resources/{name}/restart` | Restart a service |

#### Logs

| Method | Path | Description |
|--------|------|-------------|
| `GET` | `/api/logs?service={name}&level={level}&from={iso}&size={n}&query={text}` | Query structured logs |
| `WS` | `/ws/logs/stream?service={name}&level={level}` | Real-time console log stream via SignalR |

#### Traces

| Method | Path | Description |
|--------|------|-------------|
| `GET` | `/api/traces?service={name}&from={iso}&to={iso}&minDuration={ms}&limit={n}` | Search traces |
| `GET` | `/api/traces/{traceId}` | Get trace detail with span waterfall |

#### Metrics

| Method | Path | Description |
|--------|------|-------------|
| `GET` | `/api/metrics/summary` | Overview metrics for all services |
| `GET` | `/api/metrics/{service}?metrics=cpu,memory,requests,errors&range={duration}` | Per-service metrics |

#### Environment

| Method | Path | Description |
|--------|------|-------------|
| `GET` | `/api/environment` | Get current environment context |
| `PUT` | `/api/environment` | Switch environment (validates permissions) |

### 3.2 Response Models

**Resource (service):**
```json
{
  "name": "PatientService",
  "type": "microservice",
  "displayName": "Patient Service",
  "state": "Running",
  "healthStatus": "Healthy",
  "httpEndpoint": "http://localhost:5002",
  "grpcEndpoint": "https://localhost:5006",
  "uptime": "2h 34m 12s",
  "replicas": 1,
  "cpuPercent": 12.4,
  "memoryUsedMb": 256,
  "memoryLimitMb": 512,
  "databases": ["patientdb"],
  "environment": {
    "ASPNETCORE_ENVIRONMENT": "Development",
    "DOTNET_VERSION": "8.0"
  }
}
```

**Resource (database):**
```json
{
  "name": "patientdb",
  "type": "database",
  "engine": "CockroachDB",
  "state": "Running",
  "endpoint": "localhost:26257",
  "sizeMb": 45,
  "connections": 8
}
```

**Resource (infrastructure):**
```json
{
  "name": "Redis",
  "type": "infrastructure",
  "category": "cache",
  "state": "Running",
  "endpoint": "localhost:6379",
  "version": "7.0"
}
```

**LogEntry:**
```json
{
  "timestamp": "2026-07-21T08:30:00Z",
  "level": "Error",
  "service": "PatientService",
  "message": "Failed to connect to patientdb: connection refused",
  "traceId": "a1b2c3d4e5f6",
  "fields": {
    "exception": "Npgsql.NpgsqlException",
    "stackTrace": "..."
  }
}
```

---

## 4. Data Flow

### 4.1 Resource Discovery & Health

```
Angular UI ──GET /api/resources──▶ SystemDashboard.Bff
                                       │
                                       ├──▶ Consul /v1/catalog/services
                                       │    (service names, tags, ports)
                                       │
                                       ├──▶ Consul /v1/health/service/{name}
                                       │    (health check status, passing/warning/critical)
                                       │
                                       ├──▶ Docker /containers/json
                                       │    (infrastructure containers: redis, rabbitmq, es)
                                       │
                                       └──▶ Direct gRPC health probe (optional fallback)
                                            (gRPC HealthCheck protocol)
```

### 4.2 Logs (Structured + Streaming)

```
Angular UI ──GET /api/logs──▶ SystemDashboard.Bff
                                  │
                                  └──▶ Elasticsearch POST /_search
                                       {
                                         "query": {
                                           "bool": {
                                             "must": [
                                               {"match": {"service": "PatientService"}},
                                               {"match": {"level": "Error"}}
                                             ]
                                           }
                                         },
                                         "sort": [{"@timestamp": "desc"}],
                                         "size": 100
                                       }

Angular UI ──WebSocket──▶ SignalR Hub ──▶ Docker logs -f <container>
                                            (local dev only — console streaming)
```

### 4.3 Traces

```
Angular UI ──GET /api/traces──▶ SystemDashboard.Bff
                                    │
                                    └──▶ Jaeger GET /api/traces?service=PatientService&limit=20
                                         Jaeger GET /api/traces/{traceId}
```

### 4.4 Metrics

```
Angular UI ──GET /api/metrics──▶ SystemDashboard.Bff
                                     │
                                     └──▶ Prometheus GET /api/v1/query_range
                                          ?query=rate(http_requests_total[5m])
                                          &start=&end=&step=15s
```

### 4.5 Service Lifecycle

```
Angular UI ──POST /api/resources/{name}/stop──▶ SystemDashboard.Bff
                                                     │
                          ┌──────────────────────────┘
                          │
               ┌──────────▼──────────┐
               │ Environment = dev?  │
               └──────────┬──────────┘
                    Yes    │    No (staging/prod)
               ┌──────────▼──────────┐
               │ docker-compose       │    │ kubectl scale deploy/    │
               │ stop patient-service │    │ patient-service          │
               │                      │    │ --replicas=0            │
               └──────────────────────┘    └─────────────────────────┘
```

---

## 5. UI Design

### 5.1 Layout

```
┌──────────────────────────────────────────────────────────────────┐
│  ☰ His.Hope Dashboard    ─────────────── [dev ▼]  👤 operator    │
├────────────┬─────────────────────────────────────────────────────┤
│            │  ┌──────────────────────────────────────────────┐   │
│  📦        │  │  Resources                         12 total   │   │
│  Resources │  │  ┌──────────────────┐ ┌──────────────────┐   │   │
│            │  │  │ ● PatientService  │ │ ● AppointmentSvc │   │   │
│  📋        │  │  │   Running :5002  │ │   Running :5004  │   │   │
│  Logs      │  │  │   CPU 12% 256MB  │ │   CPU 8%  180MB  │   │   │
│            │  │  │   [Stop][Restart]│ │   [Stop][Restart]│   │   │
│  🔍        │  │  └──────────────────┘ └──────────────────┘   │   │
│  Traces    │  │  ┌──────────────────┐ ┌──────────────────┐   │   │
│            │  │  │ ○ PharmacySvc    │ │ 🗄 patientdb     │   │   │
│  📊        │  │  │   Stopped :5030  │ │   Running 8 conn │   │   │
│  Metrics   │  │  │   [Start]        │ │   Size: 45MB     │   │   │
│            │  │  └──────────────────┘ └──────────────────┘   │   │
│            │  │  ┌──────────────────┐ ┌──────────────────┐   │   │
│            │  │  │ ⚡ Redis         │ │ 🐇 RabbitMQ      │   │   │
│            │  │  │   Running :6379  │ │   Running :5672  │   │   │
│            │  │  └──────────────────┘ └──────────────────┘   │   │
│            │  └──────────────────────────────────────────────┘   │
└────────────┴─────────────────────────────────────────────────────┘
```

### 5.2 Pages

| Page | Content |
|------|---------|
| **Resources** (home) | Grid of resource cards grouped by: Services / Databases / Infrastructure. Each card shows status badge, port, mini metrics. Click opens detail drawer with env vars, endpoints, health check results. |
| **Logs** | Filter bar (service, level, time range, free text). Log table with color-coded levels. Toggle for real-time stream. Click row expands JSON payload. |
| **Traces** | Filter bar (service, time range, min duration). Trace list table. Click opens waterfall view showing spans with service, duration, status. |
| **Metrics** | Service multi-selector. Metric type picker (CPU, Memory, Requests, Errors, Latency). Time range selector. Line/area charts. |

### 5.3 Environment Switcher

- Dropdown in top toolbar: `Development` / `Staging` / `Production`
- Production shows red warning badge
- Switching reloads all data from target environment
- Production actions (Start/Stop) require confirmation dialog + audit log

### 5.4 Service Cards — States

| State | Visual |
|-------|--------|
| **Running** | Green dot + green border, actions: [Stop] [Restart] [View Logs] |
| **Stopped** | Gray dot + gray border, action: [Start] |
| **Degraded** | Yellow dot + yellow border (some health checks failing), actions: [Restart] [View Logs] |
| **Unknown** | Red dot + red border (unreachable), action: [View Logs] |

---

## 6. Security & Permissions

### 6.1 Authentication
- Reuse the same JWT auth mechanism from IdentityService
- Dashboard BFF validates JWT on each request
- Same login flow as main SPA (redirect to IdentityService login)

### 6.2 Authorization Matrix

| Role | Dev Env | Staging Env | Production Env |
|------|---------|-------------|----------------|
| **Developer** | View all, Start/Stop/Restart all | View all, Restart only | View all (read-only) |
| **Operator/SRE** | View all, Start/Stop/Restart all | View all, Start/Stop/Restart all | View all, Start/Stop/Restart (with confirmation) |
| **Admin** | Full access | Full access | Full access |
| **Viewer** | View all (read-only) | View all (read-only) | View all (read-only) |

### 6.3 Production Safeguards
- All production mutations require explicit confirmation dialog
- Production Stop requires reason text field
- All lifecycle actions are audit logged to Elasticsearch
- Rate limiting on lifecycle endpoints (max 5 actions/min in production)

---

## 7. Testing Strategy

| Layer | Tool | What |
|-------|------|------|
| **BFF unit** | xUnit + NSubstitute | Aggregator logic, model mapping, lifecycle controller |
| **BFF integration** | xUnit + Testcontainers | Elasticsearch query, Prometheus query, Docker API integration |
| **Angular unit** | Jasmine/Karma | Component rendering, service mocking, state transitions |
| **Angular component** | Cypress Component Testing | Resource cards, log table, trace waterfall |
| **E2E** | Playwright | Full user journey: login → view resources → filter logs → view trace → check metrics → stop/start service |
| **Contract** | buf + gRPC contract tests | BFF ↔ services communication contracts |

---

## 8. Deployment

### 8.1 Container

```
FROM mcr.microsoft.com/dotnet/aspnet:8.0-noble-chiseled AS base
WORKDIR /app
EXPOSE 5700

# Distroless per ADR 007
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "SystemDashboard.Bff.dll"]
```

### 8.2 Kubernetes

New deployment in `k8s/base/`:
```yaml
apiVersion: apps/v1
kind: Deployment
metadata:
  name: systemdashboard-bff
spec:
  replicas: 1
  selector:
    matchLabels:
      app: systemdashboard-bff
  template:
    spec:
      containers:
        - name: bff
          image: hishope/systemdashboard-bff:latest
          ports:
            - containerPort: 5700
          env:
            - name: ASPNETCORE_ENVIRONMENT
              valueFrom:
                fieldRef:
                  fieldPath: metadata.labels['environment']
```

### 8.3 Angular App

- Deploy as static files via Nginx container (port 4201)
- Separate from main SPA (port 4200)
- Configurable BFF endpoint via environment variables at runtime

---

## 9. Monitoring the Dashboard

The dashboard itself should be observable:
- Health check endpoint: `GET /health`
- Prometheus metrics: `GET /metrics`
- OpenTelemetry tracing integrated
- Logs to Elasticsearch

---

## 10. Scope Boundaries

### In Scope (v1)
- Resource list with health status for all 7 services, 7 BFFs, 8 databases, Redis, RabbitMQ
- Service Start/Stop/Restart (dev + staging via Docker/K8s)
- Structured log viewer with Elasticsearch backend
- Real-time log streaming via SignalR (dev only)
- Trace search and detail view via Jaeger
- Basic metrics (CPU, memory, request rate) via Prometheus
- Environment switching (dev/staging/production)
- RBAC with permission matrix

### Out of Scope (future)
- Console logs in staging/production (security concern — use Kibana directly)
- Grafana-level custom dashboards (link out to Grafana instead)
- Service deployment or CI/CD actions
- Configuration editing
- Database query console
- Alert configuration

---

## 11. Risks & Mitigations

| Risk | Mitigation |
|------|-----------|
| **Observability stack dependency** | If Prometheus/ES/Jaeger are down, dashboard shows "Unavailable" state for those views but Resources still work via Consul |
| **Production lifecycle safety** | Confirmation dialogs, rate limiting, audit logging, permission matrix |
| **Log stream performance** | Limit to 1 concurrent stream per user, max 100 lines/sec buffering |
| **Angular app independence** | Separate workspace prevents accidental changes to main SPA |
| **Consul as SPOF for discovery** | Fallback to direct gRPC health probes if Consul unreachable |

---

## 12. Appendix: Service Inventory

All services to be displayed in the dashboard:

| # | Name | Type | HTTP | gRPC | Database |
|---|------|------|------|------|----------|
| 1 | API Gateway | gateway | 5000 | — | — |
| 2 | IdentityService | service | 5001 | 5007 | identitydb |
| 3 | PatientService | service | 5002 | 5006 | patientdb |
| 4 | AppointmentService | service | 5004 | 5008 | appointmentdb |
| 5 | ClinicalService | service | 5005 | 5009 | clinicaldb |
| 6 | LabService | service | 5010 | — | labdb |
| 7 | BillingService | service | 5020 | — | billingdb |
| 8 | PharmacyService | service | 5030 | — | pharmacydb |
| 9 | PatientBff | bff | 5100 | — | — |
| 10 | ClinicalBff | bff | 5200 | — | — |
| 11 | LabBff | bff | 5300 | — | — |
| 12 | BillingBff | bff | 5400 | — | — |
| 13 | PharmacyBff | bff | 5500 | — | — |
| 14 | DashboardBff | bff | 5600 | — | — |
| 15 | AgentHarness | infrastructure | 5200 | — | harnessdb |
| 16 | Redis | infrastructure | 6379 | — | — |
| 17 | RabbitMQ | infrastructure | 5672 | — | — |
| 18 | Elasticsearch | infrastructure | 9200 | — | — |
| 19 | FhirGateway | service | TBD | — | — |
