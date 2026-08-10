# Hướng dẫn Monitoring & Observability — His.Hope EMR

> **Tài liệu:** OPS-MON-001
> **Version:** 1.0
> **Audience:** SRE, DevOps, Backend Developer
> **Cập nhật:** 2026-07-16

---

## 1. Grafana Dashboard Navigation

### 1.1 Truy cập Grafana

```bash
# Port-forward Grafana từ K8s cluster
kubectl port-forward svc/grafana -n monitoring 3000:3000 &

# Truy cập: http://localhost:3000
# Default credentials: admin / <từ Vault secret>
# Lấy password từ Vault:
kubectl exec -it vault-0 -n vault -- vault kv get secret/his-hope/monitoring/grafana
```

### 1.2 Key Dashboards — Service Health

| Dashboard | UID | Mô tả | Cách đọc |
|-----------|-----|-------|----------|
| **SLO Overview** | `slo-overview` | Error budget, burn rate, availability cho tất cả services | Màu xanh = healthy, vàng = cảnh báo burn rate >1, đỏ = critical burn rate >2 |
| **Service Golden Signals** | `golden-signals` | 4 golden signals (latency, traffic, errors, saturation) cho mỗi service | So sánh trend latency/errors giữa các services |
| **patient-service** | `patient-service` | Request rate, latency p50/p95/p99, error rate 5xx, DB query duration | Tất cả panels theo dõi 30d rolling window |
| **identity-service** | `identity-service` | Auth requests, login rate, JWT token operations, refresh token metrics | Chú ý spike trong login failures |
| **clinical-service** | `clinical-service` | Encounter operations, vitals recording, diagnosis rate, PHI access count | Theo dõi PHI access patterns |
| **appointment-service** | `appointment-service` | Scheduling rate, no-show rate, cancellation rate, check-in/out latency | Check-in latency không quá 2s |
| **api-gateway** | `api-gateway` | Total requests, rate limiting hits, per-route breakdown, 4xx/5xx rates | Rate limit hits (429) nên < 1% |

### 1.3 Key Dashboards — Infrastructure

| Dashboard | UID | Mô tả | Cách đọc |
|-----------|-----|-------|----------|
| **CockroachDB Overview** | `cockroachdb` | Node status (5 nodes), replication health, query performance, storage utilization | Cảnh báo nếu available storage < 20% hoặc replication lag > 10s |
| **Redis Cluster** | `redis-cluster` | Memory usage per node, hit ratio, connected clients, evictions | Hit ratio > 80% (tốt), evictions > 0 = cache đầy |
| **RabbitMQ Overview** | `rabbitmq` | Queue depth per queue, message rates (publish/consume), connections, channels | Queue depth tăng liên tục = consumer bottleneck |
| **Linkerd Mesh** | `linkerd-mesh` | mTLS status %, inbound/outbound success rates, proxy latency p99, proxy resource usage | All edges nên green (100% success), proxy latency p99 < 50ms |
| **Cilium / Hubble** | `cilium-hubble` | Dropped packets, flow rates, policy enforcement metrics, endpoint health | Dropped rate nên < 1% (trừ blocked attack traffic) |
| **Kubernetes Cluster** | `k8s-cluster` | Node health, CPU/memory cluster-wide, pod restarts, PVC usage | Pod restart spike = cần investigate |
| **Vault** | `vault` | Seal status, token usage, secret access rate, policy evaluation rate | Cảnh báo nếu any node sealed |
| **Kubecost** | `kubecost` | Cost per namespace, per deployment, per label; efficiency metrics | Budget alerts khi > 90% monthly budget |

### 1.4 Key Dashboards — Specialized

| Dashboard | UID | Mô tả |
|-----------|-----|-------|
| **Auto-Remediation** | `auto-remediation` | MTTR tracking, self-healing action log, remediation success rate |
| **Chaos Engineering** | `chaos-experiments` | Experiment status, MTTR per experiment type, service degradation during chaos |
| **NoOps** | `noops` | Automation coverage %, manual intervention count, AIOps anomaly detection |
| **SLO Alert Detail** | `slo-alert-detail` | Zoom-in view của SLO burn rate alerts, hiển thị multi-window burn rates |
| **FinOps** | `finops` | Cost breakdown, resource efficiency, spot instance usage, waste identification |

