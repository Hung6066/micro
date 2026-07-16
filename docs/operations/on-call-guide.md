# Hướng dẫn On-Call Engineer — His.Hope EMR

> **Tài liệu:** OPS-ONCALL-001
> **Version:** 1.0
> **Audience:** SRE On-Call, DevOps Engineer
> **Cập nhật:** 2026-07-16

---

## 1. Alert Severity Levels & Response SLA

### 1.1 Severity Definitions

| Level | Tên | Định nghĩa | Response SLA | Resolve SLA |
|-------|-----|-----------|-------------|-------------|
| **P0** | Critical | Hệ thống down hoàn toàn, data loss, ảnh hưởng bệnh nhân | **5 phút** acknowledge | **30 phút** |
| **P1** | High | Một service production down, ảnh hưởng >50% users | **10 phút** acknowledge | **2 giờ** |
| **P2** | Medium | Degraded performance, SLO error budget burning >2x | **30 phút** acknowledge | **8 giờ** |
| **P3** | Low | Non-critical issue, single user affected | **4 giờ** acknowledge | **24 giờ** |
| **P4** | Info | Cosmetic, enhancement request, informational | **Next business day** | **7 ngày** |

### 1.2 PagerDuty Escalation Policy

```
P0 Alert → On-Call SRE (phone call, 5min ack timeout)
  ├── No ack in 5min → SRE Lead (phone call)
  ├── No ack in 10min → Engineering Manager
  └── No ack in 15min → CTO

P1 Alert → On-Call SRE (push notification, 10min ack)
  └── No ack in 15min → SRE Lead

P2 Alert → On-Call SRE (push notification, 30min ack)
  └── No ack in 60min → SRE Lead

P3-P4 → Slack #his-hope-alerts only (no pager)
```

---

## 2. Alert Routing Matrix

### 2.1 Which Alert Goes to Which Team

| Alert Group | Team | PagerDuty Service | Slack Channel |
|-------------|------|-------------------|---------------|
| **SLOErrorBudgetBurnCritical** / **ServiceDown** | SRE | his-hope-sre-p0 | #his-hope-incidents |
| **HighLatencyP99** / **CircuitBreakerOpen** | SRE | his-hope-sre-p1 | #his-hope-incidents |
| **HighCPU** / **HighMemory** / **PodCrashLoop** | Platform | his-hope-platform-p2 | #his-hope-platform |
| **DiskFull** / **PVCThreshold** | Platform | his-hope-platform-p2 | #his-hope-platform |
| **DBConnectionPoolExhausted** / **DBReplicationLag** | Data Platform | his-hope-data-p1 | #his-hope-data |
| **RabbitMQQueueDepth** / **DeadLetterQueue** | Backend | his-hope-backend-p2 | #his-hope-backend |
| **VaultSealed** / **CertExpiry** / **JWTKeyRotation** | Security | his-hope-security-p1 | #his-hope-security |
| **KubeCostBudgetExceeded** | FinOps | his-hope-finops-p3 | #his-hope-finops |
| **ChaosExperimentFailed** | Chaos Engineering | his-hope-chaos-p3 | #his-hope-chaos |
| **MLModelDrift** / **VertexAIEndpointDegraded** | ML/AI | his-hope-ml-p2 | #his-hope-ml |

### 2.2 Alert Source → Prometheus Rule Mapping

```bash
# Liệt kê tất cả active alerts
kubectl port-forward svc/prometheus-server -n monitoring 9090:9090 &
# Mở http://localhost:9090/alerts

# Query các alert rules
kubectl get prometheusrules -n monitoring -o yaml
# → File: k8s/monitoring/prometheus-rules.yaml
```

---

## 3. Common Incident Playbooks

---

### 3.1 Incident: Service Down

**Triệu chứng:** Prometheus alert `ServiceDown` - no metrics from service for 30s.

**Chẩn đoán nhanh:**

