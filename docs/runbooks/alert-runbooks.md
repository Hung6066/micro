# His.Hope Alert Runbooks

> **Last updated**: 2026-07-23 | **Maintainer**: @sre | **Next review**: 2026-09-23

Runbooks cho 6 alert rules được định nghĩa trong `docker/rules/alert-rules.yml`.
Mỗi runbook bao gồm: mức độ nghiêm trọng, tác động, hành động ngay lập tức, tự động khắc phục, và quy trình leo thang.

---

## 1. ServiceDown

| Field | Value |
|-------|-------|
| **Severity** | critical |
| **Trigger** | `up == 0` trong 1 phút |
| **Impact** | Service `{{ $labels.job }}` không phản hồi trên endpoint `/health` |

### Hành động ngay lập tức

1. Xác nhận trạng thái trên Grafana dashboard **"His.Hope Service Overview"** — kiểm tra chỉ số `up` cho service bị alert.
2. Kiểm tra container đang chạy:
   - Docker: `docker ps | grep <service>`
   - Kubernetes: `kubectl get pods -l app=<service> -n his-hope`
3. Kiểm tra logs 50 dòng cuối:
   - Docker: `docker logs <container> --tail 50`
   - Kubernetes: `kubectl logs -l app=<service> -n his-hope --tail=50`
4. Nếu pod bị OOMKilled (exit code 137): tăng memory limit tạm thời — `kubectl set resources deployment/<service> -n his-hope --limits=memory=1Gi`
5. Nếu CrashLoopBackOff: `kubectl rollout restart deployment/<service> -n his-hope` hoặc `docker compose restart <service>`
6. Nếu lỗi kết nối DB: kiểm tra CockroachDB — `kubectl get pods -l app=cockroachdb -n his-hope`, kiểm tra connection string trong Secret/ConfigMap.
7. Nếu ImagePullBackOff: kiểm tra image tag và registry credentials.

### Tự động khắc phục
RemediationOperator sẽ tự động restart `<service>`. Kiểm tra RemediationAction CRD để xem lịch sử: `kubectl get remediationaction -n his-hope`.

### Leo thang
Nếu không giải quyết được trong **15 phút** → leo thang lên **P1 lead** qua PagerDuty.

---

## 2. HighErrorRate

| Field | Value |
|-------|-------|
| **Severity** | warning |
| **Trigger** | Tỉ lệ lỗi 5xx > 5% trong 5 phút, duy trì 2 phút |
| **Impact** | Error rate > 5% trên `{{ $labels.job }}` — người dùng gặp lỗi 500 |

### Hành động ngay lập tức

1. Mở Jaeger traces → lọc theo service `{{ $labels.job }}` + tag `error=true` trong 15 phút gần nhất.
2. Kiểm tra lịch sử deploy: `kubectl rollout history deployment/<service> -n his-hope`
3. Nếu alert xuất hiện ngay sau deploy → rollback: `kubectl rollout undo deployment/<service> -n his-hope`
4. Nếu không liên quan deploy → kiểm tra độ trễ DB:
   - `SELECT avg(latency) FROM crdb_internal.node_metrics;`
   - CockroachDB UI → trang Statements → sắp xếp theo latency
5. Kiểm tra Dead Letter Queue: `SELECT count(*) FROM dead_letter_messages WHERE created_at > now() - interval '1h'`
6. Kiểm tra circuit breaker status: `kubectl get circuitbreaker -n his-hope`
7. Nếu lỗi tập trung vào một upstream dependency → kiểm tra health của dependency đó.

### Tự động khắc phục
Circuit breaker tự động mở khi vượt ngưỡng lỗi. RemediationOperator sẽ tự động rollback nếu phát hiện tương quan với deployment gần nhất.

### Leo thang
Nếu error rate > 10% trong **10 phút** hoặc không giảm sau rollback → leo thang lên **P1 lead**.

---

## 3. SloErrorBudgetBurnCritical