---

## 2. Prometheus Alert Rules

### 2.1 Alert Rules Configuration

Alert rules được định nghĩa trong `k8s/monitoring/prometheus-rules.yaml`. Đây là bảng tổng hợp tất cả alert rules:

#### Service Health Alerts

| Alert Name | Expression | Severity | Meaning |
|------------|-----------|----------|---------|
| **ServiceDown** | `up{job=~"patient-service|identity-service|..."} == 0` for 30s | P0 Critical | Service hoàn toàn không phản hồi, Prometheus không scrape được metrics |
| **HighErrorRate5xx** | `rate(http_requests_total{code=~"5.."}[5m]) / rate(http_requests_total[5m]) > 0.01` for 5m | P1 High | >1% requests trả về 5xx trong 5 phút |
| **HighErrorRate4xx** | `rate(http_requests_total{code=~"4.."}[5m]) / rate(http_requests_total[5m]) > 0.05` for 5m | P2 Medium | >5% requests trả về 4xx (có thể là validation, rate limiting) |
| **HighLatencyP99** | `histogram_quantile(0.99, rate(http_server_duration_ms_bucket[5m])) > 500` for 5m | P2 Medium | p99 latency vượt 500ms |
| **HighLatencyP95** | `histogram_quantile(0.95, rate(http_server_duration_ms_bucket[5m])) > 300` for 5m | P3 Low | p95 latency vượt 300ms |
| **CircuitBreakerOpen** | `circuit_breaker_state{state="open"} == 1` for 1m | P1 High | Circuit breaker mở — service đang degraded |
| **SlowDBQueries** | `rate(db_query_duration_ms_bucket{le="+Inf"}[5m]) - rate(db_query_duration_ms_bucket{le="100"}[5m]) > 10` for 5m | P2 Medium | >10 queries/5min chậm hơn 100ms |

#### SLO / Error Budget Alerts

| Alert Name | Expression | Severity | Meaning |
|------------|-----------|----------|---------|
| **SLOErrorBudgetBurnCritical** | `(1 - avg_over_time(job:slo_availability_1h:ratio[1h])) / (1 - 0.999) > 14.4` AND `(1 - avg_over_time(job:slo_availability_5m:ratio[5m])) / (1 - 0.999) > 14.4` | P0 Critical | Đang đốt error budget với burn rate >14.4x (hết budget trong 1h) |
| **SLOErrorBudgetBurnWarning** | `(1 - avg_over_time(job:slo_availability_6h:ratio[6h])) / (1 - 0.999) > 6` for 1h | P1 High | Đang đốt error budget với burn rate >6x (hết budget trong 3h) |
| **SLOErrorBudgetExhausted** | `job:slo_error_budget_remaining:ratio < 0` for 5m | P0 Critical | Error budget cho tháng đã hết, cần ngừng deploy mới |

#### Infrastructure Alerts

| Alert Name | Expression | Severity | Meaning |
|------------|-----------|----------|---------|
| **HighCPUUsage** | `container_cpu_usage_seconds_total{container!=""} / container_spec_cpu_quota > 0.8` for 10m | P2 Medium | Container CPU usage >80% trong 10 phút |
| **HighMemoryUsage** | `container_memory_usage_bytes{container!=""} / container_spec_memory_limit_bytes > 0.85` for 5m | P2 Medium | Container memory >85% limit trong 5 phút |
| **PodCrashLoop** | `rate(kube_pod_container_status_restarts_total[15m]) > 0` for 5m | P1 High | Pod restart liên tục (CrashLoopBackOff) |
| **DiskFull** | `(node_filesystem_avail_bytes / node_filesystem_size_bytes) < 0.15` for 5m | P1 High | Disk còn <15% free |
| **PVCThreshold** | `(kubelet_volume_stats_available_bytes / kubelet_volume_stats_capacity_bytes) < 0.2` for 5m | P2 Medium | PVC usage >80% |
| **DBConnectionPoolExhausted** | `cockroachdb_sql_conns{state="active"} / cockroachdb_sql_conns_limit > 0.8` for 5m | P1 High | CockroachDB connection pool >80% exhausted |
| **DBReplicationLag** | `cockroachdb_replication_lag_seconds > 10` for 2m | P1 High | CockroachDB replication lag >10 giây |
| **RedisMemoryHigh** | `redis_memory_used_bytes / redis_maxmemory_bytes > 0.85` for 5m | P2 Medium | Redis memory usage >85% |
| **RedisDown** | `redis_up == 0` for 30s | P1 High | Redis node down |
| **RabbitMQQueueDepth** | `rabbitmq_queue_messages{queue!~".*dlq.*"} > 1000` for 10m | P2 Medium | RabbitMQ queue depth >1000 messages |
| **RabbitMQNodeDown** | `rabbitmq_node_up == 0` for 30s | P1 High | RabbitMQ node down |