```bash
# 1. Kiểm tra pod status
kubectl get pods -n his-hope -l app=<service-name> -o wide

# 2. Kiểm tra events
kubectl get events -n his-hope --sort-by=.lastTimestamp | Select-Object -Last 20

# 3. Kiểm tra logs container chính
kubectl logs deploy/<service-name> -n his-hope --tail=100

# 4. Kiểm tra logs linkerd-proxy sidecar
kubectl logs deploy/<service-name> -n his-hope -c linkerd-proxy --tail=50

# 5. Kiểm tra describe pod (resource limits, OOMKilled, etc.)
kubectl describe pod -n his-hope -l app=<service-name> | Select-String "State|Reason|Exit Code|OOMKilled"
```

**Nguyên nhân phổ biến & cách fix:**

| Nguyên nhân | Chẩn đoán | Fix |
|------------|-----------|-----|
| **OOMKilled (Out of Memory)** | `kubectl describe pod` → `Reason: OOMKilled` | Tăng memory limits hoặc scale HPA: `kubectl patch hpa <service> -n his-hope -p '{"spec":{"maxReplicas":20}}'` |
| **CrashLoopBackOff** | `kubectl get pods` → `STATUS: CrashLoopBackOff` | Xem logs → fix code bug hoặc rollback: `kubectl rollout undo deploy/<service> -n his-hope` |
| **ImagePullBackOff** | `kubectl describe pod` → `Failed to pull image` | Kiểm tra image digest trong `k8s/overlays/prod/image-digests.yaml`; verify container registry accessible |
| **Readiness probe failing** | Pod Running nhưng không Ready | `kubectl exec -it deploy/<service> -n his-hope -- wget -qO- http://localhost:5002/health/ready` |
| **NetworkPolicy blocking** | Pod running nhưng không reachable | `hubble observe --from-pod <pod-name> --verdict DROPPED -n his-hope` |
| **Vault agent injector not ready** | Sidecar vault-agent crash | `kubectl logs deploy/<service> -n his-hope -c vault-agent` |

**Step-by-step fix (phổ biến nhất):**

```bash
# === Pod bị CrashLoopBackOff ===

# 1. Xem logs
kubectl logs deploy/patient-service -n his-hope --previous --tail=200

# 2. Nếu lỗi transient (DB not ready, etc.) → restart
kubectl rollout restart deploy/patient-service -n his-hope

# 3. Nếu lỗi code mới deploy → rollback
kubectl rollout undo deploy/patient-service -n his-hope

# 4. Nếu lỗi resource → scale/adjust
kubectl scale deploy/patient-service -n his-hope --replicas=1
kubectl edit deploy patient-service -n his-hope  # Sửa resource limits

# 5. Verify recovery
kubectl wait deploy/patient-service -n his-hope --for=condition=Available --timeout=120s
linkerd viz stat deploy/patient-service -n his-hope
```

---

### 3.2 Incident: High Latency

**Triệu chứng:** Prometheus alert `HighLatencyP99` hoặc SLO error budget burning.

**Chẩn đoán nhanh:**

```bash
# 1. Kiểm tra Linkerd latency per service
linkerd viz stat deploy -n his-hope -o wide

# 2. Kiểm tra nơi bottleneck (theo trace)
# Port-forward Jaeger UI
kubectl port-forward svc/jaeger-query -n linkerd-jaeger 16686:16686 &
# Mở http://localhost:16686 → tìm traces có latency cao
# Query: service=patient-service AND duration>500ms

# 3. Kiểm tra DB query performance
kubectl exec -it cockroachdb-0 -n his-hope -- cockroach sql --insecure -e "
  SELECT query, mean_latency_ms, max_latency_ms, count
  FROM crdb_internal.node_statement_statistics
  WHERE mean_latency_ms > 100
  ORDER BY max_latency_ms DESC
  LIMIT 20;
"

# 4. Kiểm tra Redis latency
kubectl exec -it redis-0 -n his-hope -- redis-cli --latency-history

# 5. Kiểm tra DB connection pool
kubectl exec -it cockroachdb-0 -n his-hope -- cockroach sql --insecure -e "
  SELECT node_id, session_count, active_sessions
  FROM crdb_internal.cluster_sessions;
"

# 6. Kiểm tra pod resource saturation
kubectl top pods -n his-hope
```

