# Dashboard Phase 2: Monitoring + Log Pipeline — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Harden monitoring/log pipeline: ES retention policy, complete per-service SLO rules, UTC timezone consistency, dashboard audit trail, ES/Kibana authentication.

**Architecture:** Mostly infrastructure config (K8s YAML, Prometheus rules, Vault). Light C# code: UtcDateTimeConverter, DashboardAuditMiddleware + AuditEventWriter background service. ES security enabled via xpack with Vault-managed credentials.

**Tech Stack:** K8s, Prometheus Operator rules, Elasticsearch 8.12, Vault, .NET 8, Serilog, System.Threading.Channels

## Global Constraints

- All Prometheus rules use PromQL compatible with existing metrics job labels
- Existing SLO targets unchanged (99.9% availability, p99 targets per service)
- No PHI in audit events — operational audit only
- ES auth: service account writes, kibana_system reads, elastic superuser bootstrap
- Vault injection: secrets use placeholders, actual values set in Vault UI/CLI
- Serilog ES sink: existing `indexFormat`, `autoRegisterTemplate`, `batchPostingLimit` config preserved

---

## Task Dependency Graph

```
Task 1 (ES ILM) ────────────── no deps ─┐
Task 2 (SLO recording rules) ───────────┤
Task 3 (BFF SLO alerts) ────────────────┤
Task 4 (SLO exporter + docs) ───────────┤── BATCH A (all independent)
Task 5 (UTC converter + models) ────────┤
Task 6 (AuditEvent model) ──────────────┤
Task 7 (Middleware + Writer) ───────────┤── depends on Task 6
Task 8 (ES/Kibana auth YAML) ───────────┤── BATCH A
Task 9 (7x appsettings auth) ───────────┤── BATCH A
Task 10 (Vault + K8s secrets) ──────────┤── depends on Task 8
Task 11 (Audit ILM) ────────────────────┘── depends on Task 1 (same pattern)
```

---

### Task 1: ES Log Retention (ILM + Index Template)

**Files:**
- Create: `k8s/monitoring/elasticsearch-ilm-policy.yaml`
- Create: `k8s/monitoring/elasticsearch-index-template.yaml`
- Modify: `k8s/monitoring/elasticsearch.yaml`

**Interfaces:**
- Produces: ILM policy `his-hope-logs-policy` bound to indices matching `his-hope-logs-*`, index template with rollover settings

- [ ] **Step 1: Create ILM policy manifest**

```yaml
# File: k8s/monitoring/elasticsearch-ilm-policy.yaml
apiVersion: v1
kind: ConfigMap
metadata:
  name: elasticsearch-ilm-policy
  namespace: monitoring
data:
  policy.json: |
    {
      "policy": {
        "phases": {
          "hot": {
            "min_age": "0ms",
            "actions": {
              "set_priority": { "priority": 100 }
            }
          },
          "delete": {
            "min_age": "30d",
            "actions": {
              "delete": {}
            }
          }
        }
      }
    }
```

- [ ] **Step 2: Create index template manifest**

```yaml
# File: k8s/monitoring/elasticsearch-index-template.yaml
apiVersion: v1
kind: ConfigMap
metadata:
  name: elasticsearch-index-template
  namespace: monitoring
data:
  template.json: |
    {
      "index_patterns": ["his-hope-logs-*"],
      "template": {
        "settings": {
          "index.lifecycle.name": "his-hope-logs-policy",
          "index.lifecycle.rollover_alias": "his-hope-logs",
          "number_of_shards": 1,
          "number_of_replicas": 0
        }
      }
    }
```

- [ ] **Step 3: Add ILM init to elasticsearch.yaml**

Add a postStart lifecycle hook or init container to apply the ILM policy on first boot. Append to `k8s/monitoring/elasticsearch.yaml` after the container spec:

```yaml
lifecycle:
  postStart:
    exec:
      command:
        - /bin/bash
        - -c
        - |
          until curl -s http://localhost:9200/_cluster/health | grep -q '"status":"green\|yellow"'; do
            sleep 5
          done
          curl -X PUT "http://localhost:9200/_ilm/policy/his-hope-logs-policy" \
            -H "Content-Type: application/json" \
            --data-binary @/etc/elasticsearch/ilm-policy.json
          curl -X PUT "http://localhost:9200/_index_template/his-hope-logs-template" \
            -H "Content-Type: application/json" \
            --data-binary @/etc/elasticsearch/template.json
```

