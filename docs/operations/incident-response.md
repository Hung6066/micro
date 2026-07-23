# Sổ tay Ứng phó Sự cố — His.Hope EMR

> **Tài liệu:** OPS-IRP-001
> **Version:** 1.0
> **Audience:** SRE Team, DevOps Engineer, Engineering Manager, CTO
> **Cập nhật:** 2026-07-23

---

## 1. Mức độ Nghiêm trọng (Severity Levels)

### 1.1 Định nghĩa và SLA

| Level | Tên | Định nghĩa | Acknowledge SLA | Resolve SLA |
|-------|-----|-----------|----------------|-------------|
| **P0** | Critical | Hệ thống ngừng hoạt động toàn bộ, nguy cơ an toàn bệnh nhân, mất dữ liệu ePHI, toàn bộ người dùng không thể truy cập | **5 phút** | **30 phút** |
| **P1** | High | Một dịch vụ critical ngừng hoạt động (identity, patient, clinical), ảnh hưởng >50% người dùng, clinical workflow bị gián đoạn | **15 phút** | **1 giờ** |
| **P2** | Medium | Dịch vụ non-critical degraded, SLO error budget burn > 10x, hiệu năng suy giảm nghiêm trọng | **30 phút** | **4 giờ** |
| **P3** | Low | Suy giảm nhẹ, warning alerts, ảnh hưởng một vài người dùng, non-production issue | **2 giờ** | **Ngày làm việc tiếp theo** |
| **P4** | Info | Lỗi thẩm mỹ (cosmetic), nhiễu giám sát (monitoring noise), enhancement request | **1 tuần** | **1 tuần** |

### 1.2 Ví dụ cho từng mức

| Level | Ví dụ cụ thể |
|-------|-------------|
| **P0** | CockroachDB cluster toàn bộ node down, BFF Gateway không phản hồi, mất kết nối đến tất cả service, ePHI bị exfiltrate đang diễn ra |
| **P1** | Identity Service ngừng hoạt động (không ai đăng nhập được), Patient Service timeout hàng loạt, RabbitMQ cluster mất quorum |
| **P2** | Pharmacy Service degraded (đơn thuốc chậm > 30s), SLO error budget cháy ở mức 10x, Linkerd proxy latency spikes trên 500ms |
| **P3** | Một pod restart liên tục nhưng không ảnh hưởng traffic, disk usage > 80% warning, non-production environment issue |
| **P4** | UI hiển thị sai chính tả, log format không đúng chuẩn, metric label thiếu, alert rule tạo noise |

---

## 2. Luân phiên Trực ca (On-Call Rotation)

### 2.1 Vai trò

| Vai trò | Trách nhiệm | Phạm vi |
|---------|------------|---------|
| **Primary On-Call** | Tiếp nhận, xác nhận (acknowledge), phân loại (triage) và xử lý tất cả alert P0/P1. Trực 24/7 trong tuần trực. | Tất cả production alerts |
| **Secondary On-Call** | Backup cho Primary. Can thiệp nếu Primary không phản hồi trong 10 phút đối với P0 hoặc 15 phút đối với P1. | P0/P1 escalation |
| **SRE Lead** | Giám sát quy trình trực ca, hỗ trợ kỹ thuật cho các sự cố phức tạp, ra quyết định rollback/hotfix. | Tư vấn & phê duyệt |

### 2.2 Lịch trực

- **Chu kỳ:** Luân phiên hàng tuần, bắt đầu từ **Thứ Hai 9:00 AM** đến **Thứ Hai tuần sau 9:00 AM** (giờ Việt Nam, UTC+7).
- **Công cụ:** Quản lý qua PagerDuty với escalation policy tự động.
- **Handoff:** Primary sắp hết tuần trực gửi handoff notes vào Slack `#his-hope-incidents` trước Thứ Sáu 5:00 PM. Nội dung handoff bao gồm:
  - Các sự cố đang mở và trạng thái hiện tại
  - Các alert đang bị silence và lý do
  - Các thay đổi infrastructure gần đây (deployment, config change, migration)
  - Các known issue chưa được giải quyết

### 2.3 Thông tin liên hệ