#### Security Alerts

| Alert Name | Expression | Severity | Meaning |
|------------|-----------|----------|---------|
| **VaultSealed** | `vault_core_unsealed == 0` for 30s | P0 Critical | Vault node bị sealed |
| **CertExpiring** | `(certmanager_certificate_expiry_days < 30)` for 1h | P1 High | Certificate sắp hết hạn (<30 ngày) |
| **JWTKeyRotationFailed** | `vault_transit_rotate_failures > 0` for 5m | P1 High | Vault transit key rotation thất bại |
| **UnusualDataAccess** | `rate(phi_access_total[5m]) > 2 * avg_over_time(phi_access_total[1h:5m])` for 5m | P1 High | PHI access spike bất thường (>2x baseline) |
| **FailedLoginSpike** | `rate(login_failures_total[5m]) > 20` for 5m | P2 Medium | Login failures spike (có thể brute force) |

### 2.2 Silencing Alerts

```bash
# Tạo silence cho alert cụ thể (maintenance window)
# Port-forward Alertmanager
kubectl port-forward svc/alertmanager -n monitoring 9093:9093 &

# Tạo silence qua API
$silenceBody = @{
    matchers = @(
        @{ name = "alertname"; value = "ServiceDown"; isRegex = $false },
        @{ name = "service"; value = "patient-service"; isRegex = $false }
    )
    startsAt = (Get-Date).ToString("yyyy-MM-ddTHH:mm:ssZ")
    endsAt = (Get-Date).AddHours(2).ToString("yyyy-MM-ddTHH:mm:ssZ")
    createdBy = "sre-oncall"
    comment = "Planned maintenance - patient-service rolling update"
} | ConvertTo-Json -Depth 3

Invoke-RestMethod -Uri "http://localhost:9093/api/v2/silences" -Method POST -Body $silenceBody

# Hoặc qua Alertmanager UI: http://localhost:9093 → "New Silence"
```

---

## 3. SLO / SLI Dashboard Interpretation

### 3.1 Service Level Objectives

| Service | Availability SLO | Latency SLO (p99) | Error Budget (30d) |
|---------|-----------------|-------------------|---------------------|
| **patient-service** | 99.9% | 500ms | 43.2 phút downtime/tháng |
| **identity-service** | 99.95% | 300ms | 21.6 phút downtime/tháng |
| **appointment-service** | 99.9% | 1s | 43.2 phút downtime/tháng |
| **clinical-service** | 99.99% | 500ms | 4.32 phút downtime/tháng |

### 3.2 Error Budget Interpretation

Error budget = 1 - SLO. Ví dụ: patient-service SLO 99.9% → error budget = 0.1% = 43.2 phút/tháng.

```
Error budget còn > 50%:
  → GREEN — An toàn để deploy feature mới, experiment

Error budget 20-50%:
  → YELLOW — Deploy cẩn thận, chỉ bug fixes

Error budget < 20%:
  → ORANGE — Freeze non-critical deploys, tập trung reliability

Error budget = 0%:
  → RED — Ngừng tất cả deploy. Chỉ fix reliability issues
```

### 3.3 Burn Rate Interpretation

Burn rate = tốc độ tiêu thụ error budget hiện tại so với baseline.

