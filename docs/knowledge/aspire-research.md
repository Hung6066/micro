# .NET Aspire vs. His.Hope Monitoring Stack — Research Analysis

**Date:** 2026-07-22  
**Sources:** [Microsoft Aspire Docs](https://learn.microsoft.com/en-us/dotnet/aspire/), [GitHub microsoft/aspire](https://github.com/microsoft/aspire) (v13.4.6), [NuGet aspire profile](https://www.nuget.org/profiles/aspire), [aspirate (aspirational-manifests)](https://github.com/prom3theu5/aspirational-manifests)

---

## Executive Summary

.NET Aspire (v13.4.6, GA) is a **code-first development orchestration and observability layer** — not a production monitoring platform. It shines for **local dev experience** (one-command startup, unified dashboard, auto-OTel) and **deployment artifact generation** (K8s manifests via aspirate). It does **not** replace Prometheus/Grafana/Jaeger/ES for production observability, but its **standalone dashboard** is a compelling lightweight alternative for OTLP visualization. The incremental adoption path is strong: start with AppHost for local dev, optionally adopt the dashboard as a secondary OTLP viewer, keep existing production monitoring untouched.

---

## 1. Aspire Dashboard — Capabilities & Comparison

### What it shows out of the box:
- **Resources page:** All services, databases, containers with status (Running/Stopped/Health), endpoints, environment variables
- **Structured Logs:** Real-time log streaming with filtering by resource, log level, trace context
- **Traces:** Distributed trace viewer with span details, waterfall diagrams
- **Metrics:** Built-in metrics browser (CPU, memory, request rates, custom metrics via OTel)
- **Console Logs:** Raw stdout/stderr from all resources
- **Health Checks:** Visual status of `/health` and `/alive` endpoints per resource
- **Resource Actions:** Start/Stop/Restart resources from the UI (dev mode only)

### Architecture:
- Frontend (React) communicates with resource server over gRPC
- OTLP endpoint receives traces/metrics/logs from all services
- Dashboard runs embedded in AppHost during dev; runs standalone in production
- **Standalone mode:** Docker image or `aspire dashboard` CLI — any app sending OTLP data can use it

### Authentication:
- **Token-based auth** for the standalone dashboard
- OTLP endpoint can require API keys
- Documents explicitly warn about sensitive data in telemetry (env vars, secrets)

### Can it replace SystemDashboard.Bff + Angular dashboard-app?

| Feature | Custom Stack | Aspire Dashboard |
|---------|-------------|------------------|
| Resource overview | SystemDashboard.Bff aggregates from Consul/ES | Built-in resource state from AppHost model |
| Logs | ES/Kibana | Structured logs + console logs |
| Traces | Jaeger | Built-in trace viewer |
| Metrics | Prometheus/Grafana | Built-in metrics browser (basic) |
| Custom dashboards | Grafana (rich) | None (no custom panels) |
| Alerting | Prometheus AlertManager | None |
| Multi-tenant | Custom Bff supports it | Not designed for multi-tenant |
| Auth | Custom (Keycloak/JWT) | Token-based (basic) |
| Historical data | ES long-term retention | In-memory, no persistence |
| Production-grade | Yes | Dev-focused, standalone mode is new |

**Verdict: NOT a replacement.** The Aspire dashboard is a development/debugging tool. It lacks alerting, custom dashboards, long-term retention, and multi-tenant support that a hospital system needs. Its standalone mode is useful as a **secondary lightweight OTLP viewer** but Grafana + Kibana + Jaeger remain necessary for production.

---

## 2. OpenTelemetry Integration

### What Aspire provides:
- `AddServiceDefaults()` auto-configures:
  - OpenTelemetry SDK with OTLP exporter (sends to Aspire dashboard)
  - All ASP.NET Core instrumentation (HTTP, gRPC, EF Core)
  - Health checks endpoints (`/health`, `/alive`)
  - Service discovery configuration
  - Default resilience (retries, timeouts)
- Zero-config: traces, metrics, logs flow automatically to the dashboard
- OTLP/gRPC protocol to dashboard's OTLP endpoint

### Comparison with current OTel → Jaeger → Prometheus → ES pipeline:

| Aspect | Current Stack | Aspire OTel |
|--------|--------------|-------------|
| Setup | Manual OTel SDK + exporters config | `AddServiceDefaults()` — one call |
| Traces backend | Jaeger (dedicated) | Aspire Dashboard (dev) / any OTLP backend (prod) |
| Metrics backend | Prometheus (pull) | Dashboard (push) / any OTLP backend |
| Logs backend | Elasticsearch | Dashboard structured logs |
| Custom exporters | Manual config for each | Default is dashboard; configure additional exporters |
| Production | Battle-tested pipeline | Dashboard is not; but OTel protocol is standard |

**Verdict: AUGMENT.** Aspire's `AddServiceDefaults()` simplifies OTel setup dramatically. The services can continue exporting to Prometheus/Jaeger/ES by adding additional OTLP exporters. The Aspire dashboard becomes a convenient **local dev viewer** for OTel data that's still routed to the production pipeline.

---

## 3. Service Discovery — Can it Replace Consul?

### How Aspire service discovery works:
- **Code-first:** Resources reference each other via `WithReference()` in AppHost
- **Dev mode:** DCP (Developer Control Plane) injects connection strings as environment variables with actual container IPs/ports
- **Production:** Connection strings come from configuration (appsettings, secrets, env vars) — Aspire generates the right config for each environment
- Service discovery is primarily a **development convenience**, not a runtime service registry

### Consul features Aspire lacks:
- **DNS-based discovery** — Consul provides DNS SRV records; Aspire uses env var injection
- **Health checking with deregistration** — Consul actively health-checks and removes unhealthy instances
- **KV store** — Consul has a distributed KV store used for dynamic configuration
- **Service mesh integration** — Consul Connect provides mTLS and traffic policies
- **Multi-datacenter** — Consul supports WAN-joined clusters
- **Runtime discovery** — New instances register dynamically; Aspire's model is static at deploy time

### What Aspire does better:
- Type-safe references at compile time (no runtime discovery failures)
- Automatic startup ordering via `WaitFor()`
- Same definition for dev and prod

**Verdict: NOT a replacement.** Aspire service discovery is a **compile-time/dev-time convenience**, not a runtime service mesh. Consul remains necessary for dynamic registration, health checking, and KV store in a K8s/Linkerd environment. In K8s, Kubernetes DNS + Linkerd already handle much of what Consul does; Aspire doesn't change that layer.

---

## 4. AppHost Orchestration — Can it Replace docker-compose?

### Example AppHost for 7 His.Hope microservices:

```csharp
var builder = DistributedApplication.CreateBuilder(args);

// Databases
var identityDb = builder.AddPostgres("identity-db")
    .WithPgAdmin();
var patientDb = builder.AddPostgres("patient-db");
var clinicalDb = builder.AddPostgres("clinical-db");
var appointmentDb = builder.AddPostgres("appointment-db");
var labDb = builder.AddPostgres("lab-db");
var billingDb = builder.AddPostgres("billing-db");
var pharmacyDb = builder.AddPostgres("pharmacy-db");

// Infrastructure
var redis = builder.AddRedis("redis");
var rabbitmq = builder.AddRabbitMQ("rabbitmq");

// Microservices (each .NET project)
var identityApi = builder.AddProject<Projects.IdentityService>("identity")
    .WithReference(identityDb)
    .WithReference(redis)
    .WaitFor(identityDb);

var patientApi = builder.AddProject<Projects.PatientService>("patient")
    .WithReference(patientDb)
    .WithReference(rabbitmq)
    .WithReference(identityApi)
    .WaitFor(patientDb);

// ... repeat for each service

// Frontend (Angular)
var frontend = builder.AddNpmApp("dashboard", "../dashboard-app")
    .WithReference(identityApi)
    .WithReference(patientApi);

builder.Build().Run();
```

### docker-compose vs. AppHost:

| Feature | docker-compose | Aspire AppHost |
|---------|---------------|----------------|
| Language | YAML | C# / TypeScript |
| Type safety | None | Compile-time |
| IntelliSense | Basic YAML schema | Full IDE support |
| Startup ordering | `depends_on` (limited) | `WaitFor()` with health checks |
| Connection strings | Manual env vars | Automatic injection |
| OTel auto-config | Manual setup | Built-in |
| Dashboard | None | Built-in |
| Production | Same YAML + overrides | Generates K8s manifests (aspirate) |
| Docker-only deps | Must run containers | Can run projects directly (dotnet run) |

**Verdict: REPLACE for local dev.** AppHost is a strict upgrade over docker-compose for .NET development. Better DX, type safety, built-in observability. For production, docker-compose is already not used (you use K8s), so no conflict.

**Note on CockroachDB:** Aspire has no first-party CockroachDB integration. However, CockroachDB is **wire-compatible with PostgreSQL**, so `builder.AddPostgres("cockroach")` with the CockroachDB container image + PostgreSQL port works. For local dev, use `builder.AddContainer("cockroach", "cockroachdb/cockroach")` with custom configuration.

---

## 5. Integrations — Supported vs. Missing

### Officially supported (Microsoft, v13.4.6):
| Category | Packages |
|----------|----------|
| Databases | `Aspire.Hosting.PostgreSQL`, `Aspire.Hosting.SqlServer`, `Aspire.Hosting.Azure.CosmosDB` |
| Client DB | `Aspire.Npgsql`, `Aspire.Npgsql.EntityFrameworkCore.PostgreSQL`, `Aspire.Microsoft.EntityFrameworkCore.SqlServer` |
| Cache | `Aspire.Hosting.Redis`, `Aspire.StackExchange.Redis` |
| Messaging | `Aspire.Hosting.RabbitMQ`, `Aspire.Hosting.Kafka`, `Aspire.Hosting.Azure.ServiceBus` |
| Azure | Storage, KeyVault, Functions, OperationalInsights, AppConfiguration, EventHubs, SignalR |
| Languages | `Aspire.Hosting.JavaScript`, Python, Java, Go (via generic resource types) |
| Observability | `Aspire.Hosting.Azure.OperationalInsights` |

### His.Hope stack coverage:
| Component | Aspire Support | Notes |
|-----------|---------------|-------|
| CockroachDB | **None (via PG compat)** | Use `AddPostgres()` or `AddContainer()` |
| Redis | **Full** | `AddRedis()` + `Aspire.StackExchange.Redis` |
| RabbitMQ | **Full** | `AddRabbitMQ()` — 2.4M downloads |
| Elasticsearch | **None** | No official integration; use `AddContainer()` |
| Jaeger | **None** | Not needed; Aspire dashboard handles traces in dev |
| Prometheus | **None directly** | Can add OTLP exporter to Prometheus |
| Consul | **None** | Not applicable; Aspire is not a service mesh |
| Linkerd | **N/A** | Aspire doesn't interact with service mesh |
| Vault | **None** | Azure KeyVault is supported; HashiCorp Vault is not |

### For missing integrations:
```csharp
// Custom container for CockroachDB
var cockroach = builder.AddContainer("cockroach", "cockroachdb/cockroach:v24.1")
    .WithHttpEndpoint(8080, 8080)   // DB Console
    .WithEndpoint(26257, 26257)     // SQL
    .WithEnvironment("COCKROACH_DATABASE", "identity");

// Custom container for Elasticsearch
var elastic = builder.AddContainer("elasticsearch", "elasticsearch:8.x")
    .WithHttpEndpoint(9200, 9200)
    .WithEnvironment("discovery.type", "single-node")
    .WithEnvironment("xpack.security.enabled", "false");
```

**Verdict: ADEQUATE for core services, gaps for CockroachDB + ES.** CockroachDB via PostgreSQL compatibility works well. Elasticsearch needs a custom container definition (trivial). The integration ecosystem is growing rapidly (218 packages on NuGet).

---

## 6. Kubernetes Deployment

### Aspire's K8s deployment story:

**aspirate** (third-party tool, 911 GitHub stars, MIT license):
- Reads Aspire AppHost and generates K8s manifests (Deployments, Services, ConfigMaps, Secrets, etc.)
- Supports Kustomize for environment overlays
- Optional docker-compose output format
- Built-in secrets management (encrypted secrets file)
- **Commands:** `aspirate generate` → `aspirate apply` → `aspirate destroy`
- Handles container registry push during build

**aspire deploy** (built-in CLI):
- Newer, less documented than aspirate
- Aims to be the canonical deployment path

### ArgoCD integration:
- aspirate generates standard K8s manifests → commit to git → ArgoCD GitOps syncs
- Kustomize support means environment-specific overlays work naturally
- No native ArgoCD plugin; standard GitOps workflow applies

### Linkerd compatibility:
- Aspire generates standard K8s resources
- Linkerd injects sidecars via annotations — no conflict
- Aspire doesn't generate mTLS or network policy configs

### Current pipeline vs. Aspire:
```
Current:  make/dotnet build → Docker build → Tekton pipeline → ArgoCD GitOps → K8s
Aspire:   AppHost definition → aspirate generate → git commit → ArgoCD GitOps → K8s
```

**Verdict: COMPATIBLE, not a replacement.** Aspire/aspirate generates K8s manifests that fit into the existing ArgoCD/Tekton pipeline. It doesn't replace ArgoCD (GitOps engine) or Linkerd (service mesh). It replaces manual manifest authoring with code-generated manifests.

---

## 7. Production Readiness

### GA Status:
- .NET Aspire is GA since May 2024 (now at v13.4.6, June 2026)
- 6.2k GitHub stars, 934 forks, 7,993 commits
- Microsoft-maintained, MIT license
- Active release cadence (52 releases)

### What Aspire is NOT:
> "Aspire isn't a cloud provider or production runtime." — Official docs

- Aspire does **not** run in production. It's a development tool that generates artifacts for production.
- The AppHost runs only during development. In production, services run as normal K8s deployments.
- The **standalone dashboard** can run in production as an OTLP viewer, but it's positioned as a lightweight tool, not a production monitoring platform.

### HIPAA considerations:
- **Aspire itself** does not process, store, or transmit PHI — it's a dev tool + deployment artifact generator
- **The Aspire dashboard** in standalone mode receives OTel data which **may contain PHI** (log messages, trace attributes). This is called out in docs: "telemetry can include sensitive runtime data"
- Recommendations for HIPAA:
  - Do **not** expose the standalone dashboard without authentication
  - Configure OTLP with token-based auth
  - Filter/sanitize telemetry data before sending to the dashboard
  - Consider network policies limiting dashboard access to authorized personnel only
  - The dashboard should be treated like Kibana — access-controlled, audited

### Known limitations:
| Limitation | Impact |
|-----------|--------|
| No persistent storage for OTel data | Dashboard is real-time only; no historical queries |
| No alerting | Must keep Prometheus AlertManager |
| No custom dashboards | Must keep Grafana for custom panels |
| No multi-tenancy | Single AppHost = single application view |
| CockroachDB not officially supported | Workaround via PostgreSQL compat works |
| Elasticsearch no integration | Custom container works |
| Dashboard auth is basic (token) | Not suitable for public exposure without reverse proxy |
| .NET 8+ required | Must upgrade from .NET 8 (already met) |

---

## 8. Incremental Migration Path

### Phase 1: Local Dev Enhancement (0 risk, immediate value)
1. Add an AppHost project to the solution
2. Define CockroachDB instances (using PG compat), Redis, RabbitMQ as Aspire resources
3. Define all 7 microservices as project references
4. Run `aspire run` for local development instead of docker-compose
5. Docker-compose remains as fallback

**Benefit:** One-command startup, auto OTel in dev, unified dashboard for debugging.
**Risk:** Zero — existing docker-compose remains. AppHost is additive.

### Phase 2: OTel Simplification (low risk)
1. Add `AddServiceDefaults()` to each microservice's Program.cs
2. Keep existing manual OTel exporters (Jaeger, Prometheus, ES) running
3. In dev, OTel data flows to Aspire dashboard automatically
4. In production, no change — existing pipeline continues

**Benefit:** Simplified OTel setup, automatic instrumentation of new services.
**Risk:** Low — `AddServiceDefaults()` is additive to existing OTel config.

### Phase 3: Standalone Dashboard (medium risk, evaluate)
1. Deploy Aspire dashboard in standalone mode (Docker container in K8s)
2. Configure services to optionally export OTLP to the dashboard in addition to existing backends
3. Use as secondary OTel viewer for developers/devops
4. Evaluate whether it can replace any existing tool

**Benefit:** Lightweight OTel viewer accessible to devs without Grafana/Jaeger complexity.
**Risk:** Medium — additional OTLP traffic, must secure dashboard access, must filter PHI.

### Phase 4: aspirate for K8s Manifests (high effort, evaluate ROI)
1. Generate K8s manifests from AppHost using aspirate
2. Compare generated manifests with existing hand-crafted manifests
3. Evaluate Kustomize overlay compatibility with existing ArgoCD setup
4. Adopt if quality is sufficient; keep manual manifests as override option

**Benefit:** Single source of truth for app topology, less drift between dev and prod.
**Risk:** High — changing deployment manifests touches everything. Extensive testing needed.

### What NOT to replace:
- **Prometheus + Grafana:** Aspire dashboard cannot do custom dashboards, alerting, or long-term metrics
- **Elasticsearch + Kibana:** Aspire dashboard has no log persistence or complex log queries
- **Jaeger:** Can potentially be replaced by Aspire dashboard for trace viewing, but Jaeger has richer query capabilities
- **Consul:** Aspire has no runtime service registry, DNS, or KV store
- **ArgoCD:** Aspire generates manifests but doesn't do GitOps
- **Tekton:** Separate concern (CI pipeline)
- **Linkerd:** Separate concern (service mesh mTLS)

---

## Recommendation Matrix

| Component | Action | Rationale |
|-----------|--------|-----------|
| **docker-compose** | **Replace** with AppHost for local dev | Strict upgrade in DX |
| **SystemDashboard.Bff** | **Keep**, evaluate supplementing with Aspire dashboard | Aspire dashboard is dev-only; Bff does production aggregation |
| **Angular dashboard-app** | **Keep** | Production dashboard with HIPAA-compliant access controls |
| **Prometheus + Grafana** | **Keep** | Production metrics, alerting, custom dashboards |
| **Elasticsearch + Kibana** | **Keep** | Production log retention and search |
| **Jaeger** | **Keep**, optionally supplement with Aspire dashboard | Jaeger has richer production querying |
| **Consul** | **Keep** | Runtime service registry, health checking, KV store |
| **Manual OTel setup** | **Simplify** with `AddServiceDefaults()` | Less boilerplate, same production exporters |
| **Manual K8s manifests** | **Evaluate** aspirate generation | Single source of truth if quality acceptable |
| **Tekton / ArgoCD / Linkerd** | **Keep** | No overlap with Aspire |

### Overall Verdict

**Adopt Aspire for local development now.** It's a clear improvement over docker-compose with zero production risk. The AppHost model, unified dashboard, and automatic OTel setup will save significant dev time across 7 microservices.

**Skip aspirate for K8s** until the tool matures further (911 stars, active but community-driven). The risk of generated manifests not matching existing infrastructure expectations is high for a HIPAA system.

**Do not replace production monitoring.** Prometheus, Grafana, Elasticsearch, Kibana, and Jaeger remain essential. The Aspire standalone dashboard can be a convenient secondary viewer for developers but is not a production monitoring solution.

**CockroachDB compat:** Use PostgreSQL integration — CockroachDB's PostgreSQL wire compatibility makes this work seamlessly. For production connection strings that differ from dev, use Aspire's external parameters feature.