Also mount the ConfigMaps:
```yaml
volumeMounts:
  - name: ilm-policy
    mountPath: /etc/elasticsearch/ilm-policy.json
    subPath: policy.json
  - name: index-template
    mountPath: /etc/elasticsearch/template.json
    subPath: template.json
volumes:
  - name: ilm-policy
    configMap:
      name: elasticsearch-ilm-policy
  - name: index-template
    configMap:
      name: elasticsearch-index-template
```

- [ ] **Step 4: Commit**

```bash
git add k8s/monitoring/elasticsearch-ilm-policy.yaml k8s/monitoring/elasticsearch-index-template.yaml k8s/monitoring/elasticsearch.yaml
git commit -m "feat(monitoring): add ES ILM policy (30d retention) and index template"
```

---

### Task 2: Per-Service SLO Recording Rules (4 missing services)

**Files:**
- Modify: `k8s/monitoring/prometheus-rules.yaml`

**Context:** Existing file has 4 rule groups. Add a 5th group `his-hope.slo.per-service-recording` with recording rules for patient, identity, appointment, clinical.

- [ ] **Step 1: Add recording rules group**

Insert before the existing `his-hope.slo.alerts` group:

```yaml
  - name: his-hope.slo.per-service-completion
    rules:
      # ---------- patient-service SLO (99.9% availability, p99 < 500ms) ----------
      - record: slo:availability:patient_service:ratio_30d
        expr: |
          sum(rate(http_server_request_duration_seconds_count{
            job="patientservice",http_response_status_code!~"5.."}[30d]))
          / sum(rate(http_server_request_duration_seconds_count{job="patientservice"}[30d]))
      - record: slo:availability:patient_service:ratio_7d
        expr: |
          sum(rate(http_server_request_duration_seconds_count{
            job="patientservice",http_response_status_code!~"5.."}[7d]))
          / sum(rate(http_server_request_duration_seconds_count{job="patientservice"}[7d]))
      - record: slo:availability:patient_service:ratio_1h
        expr: |
          sum(rate(http_server_request_duration_seconds_count{
            job="patientservice",http_response_status_code!~"5.."}[1h]))
          / sum(rate(http_server_request_duration_seconds_count{job="patientservice"}[1h]))
      - record: slo:latency_p99:patient_service:ratio_30d
        expr: |
          histogram_quantile(0.99,
            sum(rate(http_server_request_duration_seconds_bucket{job="patientservice"}[30d])) by (le))
          / 0.5
      - record: slo:error_budget_remaining:patient_service
        expr: (1 - (1 - slo:availability:patient_service:ratio_30d) / (1 - 0.999)) * 100
      - record: slo:burn_rate_1h:patient_service
        expr: |
          (1 - slo:availability:patient_service:ratio_1h)
          / (1 - 0.999)
      - record: slo:burn_rate_6h:patient_service
        expr: |
          (1 - slo:availability:patient_service:ratio_7d)
          / (1 - 0.999)

      # ---------- identity-service SLO (99.95% availability, p99 < 300ms) ----------
      # Same 7 rules with job="identityservice", SLO target 0.9995, latency threshold 0.3

      # ---------- appointment-service SLO (99.9% availability, p99 < 1s) ----------
      # Same 7 rules with job="appointmentservice", SLO target 0.999, latency threshold 1.0

      # ---------- clinical-service SLO (99.99% availability, p99 < 500ms) ----------
      # Same 7 rules with job="clinicalservice", SLO target 0.9999, latency threshold 0.5
```

(The exact 28 rules with the correct job names and thresholds are specified in the brief.)

- [ ] **Step 2: Add burn rate alerts for new services**

Add alert rules referencing the new recording rules:

```yaml
      - alert: SLOErrorBudgetBurnCritical_Patient
        expr: slo:burn_rate_1h:patient_service > 14.4 and slo:burn_rate_6h:patient_service > 14.4
        for: 2m
        labels:
          severity: critical
          service: patient-service
        annotations:
          summary: "Patient service error budget burn rate critical"
```

Repeat for identity, appointment, clinical.

- [ ] **Step 3: Commit**

```bash
git add k8s/monitoring/prometheus-rules.yaml
git commit -m "feat(monitoring): add per-service SLO recording rules and burn rate alerts for patient/identity/appointment/clinical"
```

---

### Task 3: BFF Dashboard SLO Alerts