```
Burn rate < 1x:
  → Tiêu thụ chậm hơn bình thường, OK

Burn rate 1-2x:
  → Tiêu thụ bình thường đến hơi cao

Burn rate 2-5x:
  → WARNING — Nếu duy trì, hết budget trong vài giờ

Burn rate > 10x:
  → CRITICAL — Hết budget trong <1h, cần action ngay

Burn rate > 14.4x:
  → FIRING — Alert SLOErrorBudgetBurnCritical
```

### 3.4 Multi-Window Burn Rate Concept

Sử dụng 2 time windows để tránh false positives:

| Alert | Short Window | Long Window | Burn Rate Threshold |
|-------|-------------|-------------|---------------------|
| **Critical** | 1 giờ | 5 phút | > 14.4x (hết budget trong 1h) |
| **Warning** | 6 giờ | 30 phút | > 6x (hết budget trong 3h) |

Cả 2 windows phải cùng exceed threshold thì alert mới fire → chỉ alert khi thực sự severe.

### 3.5 SLO Dashboard Walkthrough

```bash
# Mở SLO dashboard
# http://localhost:3000/d/slo-overview

# Các panel chính và cách đọc:

# Panel "Error Budget Remaining" (gauge):
# → Hiển thị % error budget còn lại trong tháng
# → >50% green, 20-50% yellow, <20% red

# Panel "Multi-Window Burn Rate" (time series):
# → 2 lines: short window (5m) và long window (1h)
# → Khi cả 2 cùng trên threshold → ALERT

# Panel "Availability by Service" (table):
# → 30d rolling availability % cho từng service
# → So với SLO target

# Panel "Error Budget Burn Down" (bar):
# → Hiển thị error budget consumed theo ngày
# → Ví dụ 43.2 phút budget, consumed 15 phút → còn 28.2 phút

# Panel "Top Error Sources" (table):
# → Route/endpoint + error count
# → Xác định endpoint nào gây lỗi nhiều nhất
```

---

## 4. Jaeger Trace Analysis

### 4.1 Truy cập Jaeger

```bash
# Port-forward Jaeger Query UI
kubectl port-forward svc/jaeger-query -n linkerd-jaeger 16686:16686 &

# Truy cập: http://localhost:16686

# Export traces qua API
$JAEGER_URL = "http://localhost:16686/api/traces"
```

### 4.2 Tìm và Phân tích Trace

**Tìm trace theo correlation ID (từ log hoặc response header):**

```bash
# 1. Tìm trong Kibana log trước → lấy traceId
#    Kibana query: correlationId: "abc-123-def"
#    → Xem field traceId: "0a1b2c3d4e5f6789..."

# 2. Paste traceId vào Jaeger UI search box
#    http://localhost:16686/trace/0a1b2c3d4e5f6789...

# 3. Xem Gantt chart → xác định span nào mất thời gian nhất
```

**Tìm traces chậm (>500ms):**

```
Jaeger Search:
  Service: patient-service
  Operation: all
  Min Duration: 500ms
  Limit: 50
```

**Phân tích trace để debug latency:**

```
Sơ đồ trace điển hình:

POST /api/v1/patients [15ms]
  ├── CreatePatientCommand.Handle [12ms]
  │     ├── db.save (PatientDbContext.SaveChangesAsync) [8ms]  ← Bình thường
  │     │     ├── outbox.create [1ms]
  │     │     └── db.commit [7ms]
  │     └── cache.remove (Redis Cluster) [2ms]
  ├── eventbus.publish [2ms]
  │     └── rabbitmq.publish [1.5ms]
  └── (TOTAL: 15ms — OK!)

Nếu db.save có duration >50ms → DB performance issue
Nếu rabbitmq.publish >50ms → RabbitMQ issue
Nếu cache.remove >10ms → Redis latency issue
```

### 4.3 Jaeger Trace trong Service-to-Service Calls

```
POST /api/v1/appointments [45ms]  ← Request từ client
  ├── POST /patient.PatientService/CheckPatientExists [12ms]  ← gRPC call
  │     ├── db.query (PatientDbContext) [8ms]
  │     ├── linkerd-proxy-outbound [3ms]  ← Network + mTLS overhead
  │     └── linkerd-proxy-inbound (patient) [1ms]
  ├── db.save (AppointmentDbContext) [10ms]
  └── rabbitmq.publish (AppointmentScheduledIntegrationEvent) [15ms]
```