| Người/Vai trò | Số điện thoại | PagerDuty |
|--------------|---------------|-----------|
| Primary On-Call | +84-xxx-xxx-xxxx | pd-primary@his-hope.vn |
| Secondary On-Call | +84-xxx-xxx-xxxx | pd-secondary@his-hope.vn |
| Engineering Manager | +84-xxx-xxx-xxxx | pd-em@his-hope.vn |
| CTO | +84-xxx-xxx-xxxx | pd-cto@his-hope.vn |

---

## 3. Vòng đời Sự cố (Incident Lifecycle)

Quy trình 10 bước từ phát hiện đến đánh giá:

```
[1.Phát hiện] → [2.Xác nhận] → [3.Phân loại] → [4.Điều tra] → [5.Giảm nhẹ]
→ [6.Khắc phục] → [7.Xác minh] → [8.Post-mortem] → [9.Action Items] → [10.Đánh giá]
```

### Bước 1: Phát hiện (Detect)

- Alert được kích hoạt từ hệ thống giám sát (Prometheus + Grafana alert rules).
- Nguồn alert: Prometheus metrics, Linkerd service mesh telemetry, RabbitMQ dead-letter queues, CockroachDB health checks, Kubernetes events, Pingdom synthetic checks.
- Alert tự động gửi đến PagerDuty để kích hoạt quy trình.

### Bước 2: Xác nhận (Acknowledge)

- Primary On-Call nhận thông báo từ PagerDuty (phone call cho P0, push notification cho P1/P2).
- **Acknowledge alert trong PagerDuty ngay lập tức** — việc này dừng chuỗi escalation và thông báo cho team rằng đã có người xử lý.
- Nếu không acknowledge trong SLA, PagerDuty tự động escalate theo escalation policy.

### Bước 3: Phân loại (Triage)

- Xác định mức độ P0-P4 dựa trên bảng severity ở Mục 1.
- Đặt severity trong PagerDuty incident.
- Mở kênh Slack `#his-hope-incidents` và thông báo:
  ```
  INCIDENT [P0/P1/P2]: [Mô tả ngắn]
  Incident ID: [PagerDuty incident URL]
  Severity: [P0-P4]
  Dịch vụ bị ảnh hưởng: [danh sách]
  Primary: @oncall
  ```
- Nếu là P0 hoặc P1 có nguy cơ an toàn bệnh nhân, thông báo ngay cho Engineering Manager qua điện thoại.

### Bước 4: Điều tra (Investigate)

- Kiểm tra các dashboard giám sát theo thứ tự ưu tiên:
  1. **Grafana Service Overview** — health status của tất cả service
  2. **Linkerd Dashboard** — service mesh metrics, success rate, latency
  3. **Prometheus Alerts** — các alert đang firing
  4. **Kubernetes Dashboard** — pod status, restarts, resource usage
  5. **CockroachDB Console** — query performance, node health
  6. **RabbitMQ Management** — queue depth, consumer status
  7. **Logs (Loki)** — tìm kiếm error patterns liên quan
- Tạo timeline sự cố trong Slack thread để team cùng theo dõi.
- Nếu cần thêm người, gọi Secondary On-Call hoặc chuyên gia domain cụ thể.

### Bước 5: Giảm nhẹ (Mitigate)

- **Mục tiêu:** Ngăn chặn thiệt hại lan rộng, không cần giải quyết root cause.
- Các hành động mitigate phổ biến:
  - **Rollback deployment** — nếu sự cố xuất hiện sau deploy gần đây
  - **Scale up pods** — nếu do resource exhaustion
  - **Failover database** — nếu node CockroachDB bị lỗi
  - **Drain node** — nếu Kubernetes node unhealthy
  - **Circuit breaker** — kích hoạt manual circuit breaker qua Linkerd
  - **Restart service** — nếu memory leak hoặc deadlock
- **Nguyên tắc:** Hành động mitigate phải an toàn, có thể đảo ngược (reversible), và ưu tiên khôi phục dịch vụ hơn là tìm root cause.

### Bước 6: Khắc phục (Resolve)

- Sau khi mitigate xong, tiến hành tìm và sửa root cause.
- Các bước điển hình:
  - Phân tích logs xung quanh thời điểm sự cố
  - So sánh metric trước/sau sự cố (Prometheus compare)
  - Kiểm tra các thay đổi gần đây: deployment, config, migration, infrastructure change
  - Tạo hotfix branch nếu cần code change
  - Deploy hotfix qua pipeline CI/CD (Tekton → ArgoCD)