**Nguyên nhân & fix:**

| Nguyên nhân | Chẩn đoán | Fix |
|------------|-----------|-----|
| **DB query chậm** | `mean_latency_ms > 100` trong statement stats | Thêm index, optimize query. Kiểm tra `cockroach/migrations/` schema |
| **Connection pool exhausted** | `active_sessions` ≈ `max_connections` (100 cho clinical, 50 cho patient) | Tăng pool size hoặc scale DB cluster |
| **Redis latency cao** | `redis-cli --latency-history` > 5ms | Kiểm tra Redis memory: `redis-cli INFO memory`. Nếu maxmemory reached → `maxmemory-policy allkeys-lru` đã kick in |
| **CPU throttled** | `kubectl top pods` → CPU limit reached | Scale HPA hoặc tăng CPU limits |
| **Circuit breaker mở** | Polly CB open → trả fallback cached response | Debug nguồn gốc lỗi upstream, CB tự half-open sau 30s |
| **Network latency** | Linkerd latency high at proxy level | `linkerd viz tap deploy/<service> -n his-hope` để xem per-request latency breakdown |

**Step-by-step fix:**

```bash
# 1. Kiểm tra Jaeger trace cho request cụ thể
# Lấy trace_id từ response header hoặc log
# Jaeger query: trace_id=<trace-id>

# 2. Scale service nếu do CPU
kubectl scale deploy/patient-service -n his-hope --replicas=10

# 3. Nếu DB là bottleneck → kiểm tra slow queries
kubectl exec -it cockroachdb-0 -n his-hope -- cockroach sql --insecure -e "
  SELECT fingerprint, count, mean_latency_ms, metadata->>'query'
  FROM crdb_internal.node_statement_statistics
  WHERE mean_latency_ms > 100
  ORDER BY count DESC
  LIMIT 10;
"

# 4. Restart Redis nếu latency source
kubectl rollout restart statefulset/redis -n his-hope

# 5. Verify latency giảm
linkerd viz stat deploy -n his-hope -o wide
```

---

### 3.3 Incident: Database Connection Issues

**Triệu chứng:** Prometheus alert `DBConnectionPoolExhausted` hoặc application logs `NpgsqlException`.

**Chẩn đoán nhanh:**

```bash
# 1. Kiểm tra CockroachDB node status
kubectl exec -it cockroachdb-0 -n his-hope -- cockroach node status --insecure
# Expected: 5 nodes, is_live=true cho tất cả

# 2. Kiểm tra replication health
kubectl exec -it cockroachdb-0 -n his-hope -- cockroach sql --insecure -e "
  SELECT range_id, replicas, available_replicas, unavailable_replicas
  FROM crdb_internal.ranges
  WHERE unavailable_replicas > 0;
"

# 3. Kiểm tra connection pool
kubectl exec -it cockroachdb-0 -n his-hope -- cockroach sql --insecure -e "
  SHOW CLUSTER SETTING server.max_connections_per_gateway;
  SELECT node_id, sum(active_connections) AS total_active FROM crdb_internal.node_sessions GROUP BY node_id;
"

# 4. Kiểm tra queries đang chạy (long-running)
kubectl exec -it cockroachdb-0 -n his-hope -- cockroach sql --insecure -e "
  SELECT query_id, start_time, age(clock_timestamp(), start_time) AS duration, left(query, 100)
  FROM [SHOW CLUSTER QUERIES] WHERE age(clock_timestamp(), start_time) > interval '30 seconds';
"

# 5. Kiểm tra disk space
kubectl exec -it cockroachdb-0 -n his-hope -- cockroach sql --insecure -e "
  SELECT node_id, store_id, capacity_bytes, available_bytes, used_bytes
  FROM crdb_internal.kv_store_status;
"
```

**Scenario: Connection pool exhausted**