Linkerd tự động thêm span cho inbound/outbound proxy → visible network latency.

### 4.4 Trace-Based Metrics

```bash
# Query Jaeger để lấy metrics từ trace data
# Ví dụ: error rate theo service và operation
Invoke-RestMethod "$JAEGER_URL/api/services" | ConvertTo-Json
Invoke-RestMethod "$JAEGER_URL/api/services/patient-service/operations" | ConvertTo-Json

# So sánh latency distribution giữa các versions
# → Xem tag "version" trong span tags
```

---

## 5. Kibana Log Query Patterns

### 5.1 Truy cập Kibana

```bash
kubectl port-forward svc/kibana -n monitoring 5601:5601 &
# Truy cập: http://localhost:5601
```

### 5.2 Correlation ID Tracing

Mỗi request có một `CorrelationId` (UUID) được propagate qua tất cả services.

```json
// Log format (Serilog):
{
  "@timestamp": "2026-07-16T12:00:00.123Z",
  "level": "Information",
  "service": "patient-service",
  "traceId": "0a1b2c3d4e5f67890123456789abcdef",
  "spanId": "abc123def456",
  "correlationId": "550e8400-e29b-41d4-a716-446655440000",
  "messageTemplate": "Processing request: {Name} {@Request}",
  "properties": {
    "Name": "CreatePatientCommand",
    "Request": { "FirstName": "John", "LastName": "Doe" }
  }
}
```

#### Tìm tất cả logs của một request

```
Kibana Query:
  correlationId: "550e8400-e29b-41d4-a716-446655440000"
  AND @timestamp > now-1h
```

→ Kết quả sẽ hiển thị logs từ API Gateway → Patient Service → DB → Response, tất cả cùng correlationId.

### 5.3 Error Aggregation Patterns

#### Tìm tất cả errors trong 15 phút qua

```
Kibana Query:
  level: "Error" AND @timestamp > now-15m
```

→ Bucket aggregation theo `service` + `exceptionType` để xem pattern.

#### Top error types

```
Kibana Query:
  level: "Error" AND @timestamp > now-1h

→ Add sub-aggregation: Terms on properties.Exception.keyword
→ Sort by count descending
```

#### Error theo endpoint

```
Kibana Query:
  level: "Error" AND @timestamp > now-1h

→ Add sub-aggregation: Terms on properties.RequestPath.keyword
```

### 5.4 Performance Analysis Patterns

#### Slow requests (>500ms)

```
Kibana Query:
  properties.ElapsedMilliseconds > 500 AND @timestamp > now-15m

→ Sort by properties.ElapsedMilliseconds descending
```

#### Request latency distribution (histogram)

```
Kibana Query:
  service: "patient-service" AND @timestamp > now-1h

→ Visualization: Vertical Bar
→ X-axis: Date Histogram on @timestamp
→ Y-axis: Percentiles → properties.ElapsedMilliseconds [50, 95, 99]
```

### 5.5 Security & Audit Patterns

#### PHI access log

```
Kibana Query:
  messageTemplate: "*PHI*" AND @timestamp > now-24h

→ Kiểm tra ai đã truy cập ePHI, từ IP nào, service nào
```

#### Failed login attempts

```
Kibana Query:
  messageTemplate: "Authentication failed*" AND @timestamp > now-1h

→ Bucket by properties.Username để thấy username bị attack
→ Bucket by properties.IpAddress để thấy IP nguồn tấn công
```

#### Token revocation events

```
Kibana Query:
  messageTemplate: "Token revoked*" AND service: "identity-service" AND @timestamp > now-1h
```

### 5.6 Kibana Saved Queries (Được cấu hình sẵn trong Kibana)

