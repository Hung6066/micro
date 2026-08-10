# Dashboard Phase 2: Monitoring + Log Pipeline — Design Spec

**Date:** 2026-07-22
**Status:** Draft
**Author:** Lead System Architect

## 1. Problem Statement

Phase 1 delivered query performance. Phase 2 hardens the monitoring and log pipeline: Elasticsearch has no retention policy (disk will fill), per-service SLO rules are incomplete (3 of 7 services), log timestamps lack explicit UTC enforcement, dashboard usage is untracked (HIPAA audit gap), and ES/Kibana have no authentication.

## 2. Goals

| Goal | Metric | Target |
|------|--------|--------|
| Log retention enforced | ES disk usage | < 40Gi (of 50Gi PVC) |
| Complete per-service SLO | Recording rules per service | 7/7 services |
| UTC consistency | All log timestamps | Explicit UTC with Z suffix |
| Dashboard audit trail | Access events logged | 100% of authenticated requests |
| ES/Kibana auth | Authentication enabled | Basic auth, credentials in Vault |

## 3. Non-Goals

- Clinical audit trail (PHI access logs) — separate project, different compliance scope
- Real-time usage dashboard — can be added later, audit data is in ES
- Kibana RBAC (role-based dashboard access per user) — basic auth only, RBAC later
- Log sampling or filtering — all logs retained within retention window

---

## 4. Section 1: ES Log Retention (ILM + Index Template)

### 4.1 ILM Policy

```yaml
# File: k8s/monitoring/elasticsearch-ilm-policy.yaml
apiVersion: elasticsearch.k8s.elastic.co/v1
kind: Elasticsearch
# ... (inline policy definition via config)
```

Policy phases:

| Phase | Action | Min Age |
|-------|--------|---------|
| hot | (default) | 0d |
| delete | Delete index | 30d (prod) / 7d (dev) |

Index names covered: `his-hope-logs-*`

### 4.2 Index Template

```yaml
# File: k8s/monitoring/elasticsearch-index-template.yaml
# Applied to: his-hope-logs-*
# Binds ILM policy: his-hope-logs-policy
# Rollover: max_primary_shard_size=30gb, max_age=30d
# Settings: number_of_shards=1, number_of_replicas=0 (single-node ES)
```

### 4.3 Files

| File | Action |
|------|--------|
| `k8s/monitoring/elasticsearch-ilm-policy.yaml` | Create |
| `k8s/monitoring/elasticsearch-index-template.yaml` | Create |
| `k8s/monitoring/elasticsearch.yaml` | Modify — add ILM init container or post-start script |

---

## 5. Section 2: Complete SLO Rules

### 5.1 Per-Service Recording Rules for 4 Missing Services

Add to `k8s/monitoring/prometheus-rules.yaml`:

- **patient-service** — availability (99.9%), latency p99 (500ms)
- **identity-service** — availability (99.95%), latency p99 (300ms)
- **appointment-service** — availability (99.9%), latency p99 (1s)
- **clinical-service** — availability (99.99%), latency p99 (500ms)

Each gets 9 recording rules: availability (30d/7d/1h), latency p99 (30d/7d/1h), error budget remaining, burn rate (1h/6h).

### 5.2 Dashboard BFF SLO

Add to `k8s/monitoring/bff-slo-alerts.yaml` (already exists with 7 alerts):

| SLI | Target | PromQL |
|-----|--------|--------|
| Availability | 99.9% | `up{job="dashboard-bff"}` |
| Latency p95 | < 500ms | `histogram_quantile(0.95, rate(http_server_request_duration_seconds_bucket{job="dashboard-bff"}[5m]))` |
| Error rate | < 1% | `rate(http_server_request_duration_seconds_count{job="dashboard-bff",status=~"5.."}[5m]) / rate(...)` |

### 5.3 SLO Exporter Config

Update `k8s/monitoring/slo-exporter-config.yaml` — add entries for patient-service, identity-service, appointment-service, clinical-service, dashboard-bff.

### 5.4 Files

| File | Action |
|------|--------|
| `k8s/monitoring/prometheus-rules.yaml` | Modify — add 36 recording rules + 4 alert rules |
| `k8s/monitoring/slo-exporter-config.yaml` | Modify — add 5 missing service entries |
| `k8s/monitoring/bff-slo-alerts.yaml` | Modify — add dashboard availability/error burn alerts |
| `docs/slo/dashboard-bff-slo.md` | Create — SLO/SLI reference doc |