**Files:**
- Modify: `k8s/monitoring/bff-slo-alerts.yaml`

**Context:** File already has 7 BFF alerts. Add dashboard-specific alerts using dashboard-bff job label.

- [ ] **Step 1: Add dashboard availability and burn rate alerts**

```yaml
      - alert: DashboardBffDown
        expr: up{job="dashboard-bff"} == 0
        for: 2m
        labels:
          severity: critical
          component: dashboard
        annotations:
          summary: "Dashboard BFF is down"

      - alert: DashboardBffHighErrorRate
        expr: |
          rate(http_server_request_duration_seconds_count{
            job="dashboard-bff",http_response_status_code=~"5.."}[5m])
          / rate(http_server_request_duration_seconds_count{job="dashboard-bff"}[5m]) > 0.05
        for: 5m
        labels:
          severity: warning
          component: dashboard
        annotations:
          summary: "Dashboard BFF error rate > 5%"

      - alert: DashboardBffHighLatency
        expr: |
          histogram_quantile(0.95,
            rate(http_server_request_duration_seconds_bucket{job="dashboard-bff"}[5m])) > 1.0
        for: 5m
        labels:
          severity: warning
          component: dashboard
        annotations:
          summary: "Dashboard BFF p95 latency > 1s"
```

- [ ] **Step 2: Commit**

```bash
git add k8s/monitoring/bff-slo-alerts.yaml
git commit -m "feat(monitoring): add dashboard BFF SLO alerts (availability, error rate, latency)"
```

---

### Task 4: SLO Exporter Config + Dashboard SLO Doc

**Files:**
- Modify: `k8s/monitoring/slo-exporter-config.yaml`
- Create: `docs/slo/dashboard-bff-slo.md`

- [ ] **Step 1: Add 5 missing service entries to SLO exporter**

In `slo-exporter-config.yaml`, add entries after the existing 4 services:

```yaml
    - service: patient-service
      availability_target: 99.9
      latency_target_ms: 500
    - service: identity-service
      availability_target: 99.95
      latency_target_ms: 300
    - service: appointment-service
      availability_target: 99.9
      latency_target_ms: 1000
    - service: clinical-service
      availability_target: 99.99
      latency_target_ms: 500
    - service: dashboard-bff
      availability_target: 99.9
      latency_target_ms: 1000
```

- [ ] **Step 2: Create dashboard SLO documentation**

```markdown
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
```

- [ ] **Step 3: Commit**

```bash
git add k8s/monitoring/slo-exporter-config.yaml docs/slo/dashboard-bff-slo.md
git commit -m "docs(monitoring): add missing SLO exporter entries and dashboard BFF SLO doc"
```

---

### Task 5: UTC Timezone Converter + Model Changes

**Files:**
- Create: `src/Bff/SystemDashboard.Bff/Serialization/UtcDateTimeConverter.cs`
- Modify: `src/Bff/SystemDashboard.Bff/Models/LogEntry.cs`
- Modify: `src/Bff/SystemDashboard.Bff/Models/MetricSnapshot.cs`

- [ ] **Step 1: Create UtcDateTimeConverter**

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

- [ ] **Step 2: Add converter to LogEntry.Timestamp**

In `LogEntry.cs`, add:
```csharp
using SystemDashboard.Bff.Serialization;

// On the Timestamp property:
[JsonConverter(typeof(UtcDateTimeConverter))]
public DateTime Timestamp { get; init; }
```

- [ ] **Step 3: Add converter to MetricDataPoint.Timestamp**

In `MetricSnapshot.cs`, add:
```csharp
using SystemDashboard.Bff.Serialization;

// On the Timestamp property of MetricDataPoint:
[JsonConverter(typeof(UtcDateTimeConverter))]
public DateTime Timestamp { get; init; }
```

- [ ] **Step 4: Build and verify**

Run: `dotnet build src/Bff/SystemDashboard.Bff/SystemDashboard.Bff.csproj`
Expected: Build succeeded.

- [ ] **Step 5: Commit**

```bash
git add src/Bff/SystemDashboard.Bff/Serialization/UtcDateTimeConverter.cs src/Bff/SystemDashboard.Bff/Models/LogEntry.cs src/Bff/SystemDashboard.Bff/Models/MetricSnapshot.cs
git commit -m "feat(dashboard): add UTC timezone converter for LogEntry and MetricDataPoint timestamps"
```

---

### Task 6: AuditEvent Model