| Saved Query | Purpose |
|-------------|---------|
| **All Errors (15m)** | Tất cả Error logs trong 15 phút |
| **Slow Requests (>500ms)** | Requests chậm, có thể ảnh hưởng SLO |
| **Authentication Failures** | Security monitoring — brute force detection |
| **Patient Service Errors** | Lọc riêng patient-service |
| **Clinical PHI Access** | Audit PHI access |
| **Outbox Processing Errors** | Integration event delivery failures |
| **DB Connection Errors** | Database connectivity issues |
| **Circuit Breaker Events** | Polly circuit breaker transitions |

---

## 6. Hubble CLI — Network Flow Debugging

### 6.1 Thiết lập Hubble

```bash
# Port-forward Hubble Relay Service
kubectl port-forward -n kube-system svc/hubble-relay 4245:4245 &

# Thiết lập biến môi trường
$env:HUBBLE_SERVER = "localhost:4245"

# Kiểm tra Hubble hoạt động
hubble status
# Expected: Healthcheck passed, connected nodes: 10 (all nodes)

# Mở Hubble UI (web dashboard)
kubectl port-forward svc/hubble-ui -n kube-system 8081:80 &
# Truy cập: http://localhost:8081
```

### 6.2 Diagnostic Commands

#### Xem live flows trong namespace

```bash
# Tất cả flows
hubble observe -n his-hope

# Flows từ một label cụ thể
hubble observe -n his-hope --from-label app=patient-service

# Flows đến một service cụ thể
hubble observe -n his-hope --to-label app=clinical-service

# Flows trên một port
hubble observe -n his-hope --port 5006

# Flows với protocol HTTP
hubble observe -n his-hope --protocol http
```

#### Debug network policy issues

```bash
# Xem tất cả DROPPED packets (bị network policy chặn)
hubble observe -n his-hope --verdict DROPPED

# Xem DROPPED packets với details
hubble observe -n his-hope --verdict DROPPED -o json | ConvertFrom-Json | ForEach-Object {
    "$($_.flow.time) | $($_.source.namespace)/$($_.source.pod_name) → $($_.destination.namespace)/$($_.destination.pod_name) :$($_.destination.labels.app) port:$($_.l4.TCP.destination_port) [DROPPED]"
}

# Xem FORWARDED traffic giữa 2 services cụ thể
hubble observe -n his-hope \
  --from-label app=patient-service \
  --to-label app=clinical-service \
  --verdict FORWARDED
```

#### Phân tích traffic patterns

```bash
# Top talkers trong namespace (HTTP requests)
hubble observe -n his-hope --protocol http --since 5m -o json | \
  ConvertFrom-Json | Group-Object { "$($_.source.pod_name) → $($_.destination.pod_name)" } | \
  Sort-Object Count -Descending | Select-Object -First 10 Count, Name

# Flows trong time window cụ thể
hubble observe -n his-hope --since 2026-07-16T12:00:00Z --until 2026-07-16T12:05:00Z

# Export flows ra file để phân tích offline
hubble observe -n his-hope --since 1h -o json > .\diagnostics\hubble_flows_1h.json
```

#### Bảo mật — Phát hiện anomalous flows

```bash
# Flows đến external IPs (có thể là data exfiltration)
hubble observe -n his-hope --not-to-label app.kubernetes.io/part-of=his-hope --since 15m

# Flows từ port không expected (reconnaissance)
hubble observe -n his-hope --port 22  # SSH từ pod → suspicious

# DNS queries (domain-based threat detection)
hubble observe -n his-hope --to-port 53 --protocol UDP
```

---

## 7. Health Check Endpoint Usage

### 7.1 Endpoints

| Endpoint | Purpose | Probe Config | Port |
|----------|---------|-------------|------|
| `GET /health` | Liveness — service còn sống không? | `initialDelaySeconds: 30, periodSeconds: 15, failureThreshold: 3` | HTTP port service |
| `GET /health/ready` | Readiness — service sẵn sàng nhận traffic? | `initialDelaySeconds: 10, periodSeconds: 10, failureThreshold: 2` | HTTP port service |
| `GET /health/startup` | Startup — service khởi động xong chưa? | `initialDelaySeconds: 5, periodSeconds: 5, failureThreshold: 40` | HTTP port service |

### 7.2 Health Check Response (Full)