- **Không vội đóng incident** cho đến khi xác nhận root cause đã được sửa.

### Bước 7: Xác minh (Verify)

- Tất cả health checks phải trở về **green**.
- Kiểm tra các metric sau khắc phục trong ít nhất **15 phút** đối với P0/P1, **30 phút** đối với P2.
- Chạy smoke test / synthetic check từ Pingdom.
- Xác nhận từ Slack channel rằng người dùng/stakeholder không còn thấy vấn đề.
- Cập nhật Slack thread với trạng thái `RESOLVED`.
- Đóng PagerDuty incident.

### Bước 8: Post-mortem

- **Deadline:** Post-mortem document phải được hoàn thành trong vòng **24 giờ** sau khi incident được resolve (P0/P1) hoặc **48 giờ** (P2).
- Template: `docs/runbooks/post-mortem-template.md`
- Nội dung bắt buộc:
  - Timeline sự cố (phát hiện → acknowledge → mitigate → resolve)
  - Root cause analysis (5 Whys)
  - Impact assessment (thời gian downtime, số người dùng bị ảnh hưởng, dữ liệu bị ảnh hưởng)
  - Hành động đã thực hiện (mitigation + resolution)
  - Bài học rút ra (what went well, what went wrong)

### Bước 9: Action Items

- Tạo Jira ticket cho từng action item từ post-mortem.
- Phân loại priority:
  - **Blocker/P0:** Phải làm ngay trong 24h (ví dụ: hotfix bảo mật, vá lỗ hổng)
  - **High/P1:** Phải làm trong sprint hiện tại (ví dụ: thêm alert rule, cải thiện monitoring)
  - **Medium/P2:** Trong 1-2 sprint tới (ví dụ: refactor module, thêm integration test)
  - **Low/P3:** Backlog (ví dụ: cập nhật tài liệu, cải tiến quy trình)
- Gán Jira ticket cho người chịu trách nhiệm cụ thể.
- Link Jira ticket vào post-mortem document.

### Bước 10: Đánh giá (Weekly SRE Review)

- **Thời gian:** Thứ Sáu hàng tuần, 4:00 PM (giờ Việt Nam).
- **Thành phần:** SRE Team, Engineering Manager, đại diện DevOps.
- **Nội dung cuộc họp:**
  - Review tất cả incident P0/P1 trong tuần
  - Đánh giá tiến độ action items từ post-mortem
  - Phân tích xu hướng sự cố (tần suất, loại, service bị ảnh hưởng nhiều nhất)
  - Đánh giá hiệu quả của on-call rotation
  - Đề xuất cải tiến quy trình, công cụ, monitoring
  - Review SLO/SLI và error budget status
- Biên bản cuộc họp được lưu tại `docs/operations/sre-review/YYYY-MM-DD.md`.

---

## 4. Ma trận Leo thang (Escalation Matrix)

### 4.1 P0 — Critical

```
P0 Alert kích hoạt
  ├── 0 phút: PagerDuty gọi Primary On-Call (phone call)
  ├── 5 phút: Nếu chưa acknowledge → PagerDuty gọi Secondary On-Call
  ├── 10 phút: Nếu vẫn chưa acknowledge → PagerDuty gọi Engineering Manager
  └── 15 phút: Nếu vẫn chưa acknowledge → PagerDuty gọi CTO
```

### 4.2 P1 — High

```
P1 Alert kích hoạt
  ├── 0 phút: PagerDuty push notification đến Primary On-Call
  ├── 15 phút: Nếu chưa acknowledge → PagerDuty gọi Secondary On-Call
  ├── 1 giờ: Nếu chưa resolve → Thông báo Tech Lead
  └── 2 giờ: Nếu vẫn chưa resolve → Thông báo Engineering Manager
```

### 4.3 P2 — Medium

```
P2 Alert kích hoạt
  ├── 0 phút: PagerDuty push notification đến Primary On-Call
  ├── 30 phút: Nếu chưa acknowledge → Slack reminder trong #his-hope-incidents
  └── 4 giờ: Nếu chưa resolve → Thông báo Tech Lead
```

### 4.4 P3 / P4