| Field | Value |
|-------|-------|
| **Severity** | critical |
| **Trigger** | `slo:burn_rate:1h > 14.4` VÀ `slo:burn_rate:6h > 14.4` trong 2 phút |
| **Impact** | Ngân sách lỗi SLO đang cháy > 14.4x — sẽ cạn kiệt trong < 1 giờ nếu không can thiệp |

### Hành động ngay lập tức

1. **Đóng băng tất cả deployment**:
   - Kubernetes: `kubectl lock deployment --all -n his-hope`
   - ArgoCD: tạm dừng sync (ArgoCD UI → App → SYNC → DISABLE AUTO-SYNC)
2. Mở Grafana SLO dashboard — xác định service nào và cửa sổ thời gian nào bị ảnh hưởng.
3. Kiểm tra cascade failure: upstream/downstream services có đồng thời bị lỗi không.
4. Kiểm tra các alert khác đang active (HighErrorRate, ServiceDown, HighLatencyP99) — tìm nguyên nhân gốc.
5. Nếu budget < 50% và đang cháy nhanh: kích hoạt **war room** — tập hợp SRE + backend lead.
6. Xác định xem có cần hi sinh tính năng không quan trọng để bảo vệ budget hay không.

### Tự động khắc phục
**Không có** — yêu cầu phán đoán của con người. KHÔNG tự động rollback hoặc restart.

### Leo thang
Ngay lập tức nếu burn rate > 14.4x trong **1 giờ** — leo thang lên **P0 incident commander**.

---

## 4. SloErrorBudgetBurnWarning

| Field | Value |
|-------|-------|
| **Severity** | warning |
| **Trigger** | `slo:burn_rate:1h > 3` VÀ `slo:burn_rate:6h > 3` trong 10 phút |
| **Impact** | Ngân sách lỗi SLO đang cháy > 3x — cần điều tra trước khi thành critical |

### Hành động ngay lập tức

1. Mở Grafana SLO dashboard — kiểm tra service nào đang đốt budget.
2. Kiểm tra lịch sử deploy gần đây: `kubectl rollout history deployment/<service> -n his-hope`
3. Mở Jaeger traces của service bị ảnh hưởng — tìm pattern lỗi lặp lại.
4. Nếu xác định được nguyên nhân (bad deploy, slow DB query, external API timeout) → chuẩn bị phương án khắc phục.
5. Đánh giá budget còn lại: nếu > 70% → có thời gian để fix có kiểm soát. Nếu < 70% → ưu tiên cao hơn.
6. Thông báo cho team qua Slack `#his-hope-alerts` về tình trạng và kế hoạch.

### Tự động khắc phục
**Không có** — cần điều tra thủ công. Cảnh báo sớm để tránh leo thang thành critical.

### Leo thang
Nếu burn rate không giảm sau **30 phút** hoặc chuyển thành critical → leo thang lên **P1 lead**.

---

## 5. HighLatencyP99

| Field | Value |
|-------|-------|
| **Severity** | warning |
| **Trigger** | `slo:latency:p99 > 1.0` (giây) trong 5 phút |
| **Impact** | P99 latency > 1s trên `{{ $labels.job }}` — trải nghiệm người dùng chậm |

### Hành động ngay lập tức

1. Mở Jaeger traces → sắp xếp theo duration giảm dần → xác định operation chậm nhất.
2. Kiểm tra DB slow queries: CockroachDB UI → Statements → lọc theo thời gian > 1s.
3. Kiểm tra KEDA scale status: `kubectl get scaledobject -A` — đảm bảo đủ replicas.
4. Nếu DB-bound: kiểm tra index usage — `SHOW INDEX FROM <table>;` và connection pool — `SELECT * FROM crdb_internal.cluster_sessions WHERE status = 'Active';`
5. Kiểm tra CPU throttling: `kubectl top pods -n his-hope -l app=<service>` — nếu CPU > 80%, cần scale.
6. Kiểm tra Redis latency: `kubectl exec redis-0 -n his-hope -- redis-cli -a $REDIS_PASSWORD LATENCY LATEST`
7. Kiểm tra RabbitMQ consumer lag: `kubectl exec his-hope-rabbitmq-0 -n his-hope -- rabbitmqctl list_queues name messages`