```json
{
  "status": "Healthy",
  "duration": 123.45,
  "checks": [
    { "name": "cockroachdb-patient", "status": "Healthy", "duration": 5.2 },
    { "name": "rabbitmq", "status": "Healthy", "duration": 12.1 },
    { "name": "redis-cluster", "status": "Healthy", "duration": 3.8 },
    { "name": "grpc-identity-service", "status": "Healthy", "duration": 8.1 },
    { "name": "linkerd-mtls", "status": "Healthy", "duration": 1.2 }
  ]
}
```

### 7.3 Manual Health Check Commands

```bash
# Check từng service một
kubectl exec -it deploy/patient-service -n his-hope -- sh -c "wget -qO- http://localhost:5002/health"
kubectl exec -it deploy/identity-service -n his-hope -- sh -c "wget -qO- http://localhost:5003/health"
kubectl exec -it deploy/clinical-service -n his-hope -- sh -c "wget -qO- http://localhost:5005/health"
kubectl exec -it deploy/appointment-service -n his-hope -- sh -c "wget -qO- http://localhost:5004/health"
kubectl exec -it deploy/api-gateway -n his-hope -- sh -c "wget -qO- http://localhost:5000/health"

# Check readiness (tất cả dependencies sẵn sàng?)
kubectl exec -it deploy/patient-service -n his-hope -- sh -c "wget -qO- http://localhost:5002/health/ready"

# Debug health check cho một dependency cụ thể
kubectl exec -it deploy/patient-service -n his-hope -- sh -c "wget -qO- http://localhost:5002/health/ready 2>&1" | ConvertFrom-Json | Select-Object -ExpandProperty checks | Where-Object { $_.status -ne "Healthy" }
```

---

## 8. Common Diagnostic Commands

### 8.1 Quick Health Assessment (Single Command)

```bash
# Script đánh giá nhanh sức khỏe toàn bộ hệ thống
# Chạy từ workstation kết nối đến cluster

Write-Host "=== Kubernetes Resources ===" -ForegroundColor Cyan
kubectl get pods -n his-hope | Select-String "0/|Error|CrashLoop|Pending"
kubectl top pods -n his-hope --sort-by=cpu | Select-Object -First 5
kubectl top nodes

Write-Host "`n=== Linkerd Mesh Status ===" -ForegroundColor Cyan
linkerd viz stat deploy -n his-hope -o wide

Write-Host "`n=== CockroachDB Status ===" -ForegroundColor Cyan
kubectl exec -it cockroachdb-0 -n his-hope -- cockroach node status --insecure

Write-Host "`n=== Redis Cluster ===" -ForegroundColor Cyan
kubectl exec -it redis-0 -n his-hope -- redis-cli CLUSTER INFO | Select-String "cluster_state|cluster_slots_ok"

Write-Host "`n=== Active Prometheus Alerts ===" -ForegroundColor Cyan
kubectl exec -it deploy/prometheus-server -n monitoring -- wget -qO- http://localhost:9090/api/v1/alerts | ConvertFrom-Json | Select-Object -ExpandProperty data | Select-Object -ExpandProperty alerts | Where-Object { $_.state -eq "firing" } | Format-Table labels.alertname, annotations.summary
```

### 8.2 Service-Specific Debug

```bash
# === Patient Service ===
kubectl logs deploy/patient-service -n his-hope --tail=50 --timestamps
kubectl exec -it deploy/patient-service -n his-hope -- sh -c "wget -qO- http://localhost:5002/health" | ConvertFrom-Json

# === Identity Service (auth problems) ===
kubectl logs deploy/identity-service -n his-hope --tail=50 | Select-String "login|token|auth"
kubectl exec -it deploy/identity-service -n his-hope -- sh -c "wget -qO- http://localhost:5003/health" | ConvertFrom-Json

# === CockroachDB ===
kubectl exec -it cockroachdb-0 -n his-hope -- cockroach sql --insecure -e "
  SELECT node_id, address, is_live, ranges, replicas
  FROM crdb_internal.gossip_nodes;
"