**Files:**
- Create: `src/Bff/SystemDashboard.Bff/Models/AuditEvent.cs`

- [ ] **Step 1: Create AuditEvent record**

```csharp
// File: src/Bff/SystemDashboard.Bff/Models/AuditEvent.cs
namespace SystemDashboard.Bff.Models;

public sealed record AuditEvent
{
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
    public required string UserId { get; init; }
    public string UserName { get; init; } = "";
    public string Role { get; init; } = "";
    public required string Action { get; init; }
    public required string Resource { get; init; }
    public string? IpAddress { get; init; }
    public string? UserAgent { get; init; }
    public long DurationMs { get; init; }
    public int StatusCode { get; init; }
}
```

- [ ] **Step 2: Build and commit**

```bash
git add src/Bff/SystemDashboard.Bff/Models/AuditEvent.cs
git commit -m "feat(dashboard): add AuditEvent model for dashboard access tracking"
```

---

### Task 7: DashboardAuditMiddleware + AuditEventWriter + Program.cs

**Files:**
- Create: `src/Bff/SystemDashboard.Bff/Middleware/DashboardAuditMiddleware.cs`
- Create: `src/Bff/SystemDashboard.Bff/Services/AuditEventWriter.cs`
- Modify: `src/Bff/SystemDashboard.Bff/Program.cs`

- [ ] **Step 1: Create DashboardAuditMiddleware**

```csharp
// File: src/Bff/SystemDashboard.Bff/Middleware/DashboardAuditMiddleware.cs
using System.Security.Claims;
using System.Threading.Channels;
using SystemDashboard.Bff.Models;

namespace SystemDashboard.Bff.Middleware;

public sealed class DashboardAuditMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ChannelWriter<AuditEvent> _writer;

    public DashboardAuditMiddleware(RequestDelegate next, Channel<AuditEvent> channel)
    {
        _next = next;
        _writer = channel.Writer;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (!context.Request.Path.StartsWithSegments("/api"))
        {
            await _next(context);
            return;
        }

        var sw = System.Diagnostics.Stopwatch.StartNew();
        var userId = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
        var userName = context.User.FindFirstValue("name") ?? "";
        var role = context.User.FindFirstValue(ClaimTypes.Role) ?? "";
        var isAuthenticated = context.User.Identity?.IsAuthenticated == true;

        await _next(context);

        sw.Stop();

        if (isAuthenticated && userId is not null)
        {
            var auditEvent = new AuditEvent
            {
                UserId = userId,
                UserName = userName,
                Role = role,
                Action = DeriveAction(context.Request.Path, context.Request.Method),
                Resource = context.Request.Path,
                IpAddress = context.Connection.RemoteIpAddress?.ToString(),
                UserAgent = context.Request.Headers.UserAgent.ToString(),
                DurationMs = sw.ElapsedMilliseconds,
                StatusCode = context.Response.StatusCode
            };

            _writer.TryWrite(auditEvent); // non-blocking
        }
    }

    private static string DeriveAction(string path, string method) => (path, method) switch
    {
        var (p, _) when p.StartsWith("/api/resources") => "resource_view",
        var (p, _) when p.StartsWith("/api/metrics") => "query_metrics",
        var (p, _) when p.StartsWith("/api/logs") => "query_logs",
        var (p, _) when p.StartsWith("/api/traces") => "query_traces",
        _ => "page_view"
    };
}
```

- [ ] **Step 2: Create AuditEventWriter BackgroundService**