```bash
# 1. Kiểm tra max connections setting
kubectl exec -it cockroachdb-0 -n his-hope -- cockroach sql --insecure -e "
  SHOW CLUSTER SETTING server.max_connections_per_gateway;
"

# 2. Nếu hết connections → ngắt các connection idle lâu
kubectl exec -it cockroachdb-0 -n his-hope -- cockroach sql --insecure -e "
  CANCEL QUERIES (SELECT query_id FROM [SHOW CLUSTER QUERIES]
    WHERE age(clock_timestamp(), start_time) > interval '5 minutes' AND application_name != '$ cockroach');
"

# 3. Tăng connection pool size trong Vault secrets (nếu cần)
kubectl exec -it vault-0 -n vault -- vault kv patch secret/his-hope/database/patientdb \
  max_connections=100

# 4. Restart service để pick up config mới
kubectl rollout restart deploy/patient-service -n his-hope
```

**Scenario: Replication lag hoặc unavailable replicas**

```bash
# 1. Kiểm tra replication lag
kubectl exec -it cockroachdb-0 -n his-hope -- cockroach sql --insecure -e "
  SELECT node_id, range_id, replicas, available_replicas
  FROM crdb_internal.ranges WHERE available_replicas < replicas;
"

# 2. Nếu có replicas unavailable → kiểm tra node đó
kubectl get pods -n his-hope -l app=cockroachdb
kubectl describe pod cockroachdb-3 -n his-hope | Select-String "Status|Ready"

# 3. Restart node nếu cần
kubectl delete pod cockroachdb-3 -n his-hope  # StatefulSet sẽ tự restart
```

---

### 3.4 Incident: JWT / Authentication Failures

**Triệu chứng:** Người dùng không login được, API trả về 401, Prometheus alert `JWTKeyRotationFailed`.

**Chẩn đoán nhanh:**

```bash
# 1. Kiểm tra identity-service health
kubectl exec -it deploy/identity-service -n his-hope -- wget -qO- http://localhost:5003/health

# 2. Kiểm tra Vault transit key status
kubectl exec -it vault-0 -n vault -- vault read transit/keys/jwt-signing
# Expected: latest_version, min_decryption_version, deletion_allowed=false

# 3. Kiểm tra JWT public key sync giữa các service
kubectl exec -it vault-0 -n vault -- vault kv get secret/his-hope/jwt/public-key

# 4. Kiểm tra Redis token blacklist
kubectl exec -it redis-0 -n his-hope -- redis-cli DBSIZE

# 5. Kiểm tra token revocation endpoint
curl -X POST http://localhost:5000/api/v1/auth/revoke \
  -H "Authorization: Bearer $token" \
  -d '{"token": "<token-to-revoke>"}'
```

**Scenario: Vault transit key bị rotate nhưng service chưa reload**

```bash
# 1. Kiểm tra version transit key đang active
kubectl exec -it vault-0 -n vault -- vault read transit/keys/jwt-signing

# 2. Force rotate nếu cần (auto-rotate mỗi 720h)
kubectl exec -it vault-0 -n vault -- vault write -f transit/keys/jwt-signing/rotate

# 3. Cập nhật public key mới vào KV
kubectl exec -it vault-0 -n vault -- vault read -field=public_key transit/keys/jwt-signing | \
  vault kv put secret/his-hope/jwt/public-key key_pem=-

# 4. Restart tất cả services để reload JWT public key
kubectl rollout restart deploy -n his-hope

# 5. Verify login hoạt động
curl -X POST http://localhost:5000/api/v1/auth/login \
  -H "Content-Type: application/json" \
  -d '{"username":"admin@hishop.com","password":"Admin@123!"}'
```

**Scenario: Redis token blacklist not working**

```bash
# 1. Kiểm tra Redis connectivity
kubectl exec -it deploy/identity-service -n his-hope -- wget -qO- http://redis:6379 ping || echo "Cannot reach Redis"

# 2. Kiểm tra NetworkPolicy có cho phép identity-service → Redis
kubectl describe networkpolicy allow-grpc-to-identity-service -n his-hope

# 3. Kiểm tra Hubble nếu bị drop
hubble observe --from-pod -l app=identity-service --to-pod -l app.kubernetes.io/name=redis -n his-hope

# 4. Restart Redis nếu cần
kubectl rollout restart statefulset/redis -n his-hope
```