```
P3/P4 → Slack #his-hope-alerts (không pager, không escalation tự động)
```

---

## 5. Kênh Giao tiếp (Communication Channels)

### 5.1 Tổng quan kênh

| Kênh | Mục đích | Đối tượng | Tần suất |
|------|----------|-----------|---------|
| **Slack #his-hope-incidents** | Phối hợp real-time trong sự cố | SRE Team, DevOps, Dev leads | Mỗi sự cố |
| **Slack #his-hope-alerts** | Non-critical alert notification | SRE Team | Liên tục |
| **PagerDuty** | Alerting + escalation tự động | On-Call engineers | 24/7 |
| **Status Page** | Cập nhật trạng thái cho bệnh viện | Ban giám đốc bệnh viện, trưởng khoa, IT admin bệnh viện | Mỗi sự cố P0/P1 |
| **Email** | Phân phối post-mortem, thông báo bảo trì | Tất cả stakeholders | Sau mỗi sự cố hoặc bảo trì |

### 5.2 Slack #his-hope-incidents — Quy tắc

- **KHÔNG** thảo luận ngoài lề. Kênh này chỉ dành cho phối hợp sự cố.
- Mỗi sự cố = một Slack thread riêng trong kênh.
- Format message đầu thread:

  ```
  🚨 INCIDENT: [mô tả ngắn]
  Severity: [P0/P1/P2]
  Service: [tên dịch vụ]
  PagerDuty: [link]
  Primary: @oncall
  ```

- Cập nhật timeline vào thread mỗi khi có thay đổi trạng thái.
- Khi resolve, gửi message cuối cùng:
  ```
  ✅ RESOLVED: [mô tả ngắn]
  Duration: [thời gian]
  Root cause: [tóm tắt 1 dòng]
  Post-mortem: [link]
  ```

### 5.3 Status Page

- URL: `https://status.his-hope.vn`
- Cập nhật status page **trong vòng 15 phút** sau khi xác nhận P0/P1.
- Các thành phần cần cập nhật:
  - **Operational:** Dịch vụ hoạt động bình thường
  - **Degraded Performance:** Dịch vụ chậm nhưng vẫn hoạt động
  - **Partial Outage:** Một phần người dùng bị ảnh hưởng
  - **Major Outage:** Toàn bộ dịch vụ ngừng hoạt động
  - **Under Maintenance:** Bảo trì có kế hoạch

### 5.4 Email — Post-mortem Distribution

- Gửi post-mortem đến: `sre-team@his-hope.vn`, `engineering@his-hope.vn`
- CC: CTO (với P0), Engineering Manager (với P0/P1)
- Subject format: `[POST-MORTEM] [P0/P1/P2] <mô tả ngắn> — <ngày>`

---

## 6. Sau Sự cố (Post-Incident)

### 6.1 Post-mortem Document

- **Mẫu:** Sử dụng file template tại `docs/runbooks/post-mortem-template.md`
- **Nơi lưu:** `docs/operations/post-mortems/YYYY-MM-DD-<mô tả>.md`
- **Deadline P0/P1:** 24 giờ sau resolve
- **Deadline P2:** 48 giờ sau resolve
- **Deadline P3/P4:** Không bắt buộc, khuyến khích ghi chú ngắn gọn

### 6.2 Action Items trong Jira

Tất cả action items từ post-mortem phải được tạo thành Jira ticket với:

- **Project:** `HISHOPE` hoặc `SRE`
- **Labels:** `post-mortem`, `incident-<P0/P1/P2>`
- **Priority:** Theo phân loại ở Mục 3, Bước 9
- **Assignee:** Chỉ định người cụ thể
- **Due date:** Theo mức ưu tiên (blocker: 24h, high: hết sprint, medium: 2 sprint)
- **Linked issue:** Link đến post-mortem document

### 6.3 Weekly SRE Review

Cuộc họp định kỳ để đảm bảo vòng phản hồi đóng lại:

| Thông tin | Chi tiết |
|-----------|---------|
| **Thời gian** | Thứ Sáu, 4:00 PM — 5:00 PM (UTC+7) |
| **Địa điểm** | Google Meet (link trong calendar invite) |
| **Thành phần** | SRE Team (bắt buộc), Engineering Manager, DevOps Lead |
| **Agenda** | Xem Mục 3, Bước 10 |