### Tự động khắc phục
HPA tự động scale pods nếu CPU > 70%. Prophet pre-warms cache dựa trên dự đoán tải.

### Leo thang
Nếu P99 > 2s trong **15 phút** hoặc ảnh hưởng đến SLO budget → leo thang lên **P1 lead**.

---

## 6. HighMemoryUsage

| Field | Value |
|-------|-------|
| **Severity** | warning |
| **Trigger** | `process_memory_usage_bytes / 1024 / 1024 > 500` (MB) trong 5 phút |
| **Impact** | Memory > 500MB trên `{{ $labels.job }}` — nguy cơ OOMKill nếu tiếp tục tăng |

### Hành động ngay lập tức

1. Mở Grafana memory timeline cho service `{{ $labels.job }}` — xác định pattern: leak (tăng từ từ) hay spike (tăng đột ngột).
2. Nếu **tăng từ từ (leak)**:
   - Restart service: `kubectl rollout restart deployment/<service> -n his-hope`
   - Tạo bug ticket trên Jira với tag `memory-leak`
3. Nếu **spike đột ngột**:
   - Kiểm tra có query lớn, file upload, hoặc batch job đang chạy không
   - Kiểm tra `kubectl logs -l app=<service> -n his-hope --tail=100` để tìm dấu hiệu
4. Tăng limit tạm thời để tránh OOM: `kubectl set resources deployment/<service> -n his-hope --limits=memory=1Gi`
5. Nếu là .NET service: kiểm tra GC metrics — `dotnet-counters monitor -p 1 System.Runtime[gc-heap-size,working-set]`
6. Capture memory dump nếu cần phân tích offline: `dotnet-dump collect -p 1`

### Tự động khắc phục
**Không có** — cần điều tra nguyên nhân. Memory spike có thể là leak hoặc tải hợp lệ, cần phân biệt.

### Leo thang
Nếu memory > 800MB hoặc pod bị OOMKilled → leo thang lên **P1 lead** ngay lập tức.

---

## Mẫu Xử Lý Chung (Common Resolution Patterns)

Các thao tác thường dùng cho mọi alert:

| Hành động | Lệnh |
|-----------|------|
| **Restart service** | `kubectl rollout restart deployment/<service> -n his-hope` / `docker compose restart <service>` |
| **Rollback deploy** | `kubectl rollout undo deployment/<service> -n his-hope` |
| **Scale up** | `kubectl scale deployment/<service> -n his-hope --replicas=<n>` |
| **Kiểm tra logs** | `kubectl logs -l app=<service> -n his-hope --tail=100` |
| **Kiểm tra traces** | Jaeger UI → filter service + time range |
| **Kiểm tra metrics** | Grafana → dashboard "His.Hope Service Overview" |
| **Kiểm tra DB** | CockroachDB UI → Statements / Sessions |
| **Kiểm tra events** | `kubectl get events -n his-hope --sort-by=.lastTimestamp` |

### Quy trình leo thang

```
warning alert → điều tra 15 phút → không resolve → Slack #his-hope-alerts
                                       ↓
                              30 phút không resolve → P1 lead (PagerDuty)
                                       ↓
critical alert → P1 lead ngay lập tức
                                       ↓
                              SLO budget burn > 14.4x → P0 incident commander
```

---

> **Tham khảo**: Alert rules gốc tại `docker/rules/alert-rules.yml`.
> **Runbook chi tiết từng vấn đề**: `docs/runbooks/oom-kill.md`, `docs/runbooks/high-latency.md`, `docs/runbooks/service-unavailable.md`.