---

### 3.5 Incident: RabbitMQ Issues

**Triệu chứng:** Integration events không được publish/consume, queue depth tăng, Prometheus alert `RabbitMQQueueDepth`.

**Chẩn đoán nhanh:**

```bash
# 1. Kiểm tra RabbitMQ cluster status
kubectl exec -it rabbitmq-0 -n his-hope -- rabbitmqctl cluster_status

# 2. Kiểm tra queue depth (các queue có messages tích tụ)
kubectl exec -it rabbitmq-0 -n his-hope -- rabbitmqctl list_queues name messages consumers | Sort-Object { [int]($_ -split '\s+')[1] } -Descending

# 3. Kiểm tra dead letter queues (messages thất bại sau retry)
kubectl exec -it rabbitmq-0 -n his-hope -- rabbitmqctl list_queues name messages | Select-String "dlq|dead_letter|error"

# 4. Kiểm tra exchanges
kubectl exec -it rabbitmq-0 -n his-hope -- rabbitmqctl list_exchanges name type | Select-String "his_hope"

# 5. Kiểm tra bindings
kubectl exec -it rabbitmq-0 -n his-hope -- rabbitmqctl list_bindings | Select-String "his_hope"
```

**Scenario: Queue depth tăng cao (messages không được consume)**

```bash
# 1. Xác định consumer cho queue
kubectl exec -it rabbitmq-0 -n his-hope -- rabbitmqctl list_consumers | Select-String "clinical.patient"

# 2. Kiểm tra consumer service health
kubectl get pods -n his-hope -l app=clinical-service
kubectl logs deploy/clinical-service -n his-hope --tail=50 | Select-String "rabbitmq|eventbus|consume"

# 3. Kiểm tra Outbox processor có đang chạy không
kubectl logs deploy/patient-service -n his-hope | Select-String "OutboxProcessor|outbox"

# 4. Nếu consumer down → restart
kubectl rollout restart deploy/clinical-service -n his-hope

# 5. Nếu queue quá sâu → purge (cẩn thận: mất messages!)
# Chỉ purge nếu messages là stale/redundant
# kubectl exec -it rabbitmq-0 -n his-hope -- rabbitmqctl purge_queue clinical.patient

# 6. Verify queue giảm
kubectl exec -it rabbitmq-0 -n his-hope -- rabbitmqctl list_queues name messages | Select-String "clinical.patient"
```

**Scenario: Dead letter queue có messages**

```bash
# 1. Inspect dead letter messages
kubectl exec -it rabbitmq-0 -n his-hope -- rabbitmqctl list_queues name messages | Select-String "dlq"

# 2. Xem reason failed
kubectl logs deploy/patient-service -n his-hope --tail=100 | Select-String "dead.letter|dlq|retry.exhausted"

# 3. Nếu có thể retry → move messages từ DLQ về queue gốc
# (Sử dụng shovel plugin hoặc manual script)

# 4. Nếu messages corrupted → purge DLQ
kubectl exec -it rabbitmq-0 -n his-hope -- rabbitmqctl purge_queue clinical.patient.dlq
```

---

## 4. Escalation Paths

### 4.1 Escalation Contacts

```
Level 1: SRE On-Call Engineer
    → PagerDuty rotation (xem lịch trong PagerDuty)
    → Slack: @sre-oncall
    → Phone: [Số điện thoại trong PagerDuty profile]

Level 2: SRE Lead
    → Slack: @sre-lead
    → Phone: [Số điện thoại]

Level 3: Engineering Manager
    → Slack: @eng-manager
    → Phone: [Số điện thoại]

Level 4 (SEV-1 only): CTO
    → Slack: @cto
    → Phone: [Số điện thoại]

Security Incident: Security Team
    → Slack: @security-team
    → PagerDuty: his-hope-security-p1
    → Phone: [Số điện thoại CISO]

HIPAA Breach: Compliance Officer
    → Email: compliance@hishop.com
    → Phone: [Số điện thoại]
    → SLA: Notify trong vòng 24h, báo cáo chính thức trong 60 ngày
```