### 6.4 Cải tiến Liên tục

Sau mỗi chu kỳ đánh giá, các cải tiến được đề xuất sẽ được theo dõi qua:
- **Jira Epic:** `SRE Process Improvement`
- **Metrics theo dõi:** MTTR (Mean Time to Resolve), MTTD (Mean Time to Detect), incident frequency, false positive rate của alert
- **Review hàng quý:** Đánh giá xu hướng dài hạn và điều chỉnh SLA nếu cần

---

## 7. Công cụ và Tài nguyên

| Công cụ | Mục đích | URL / Cách truy cập |
|---------|----------|---------------------|
| **PagerDuty** | Alerting & on-call management | https://his-hope.pagerduty.com |
| **Grafana** | Dashboards & metrics visualization | https://grafana.his-hope.vn |
| **Prometheus** | Metrics collection & alert rules | https://prometheus.his-hope.vn |
| **Loki** | Log aggregation & search | https://loki.his-hope.vn (qua Grafana) |
| **Kubernetes** | Container orchestration | `kubectl` context `his-hope-prod` |
| **CockroachDB** | Database cluster management | https://crdb-admin.his-hope.vn |
| **RabbitMQ** | Message broker management | https://rabbitmq.his-hope.vn |
| **ArgoCD** | GitOps deployment | https://argocd.his-hope.vn |
| **Linkerd** | Service mesh dashboard | `linkerd dashboard --context his-hope-prod` |
| **Pingdom** | Synthetic checks & uptime | https://my.pingdom.com |
| **Status Page** | Public status communication | https://status.his-hope.vn |
| **Jira** | Issue tracking & action items | https://his-hope.atlassian.net |
| **Vault** | Secrets management | https://vault.his-hope.vn |

---

## 8. Runbooks Nhanh

Các runbook chi tiết cho các tình huống cụ thể được lưu tại `docs/runbooks/`:

| Runbook | File | Áp dụng cho |
|---------|------|------------|
| Service Unavailable | `service-unavailable.md` | P0/P1 — Một service không phản hồi |
| Failed Deployment Rollback | `failed-deployment-rollback.md` | P0/P1 — Rollback sau deploy lỗi |
| CockroachDB Failure | `crdb-failure.md` | P0 — Database cluster failure |
| RabbitMQ Failure | `rabbitmq-failure.md` | P1 — Message broker mất quorum |
| Redis Failure | `redis-failure.md` | P2 — Cache failure, token blacklist |
| OOM Kill | `oom-kill.md` | P1/P2 — Pod bị kill do out-of-memory |
| High Latency | `high-latency.md` | P2 — Service response time cao |
| Brute Force Attack | `brute-force.md` | P1 — Phát hiện tấn công brute-force |
| PHI Exfiltration | `phi-exfiltration.md` | P0 — Nghi ngờ rò rỉ dữ liệu bệnh nhân |
| Token Theft | `token-theft.md` | P0/P1 — JWT token bị đánh cắp |
| Error Response | `error-response-runbook.md` | P3/P4 — Debug error response pattern |
| BFF Canary Rollout | `bff-canary-rollout.md` | P1 — Rollback BFF canary deployment |

---

## 9. Phụ lục

### 9.1 Định nghĩa viết tắt

| Từ viết tắt | Định nghĩa |
|------------|------------|
| **SRE** | Site Reliability Engineering |
| **SLA** | Service Level Agreement |
| **SLO** | Service Level Objective |
| **SLI** | Service Level Indicator |
| **MTTR** | Mean Time to Resolve |
| **MTTD** | Mean Time to Detect |
| **ePHI** | Electronic Protected Health Information |
| **PII** | Personally Identifiable Information |
| **BFF** | Backend for Frontend |
| **EMR** | Electronic Medical Records |

### 9.2 Lịch sử tài liệu

| Phiên bản | Ngày | Tác giả | Thay đổi |
|-----------|------|---------|---------|
| 1.0 | 2026-07-23 | SRE Team | Phiên bản đầu tiên |

---

> **Ghi chú:** Tài liệu này được đánh giá và cập nhật hàng quý. Mọi đề xuất thay đổi vui lòng gửi qua Pull Request đến repo `D:\AI\micro` với label `docs/operations`.