---

## 6. Section 3: Timezone Hardening

### 6.1 UtcDateTimeConverter

New JSON converter ensures all DateTime values serialize with explicit UTC:

```csharp
// File: src/Bff/SystemDashboard.Bff/Serialization/UtcDateTimeConverter.cs
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SystemDashboard.Bff.Serialization;

public sealed class UtcDateTimeConverter : JsonConverter<DateTime>
{
    public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        => DateTime.SpecifyKind(reader.GetDateTime(), DateTimeKind.Utc);

    public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options)
        => writer.WriteStringValue(value.ToUniversalTime().ToString("o"));
}
```

### 6.2 Model Changes

**LogEntry.Timestamp:**
```csharp
// Add using: using SystemDashboard.Bff.Serialization;
[JsonConverter(typeof(UtcDateTimeConverter))]
public DateTime Timestamp { get; init; }
```

**MetricDataPoint.Timestamp:**
```csharp
[JsonConverter(typeof(UtcDateTimeConverter))]
public DateTime Timestamp { get; init; }
```

### 6.3 LogStreamBackgroundService (already correct)

`_lastPushedTimestamp = DateTime.UtcNow - TimeSpan.FromSeconds(30);` — already uses UtcNow. No change needed.

### 6.4 Files

| File | Action |
|------|--------|
| `src/Bff/SystemDashboard.Bff/Serialization/UtcDateTimeConverter.cs` | Create |
| `src/Bff/SystemDashboard.Bff/Models/LogEntry.cs` | Modify — add JsonConverter attribute |
| `src/Bff/SystemDashboard.Bff/Models/MetricSnapshot.cs` | Modify — add JsonConverter to Timestamp |

---

## 7. Section 4: Usage Analytics + Audit Trail

### 7.1 AuditEvent Model

```csharp
// File: src/Bff/SystemDashboard.Bff/Models/AuditEvent.cs
namespace SystemDashboard.Bff.Models;

public sealed record AuditEvent
{
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
    public required string UserId { get; init; }
    public string UserName { get; init; } = "";
    public string Role { get; init; } = "";
    public required string Action { get; init; }      // "page_view", "query_logs", etc.
    public required string Resource { get; init; }     // "/dashboard/resources"
    public string? IpAddress { get; init; }
    public string? UserAgent { get; init; }
    public long DurationMs { get; init; }
    public int StatusCode { get; init; }
}
```

### 7.2 DashboardAuditMiddleware

```csharp
// Intercepts /api/dashboard/* requests
// Captures: userId from JWT claims, action from route pattern, resource from path
// Writes AuditEvent to Channel (non-blocking)
// Adds X-Request-Duration header for timing
```

### 7.3 AuditEventWriter (BackgroundService)

```csharp
// Reads from Channel<AuditEvent>
// Batches: every 5 seconds OR 50 events (whichever first)
// Writes to: his-hope-audit-{yyyy.MM.dd} index via ElasticsearchClient
// Circuit breaker + retry via existing resilience policies
```

### 7.4 Registration (Program.cs)

```csharp
// Add channel
builder.Services.AddSingleton(Channel.CreateUnbounded<AuditEvent>(
    new UnboundedChannelOptions { SingleReader = true }));

// Add background writer
builder.Services.AddHostedService<AuditEventWriter>();

// Add middleware (after auth, before endpoints)
app.UseMiddleware<DashboardAuditMiddleware>();
```

### 7.5 Audit Log Retention

Separate ILM policy: 90 days (HIPAA minimum).

### 7.6 What Is NOT Logged

- Request bodies (no PHI exposure)
- Search query terms
- Response payloads
- Trace IDs or internal correlation data
- Patient IDs or clinical data of any kind

### 7.7 Files

| File | Action |
|------|--------|
| `src/Bff/SystemDashboard.Bff/Models/AuditEvent.cs` | Create |
| `src/Bff/SystemDashboard.Bff/Middleware/DashboardAuditMiddleware.cs` | Create |
| `src/Bff/SystemDashboard.Bff/Services/AuditEventWriter.cs` | Create |
| `src/Bff/SystemDashboard.Bff/Program.cs` | Modify — register channel + middleware + bg service |
| `k8s/monitoring/elasticsearch-audit-ilm.yaml` | Create |