### 4.2 When to Escalate

| Tình huống | Escalate khi |
|------------|-------------|
| Không resolve được P0 trong 15 phút | → SRE Lead |
| Không resolve được P0 trong 30 phút | → CTO |
| Data loss hoặc data corruption | → Security Team + Compliance Officer |
| Phát hiện security breach | → Security Team ngay lập tức |
| Cần thay đổi infrastructure lớn | → Platform Team Lead |
| Cần rollback production | → Thông báo trong Slack #his-hope-incidents trước khi rollback |

---

## 5. Postmortem Template

Sau mỗi incident P0/P1, tạo postmortem document trong vòng 48h.

### 5.1 Postmortem Structure (Blameless)

```markdown
# Postmortem: [Tên Incident]

| Field | Value |
|-------|-------|
| **Incident ID** | INC-YYYY-MMDD-NNN |
| **Severity** | P0 / P1 / P2 |
| **Date** | YYYY-MM-DD |
| **Duration** | Start → End (X hours Y minutes) |
| **Incident Commander** | [Tên] |
| **Services Affected** | patient-service, identity-service, ... |
| **Users Affected** | X% of users |
| **PagerDuty URL** | [Link] |

---

## Timeline (All times in UTC)

| Time | Event | Actor |
|------|-------|-------|
| 14:30 | Alert fires: ServiceDown for patient-service | Prometheus → PagerDuty |
| 14:31 | On-call acknowledges | @sre-oncall |
| 14:33 | Root cause identified: OOMKilled due to memory leak | @sre-oncall |
| 14:35 | Rollback deployed | @sre-oncall |
| 14:37 | Service restored | @sre-oncall |
| 14:40 | Incident resolved | @sre-oncall |

---

## Root Cause Analysis

**What happened?**
[Mô tả ngắn gọn sự cố]

**Why did it happen?**
[5 Whys analysis]

1. Why did the service crash? → OOMKilled.
2. Why OOM? → Memory gradually increased over 4 hours.
3. Why did memory increase? → New feature introduced unbounded cache in MemoryCache.
4. Why wasn't this caught? → Load test didn't profile memory over time.
5. Why didn't monitoring catch the trend? → Memory usage alert threshold was too high (95%).

---

## Impact

- **Users affected:** X patients, Y providers
- **Data loss:** None / X records lost
- **HIPAA impact:** None / PHI exposure (escalate immediately)
- **Financial impact:** $X (estimated)

---

## Detection

- **How was it detected?** Prometheus alert ServiceDown
- **Time to detect:** 30 seconds (automatic)
- **Time to acknowledge:** 1 minute
- **Time to resolve:** 10 minutes (total)

---

## Resolution

1. [Action 1] — Rollback deployed deployment to previous version
2. [Action 2] — Verified service health
3. [Action 3] — Notified team via #his-hope-incidents

---

## Action Items

| # | Action | Owner | Priority | Due | Status |
|---|--------|-------|----------|-----|--------|
| 1 | Fix memory leak in PatientCache.cs | Backend Team | P0 | 2026-07-18 | [ ] |
| 2 | Add memory profiling to load test suite | QA Team | P1 | 2026-07-25 | [ ] |
| 3 | Lower memory alert threshold to 80% | SRE Team | P1 | 2026-07-17 | [ ] |
| 4 | Add memory trend dashboard in Grafana | SRE Team | P2 | 2026-07-20 | [ ] |

---

## Lessons Learned

- [Bài học 1]
- [Bài học 2]

## Timeline
- **Postmortem published:** YYYY-MM-DD
- **Review meeting:** YYYY-MM-DD

---

**Approved by:** [SRE Manager]
```

---

## 6. Runbook References

### 6.1 Grafana Dashboards