```csharp
// File: src/Bff/SystemDashboard.Bff/Services/AuditEventWriter.cs
using System.Net.Http.Json;
using System.Threading.Channels;
using SystemDashboard.Bff.Models;

namespace SystemDashboard.Bff.Services;

public sealed class AuditEventWriter : BackgroundService
{
    private readonly ChannelReader<AuditEvent> _reader;
    private readonly IElasticsearchQueryService _esService;
    private readonly ILogger<AuditEventWriter> _logger;

    public AuditEventWriter(
        Channel<AuditEvent> channel,
        IElasticsearchQueryService esService,
        ILogger<AuditEventWriter> logger)
    {
        _reader = channel.Reader;
        _esService = esService;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var batch = new List<AuditEvent>();

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // Read with 5-second deadline
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
                cts.CancelAfter(TimeSpan.FromSeconds(5));

                await foreach (var auditEvent in _reader.ReadAllAsync(cts.Token))
                {
                    batch.Add(auditEvent);
                    if (batch.Count >= 50)
                        break;
                }
            }
            catch (OperationCanceledException) { }

            if (batch.Count > 0)
            {
                await FlushBatchAsync(batch, stoppingToken);
                batch.Clear();
            }
        }
    }

    private async Task FlushBatchAsync(List<AuditEvent> batch, CancellationToken ct)
    {
        var index = $"his-hope-audit-{DateTime.UtcNow:yyyy.MM.dd}";
        try
        {
            var esUrl = $"http://elasticsearch:9200/{index}/_bulk";
            var httpClient = new HttpClient();
            var bulkBody = string.Join("\n", batch.Select(e =>
                $"{{\"index\":{{\"_index\":\"{index}\"}}}}\n" +
                System.Text.Json.JsonSerializer.Serialize(e)));

            var content = new StringContent(bulkBody + "\n", System.Text.Encoding.UTF8, "application/x-ndjson");
            var response = await httpClient.PostAsync(esUrl, content, ct);
            if (!response.IsSuccessStatusCode)
                _logger.LogWarning("Audit batch write failed: {Status}", response.StatusCode);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Audit batch write exception");
        }
    }
}
```

- [ ] **Step 3: Register in Program.cs**

Add after `builder.Services.AddHealthChecks();`:
```csharp
// Dashboard audit channel + background writer
builder.Services.AddSingleton(Channel.CreateUnbounded<AuditEvent>(
    new UnboundedChannelOptions { SingleReader = true }));
builder.Services.AddHostedService<AuditEventWriter>();
```

Add after `app.UseAuthorization();`:
```csharp
app.UseMiddleware<DashboardAuditMiddleware>();
```

- [ ] **Step 4: Build and verify**

Run: `dotnet build src/Bff/SystemDashboard.Bff/SystemDashboard.Bff.csproj`
Expected: Build succeeded.

- [ ] **Step 5: Commit**

```bash
git add src/Bff/SystemDashboard.Bff/Middleware/DashboardAuditMiddleware.cs src/Bff/SystemDashboard.Bff/Services/AuditEventWriter.cs src/Bff/SystemDashboard.Bff/Program.cs
git commit -m "feat(dashboard): add audit middleware and background writer for dashboard access tracking"
```

---

### Task 8: ES/Kibana Auth — K8s Manifests

**Files:**
- Modify: `k8s/monitoring/elasticsearch.yaml`
- Modify: `k8s/monitoring/kibana.yaml`

- [ ] **Step 1: Enable xpack.security in elasticsearch.yaml**

Add to the container `env:` section:
```yaml
  - name: xpack.security.enabled
    value: "true"
  - name: ELASTIC_PASSWORD
    valueFrom:
      secretKeyRef:
        name: elasticsearch-credentials
        key: elastic-password
```

Also update the `postStart` lifecycle hook from Task 1 to include `-u elastic:$ELASTIC_PASSWORD` in curl commands.

- [ ] **Step 2: Add credentials to kibana.yaml**

Add to the container `env:` section:
```yaml
  - name: ELASTICSEARCH_USERNAME
    value: "kibana_system"
  - name: ELASTICSEARCH_PASSWORD
    valueFrom:
      secretKeyRef:
        name: elasticsearch-credentials
        key: kibana-password
```

- [ ] **Step 3: Commit**

```bash
git add k8s/monitoring/elasticsearch.yaml k8s/monitoring/kibana.yaml
git commit -m "feat(monitoring): enable ES xpack.security and add Kibana credentials"
```

---

### Task 9: Service-to-ES Auth — appsettings.json (7 services)

**Files:**
- Modify: 7 service `appsettings.json` files

**Files to modify:**
1. `src/Services/PatientService/PatientService.Api/appsettings.json`
2. `src/Services/IdentityService/IdentityService.Api/appsettings.json`
3. `src/Services/ClinicalService/ClinicalService.Api/appsettings.json`
4. `src/Services/AppointmentService/AppointmentService.Api/appsettings.json`
5. `src/Services/LabService/LabService.Api/appsettings.json`
6. `src/Services/BillingService/BillingService.Api/appsettings.json`
7. `src/Services/PharmacyService/PharmacyService.Api/appsettings.json`

- [ ] **Step 1: Add ES credentials to each appsettings.json**

In each file, find the Elasticsearch Serilog sink config and add two args BEFORE `indexFormat`:

```json
{
  "Name": "Elasticsearch",
  "Args": {
    "nodeUris": "http://elasticsearch:9200",
    "username": "${ES_LOG_USER}",
    "password": "${ES_LOG_PASSWORD}",
    "indexFormat": "his-hope-logs-{0:yyyy.MM.dd}",
    // ... existing args preserved
  }
}
```

- [ ] **Step 2: Verify no regressions**

For each file, verify all existing keys are preserved (only 2 lines added per file).

- [ ] **Step 3: Commit**

```bash
git add src/Services/PatientService/PatientService.Api/appsettings.json src/Services/IdentityService/IdentityService.Api/appsettings.json src/Services/ClinicalService/ClinicalService.Api/appsettings.json src/Services/AppointmentService/AppointmentService.Api/appsettings.json src/Services/LabService/LabService.Api/appsettings.json src/Services/BillingService/BillingService.Api/appsettings.json src/Services/PharmacyService/PharmacyService.Api/appsettings.json
git commit -m "feat(logging): add ES credentials to Serilog sink config for all 7 services"
```

---

### Task 10: Vault Policy + K8s Secret Template

**Files:**
- Create: `vault/elasticsearch/policies.hcl`
- Create: `k8s/monitoring/elasticsearch-secrets.yaml`

- [ ] **Step 1: Create Vault policy**

```hcl
# File: vault/elasticsearch/policies.hcl
path "secret/data/elasticsearch" {
  capabilities = ["read"]
}
```

- [ ] **Step 2: Create K8s Secret template**

```yaml
# File: k8s/monitoring/elasticsearch-secrets.yaml
apiVersion: v1
kind: Secret
metadata:
  name: elasticsearch-credentials
  namespace: monitoring
type: Opaque
stringData:
  elastic-password: "CHANGE_ME_VAULT_INJECTED"
  kibana-password: "CHANGE_ME_VAULT_INJECTED"
---
apiVersion: v1
kind: Secret
metadata:
  name: es-log-credentials
  namespace: his-hope
type: Opaque
stringData:
  ES_LOG_USER: "his-hope-log-writer"
  ES_LOG_PASSWORD: "CHANGE_ME_VAULT_INJECTED"
```

- [ ] **Step 3: Commit**

```bash
git add vault/elasticsearch/policies.hcl k8s/monitoring/elasticsearch-secrets.yaml
git commit -m "feat(security): add Vault policy and K8s secret templates for ES credentials"
```

---

### Task 11: Audit Log ILM (ES Retention for Audit Index)

**Files:**
- Create: `k8s/monitoring/elasticsearch-audit-ilm.yaml`

- [ ] **Step 1: Create audit ILM policy**

```yaml
# File: k8s/monitoring/elasticsearch-audit-ilm.yaml
apiVersion: v1
kind: ConfigMap
metadata:
  name: elasticsearch-audit-ilm-policy
  namespace: monitoring
data:
  policy.json: |
    {
      "policy": {
        "phases": {
          "hot": {
            "min_age": "0ms",
            "actions": {
              "set_priority": { "priority": 100 }
            }
          },
          "delete": {
            "min_age": "90d",
            "actions": {
              "delete": {}
            }
          }
        }
      }
    }
  template.json: |
    {
      "index_patterns": ["his-hope-audit-*"],
      "template": {
        "settings": {
          "index.lifecycle.name": "his-hope-audit-policy",
          "number_of_shards": 1,
          "number_of_replicas": 0
        }
      }
    }
```

- [ ] **Step 2: Commit**

```bash
git add k8s/monitoring/elasticsearch-audit-ilm.yaml
git commit -m "feat(monitoring): add 90-day ILM retention policy for audit logs"
```

---

## Verification Checklist

- [ ] `dotnet build src/Bff/SystemDashboard.Bff/SystemDashboard.Bff.csproj` — succeeds
- [ ] `dotnet test src/Bff/SystemDashboard.Bff/SystemDashboard.Bff.csproj` — all pass
- [ ] `kubectl apply --dry-run=client -f k8s/monitoring/` — all YAML validates
- [ ] SLO recording rules: `promtool check rules k8s/monitoring/prometheus-rules.yaml` — passes
- [ ] BFF SLO alerts: `promtool check rules k8s/monitoring/bff-slo-alerts.yaml` — passes
- [ ] `appsettings.json` files: valid JSON (all 7 + unchanged ones)