---

## 8. Section 5: ES/Kibana Auth

### 8.1 Enable Security in Elasticsearch

Modify `k8s/monitoring/elasticsearch.yaml`:

```yaml
env:
  - name: xpack.security.enabled
    value: "true"
  - name: ELASTIC_PASSWORD
    valueFrom:
      secretKeyRef:
        name: elasticsearch-credentials
        key: elastic-password
```

### 8.2 Add Credentials to Kibana

Modify `k8s/monitoring/kibana.yaml`:

```yaml
env:
  - name: ELASTICSEARCH_USERNAME
    value: "kibana_system"
  - name: ELASTICSEARCH_PASSWORD
    valueFrom:
      secretKeyRef:
        name: elasticsearch-credentials
        key: kibana-password
```

### 8.3 Service-to-ES Auth (Serilog Sink)

Update `appsettings.json` in 8 services — add ES credentials to Serilog Elasticsearch sink config:

```json
{
  "Name": "Elasticsearch",
  "Args": {
    "nodeUris": "http://elasticsearch:9200",
    "username": "${ES_LOG_USER}",
    "password": "${ES_LOG_PASSWORD}",
    "indexFormat": "his-hope-logs-{0:yyyy.MM.dd}",
    "autoRegisterTemplate": true,
    "batchPostingLimit": 50,
    "inlineFields": true
  }
}
```

### 8.4 K8s Secret (placeholder — Vault injected)

```yaml
# File: k8s/monitoring/elasticsearch-secrets.yaml
apiVersion: v1
kind: Secret
metadata:
  name: elasticsearch-credentials
type: Opaque
stringData:
  elastic-password: "CHANGE_ME_VAULT_INJECTED"
  kibana-password: "CHANGE_ME_VAULT_INJECTED"
  es-log-user: "his-hope-log-writer"
  es-log-password: "CHANGE_ME_VAULT_INJECTED"
```

### 8.5 Vault Policy

```hcl
# vault/elasticsearch/policies.hcl
path "secret/data/elasticsearch" {
  capabilities = ["read"]
}
```

### 8.6 Files

| File | Action |
|------|--------|
| `k8s/monitoring/elasticsearch.yaml` | Modify — enable security |
| `k8s/monitoring/kibana.yaml` | Modify — add credentials |
| `k8s/monitoring/elasticsearch-secrets.yaml` | Create |
| `vault/elasticsearch/policies.hcl` | Create |
| 8x `appsettings.json` | Modify — add ES auth args (username/password) to Serilog ES sink. Files: PatientService.Api, IdentityService.Api, ClinicalService.Api, AppointmentService.Api, LabService.Api, BillingService.Api, PharmacyService.Api, SystemDashboard.Bff |

---

## 9. Risk Assessment

| Risk | Likelihood | Impact | Mitigation |
|------|-----------|--------|------------|
| ILM policy deletes needed logs | Low | High | 90-day audit retention; 30-day app logs; dev env 7-day |
| ES auth breaks Serilog sink | Medium | Medium | Test in dev first; fallback: Serilog auto-register template still works with Basic auth |
| Audit middleware adds latency | Low | Low | Channel.WriteAsync is non-blocking; batching reduces ES writes |
| UTC converter changes serialization format | Low | Medium | Angular frontend Date parsing should handle ISO 8601 Z suffix natively |
| SLO rules query load on Prometheus | Low | Low | Recording rules evaluated per evaluation_interval (30s); 36 rules is negligible |

## 10. Rollout Plan

1. **Dev:** Deploy ILM + index template. Test ES auth locally. Run dashboard, verify audit events in ES.
2. **Staging:** Deploy SLO rules, verify alerts fire correctly. Test 24h.
3. **Production:** Phase rollout:
   - Week 1: ILM + index template (non-breaking, existing data preserved)
   - Week 2: SLO rules + dashboard audit middleware (additive)
   - Week 3: ES auth (breaking change — coordinated with service restarts)
   - Week 4: Verify ILM deletion working, audit retention compliant