| Dashboard | URL | Purpose |
|-----------|-----|---------|
| **SLO Overview** | `http://grafana:3000/d/slo-overview` | Error budgets, burn rates cho tất cả services |
| **Service Dashboard - Patient** | `http://grafana:3000/d/patient-service` | Request rate, latency, errors cho patient-service |
| **Service Dashboard - Identity** | `http://grafana:3000/d/identity-service` | Auth requests, JWT operations, login rate |
| **Service Dashboard - Clinical** | `http://grafana:3000/d/clinical-service` | Encounter operations, PHI access audit |
| **Service Dashboard - Appointment** | `http://grafana:3000/d/appointment-service` | Appointment scheduling, no-show metrics |
| **CockroachDB Overview** | `http://grafana:3000/d/cockroachdb` | Node status, replication, query performance |
| **Redis Cluster** | `http://grafana:3000/d/redis-cluster` | Memory usage, hit ratio, cluster health |
| **RabbitMQ Overview** | `http://grafana:3000/d/rabbitmq` | Queue depth, message rates, connections |
| **Linkerd Mesh** | `http://grafana:3000/d/linkerd-mesh` | mTLS status, success rates, latency |
| **Kubecost** | `http://grafana:3000/d/kubecost` | Cost per namespace, per service |
| **Auto-Remediation** | `http://grafana:3000/d/auto-remediation` | Self-healing actions, MTTR tracking |
| **NoOps Dashboard** | `http://grafana:3000/d/noops` | Overall system health, automation coverage |

```bash
# Port-forward để truy cập Grafana từ workstation
kubectl port-forward svc/grafana -n monitoring 3000:3000 &

# Export dashboard JSON (backup)
kubectl exec -it deploy/grafana -n monitoring -- \
  curl -s http://admin:admin@localhost:3000/api/dashboards/uid/slo-overview | jq '.'
```

### 6.2 Kibana Queries (Log Analysis)

| Use Case | Kibana Query |
|----------|-------------|
| **Tìm log theo correlation ID** | `correlationId: "abc-123-def"` |
| **Tất cả ERROR trong 15 phút qua** | `level: "Error" AND @timestamp > now-15m` |
| **Tất cả logs của patient-service** | `service: "patient-service" AND @timestamp > now-1h` |
| **Slow requests (>500ms)** | `ElapsedMilliseconds: >500 AND service: *` |
| **Authentication failures** | `messageTemplate: "Authentication failed*" AND @timestamp > now-1h` |
| **Database connection errors** | `message: *NpgsqlException* OR message: *connection* AND level: "Error"` |
| **Outbox processing errors** | `messageTemplate: "Outbox*" AND level: "Error"` |
| **PHI access audit** | `action: "PHI_ACCESS" AND @timestamp > now-24h` |
| **Token revocation events** | `messageTemplate: "Token revoked*" AND service: "identity-service"` |

```bash
# Port-forward Kibana
kubectl port-forward svc/kibana -n monitoring 5601:5601 &
# Mở http://localhost:5601 → Discover
```

### 6.3 Hubble CLI (Network Flow Debugging)

```bash
# Port-forward Hubble Relay
kubectl port-forward -n kube-system svc/hubble-relay 4245:4245 &

# Thiết lập biến môi trường để dùng Hubble CLI
$env:HUBBLE_SERVER = "localhost:4245"

# === Các lệnh hữu ích ===

# 1. Xem live flows từ một namespace
hubble observe -n his-hope

# 2. Xem flows bị DROPPED (network policy issue)
hubble observe -n his-hope --verdict DROPPED

# 3. Xem flows từ pod cụ thể đến pod cụ thể
hubble observe --from-pod default/patient-service-xxx --to-pod default/clinical-service-yyy -n his-hope

# 4. Xem flows trên một port cụ thể
hubble observe -n his-hope --port 5006

# 5. Xem flows với protocol HTTP
hubble observe -n his-hope --protocol http

# 6. Export flows ra JSON để phân tích
hubble observe -n his-hope --since 5m -o json > /tmp/hubble-flows.json

# 7. Xem service map (cần Hubble UI)
kubectl port-forward svc/hubble-ui -n kube-system 8081:80 &
# Mở http://localhost:8081
```

### 6.4 Linkerd Diagnostic Commands