# === RabbitMQ ===
kubectl exec -it rabbitmq-0 -n his-hope -- rabbitmqctl list_queues name messages consumers | Where-Object { $_ -match '^\S+\s+(\d+)' -and $Matches[1] -gt 100 }

# === Redis ===
kubectl exec -it redis-0 -n his-hope -- redis-cli INFO memory | Select-String "used_memory_human|maxmemory_human|evicted_keys|keyspace_hits|keyspace_misses"
kubectl exec -it redis-0 -n his-hope -- redis-cli --latency-dist
```

### 8.3 Network Connectivity Debug

```bash
# Test kết nối từ một pod đến một service
kubectl run netdebug --rm -it --image=nicolaka/netshoot --restart=Never -n his-hope -- sh -c '
  echo "=== DNS resolution ==="
  nslookup patient-service.his-hope.svc.cluster.local
  echo "=== TCP connectivity ==="
  nc -zv patient-service 5002
  echo "=== HTTP health ==="
  wget -qO- http://patient-service:5002/health
'

# Test gRPC connectivity
kubectl run grpcdebug --rm -it --image=fullstorydev/grpcurl:latest --restart=Never -n his-hope -- \
  -plaintext patient-service.his-hope.svc.cluster.local:5006 list

# Trace path của network policy (xem flow có bị block không)
hubble observe --from-pod netdebug --to-label app=patient-service -n his-hope --verdict DROPPED
```

### 8.4 Performance Profiling

```bash
# CPU/Memory real-time per pod
kubectl top pods -n his-hope --containers

# Xem process CPU breakdown trong pod
kubectl exec -it deploy/patient-service -n his-hope -- sh -c "top -bn1 | head -20"

# Xem dotnet process metrics (nếu dotnet-counters enabled)
kubectl exec -it deploy/patient-service -n his-hope -- dotnet-counters monitor -p 1 --counters System.Runtime

# Linkerd tap để xem latency real-time per request
linkerd viz tap deploy/patient-service -n his-hope --path "/api/v1/patients/*" -o json | ForEach-Object {
    $req = $_ | ConvertFrom-Json
    "$($req.requestInitiatedTime) | $($req.sourceMeta.labels.app) → $($req.targetMeta.labels.app) | $($req.httpMethod) $($req.path) | $($req.responseLatency)µs"
}
```

---

## 9. Monitoring Stack Architecture

### 9.1 Component Map

```
Application Services (his-hope namespace)
  │
  ├── OpenTelemetry SDK (auto-instrument)
  │     ├── Traces → OpenTelemetry Collector (DaemonSet) → Jaeger (linkerd-jaeger)
  │     └── Metrics → Prometheus (monitoring namespace, port 9090)
  │
  ├── Serilog structured logs → Elasticsearch (monitoring) → Kibana (:5601)
  │
  └── linkerd-proxy sidecar → Linkerd Viz (:8084) → Grafana (:3000)

Infrastructure:
  ├── Cilium agent → Hubble Relay → Hubble UI (:8081)
  ├── Prometheus → Alertmanager (:9093) → PagerDuty + Slack
  └── Grafana dashboards ← Prometheus datasource
```

### 9.2 Port Forward Map

```bash
# Quick-access script — mở tất cả dashboards
kubectl port-forward svc/grafana -n monitoring 3000:3000 &          # Grafana
kubectl port-forward svc/prometheus-server -n monitoring 9090:9090 & # Prometheus
kubectl port-forward svc/alertmanager -n monitoring 9093:9093 &      # Alertmanager
kubectl port-forward svc/kibana -n monitoring 5601:5601 &            # Kibana
kubectl port-forward svc/jaeger-query -n linkerd-jaeger 16686:16686 & # Jaeger
kubectl port-forward svc/hubble-ui -n kube-system 8081:80 &          # Hubble UI
linkerd viz dashboard &                                                # Linkerd Viz

# Access URLs:
# Grafana:      http://localhost:3000
# Prometheus:   http://localhost:9090
# Alertmanager: http://localhost:9093
# Kibana:       http://localhost:5601
# Jaeger:       http://localhost:16686
# Hubble UI:    http://localhost:8081
# Linkerd Viz:  http://localhost:50750 (auto-assigned)
```