```bash
# 1. Tổng quan service mesh
linkerd viz stat deploy -n his-hope

# 2. Tap live traffic (xem request/response real-time)
linkerd viz tap deploy/patient-service -n his-hope --to deploy/clinical-service

# 3. Xem edges (service-to-service connections) với authority
linkerd viz edges -n his-hope deployment

# 4. Xem routes (per-endpoint stats từ ServiceProfile)
linkerd viz routes -n his-hope svc/patient-service

# 5. Kiểm tra mTLS status
linkerd viz edges -n his-hope deployment -o wide | Select-String "100.0%"

# 6. Xem top (process-level per-pod)
linkerd viz top deploy/patient-service -n his-hope

# 7. Profile (CPU/memory của linkerd-proxy)
linkerd viz profile -n his-hope deploy/patient-service --tap --duration 30s

# 8. Gateway (cross-cluster/region)
linkerd viz gateways -n linkerd-multicluster
```

### 6.5 Health Check Endpoints

| Endpoint | Purpose | Port Example | Response |
|----------|---------|--------------|----------|
| `GET /health` | Liveness + basic checks | 5002 | `{"status":"Healthy","duration":123.45}` |
| `GET /health/ready` | Readiness (tất cả dependencies sẵn sàng) | 5002 | `{"status":"Healthy"}` |
| `GET /health/startup` | Startup probe (warmup) | 5002 | `{"status":"Healthy"}` |

Từng service port:

| Service | HTTP Health Port | gRPC Port |
|---------|-----------------|-----------|
| **patient-service** | 5002 | 5006 |
| **identity-service** | 5003 | 5007 |
| **appointment-service** | 5004 | 5008 |
| **clinical-service** | 5005 | 5009 |
| **lab-service** | 5010 | 5012 |
| **billing-service** | 5020 | 5022 |
| **pharmacy-service** | 5030 | 5032 |
| **api-gateway** | 5000 | — |

```bash
# Check health từng service
kubectl exec -it deploy/patient-service -n his-hope -- wget -qO- http://localhost:5002/health
kubectl exec -it deploy/patient-service -n his-hope -- wget -qO- http://localhost:5002/health/ready
```

---

## 7. Common Diagnostic Commands Cheat Sheet

```bash
# === Kubernetes Quick Check ===
kubectl get pods -n his-hope --sort-by=.status.startTime
kubectl get events -n his-hope --sort-by=.lastTimestamp | Select-Object -Last 30
kubectl top pods -n his-hope --sort-by=cpu
kubectl top nodes

# === CockroachDB Quick Check ===
kubectl exec -it cockroachdb-0 -n his-hope -- cockroach node status --insecure
kubectl exec -it cockroachdb-0 -n his-hope -- cockroach sql --insecure -e "SHOW DATABASES;"

# === Redis Quick Check ===
kubectl exec -it redis-0 -n his-hope -- redis-cli CLUSTER INFO | Select-String "cluster_state"
kubectl exec -it redis-0 -n his-hope -- redis-cli INFO memory | Select-String "used_memory_human|maxmemory"

# === RabbitMQ Quick Check ===
kubectl exec -it rabbitmq-0 -n his-hope -- rabbitmqctl list_queues name messages | Sort-Object { [int]($_ -split '\s+')[1] } -Descending | Select-Object -First 10

# === Linkerd Quick Check ===
linkerd viz stat deploy -n his-hope -o wide
linkerd viz edges -n his-hope deployment

# === Vault Quick Check ===
kubectl exec -it vault-0 -n vault -- vault status -format=json | ConvertFrom-Json | Select-Object initialized, sealed, ha_enabled

# === Network Quick Check ===
hubble observe -n his-hope --since 1m --verdict DROPPED
kubectl get networkpolicies -n his-hope

# === Check Pod Restart Counts ===
kubectl get pods -n his-hope -o custom-columns="NAME:.metadata.name,RESTARTS:.status.containerStatuses[*].restartCount" --no-headers | Where-Object { $_ -notmatch '0' }

# === Check Certificate Expiry (Linkerd) ===
kubectl get secrets -n linkerd -l linkerd.io/control-plane-component=identity -o jsonpath='{.items[0].data.crt}' | base64 -d | openssl x509 -noout -enddate
```
