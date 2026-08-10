# Chính sách SLO & Error Budget — His.Hope EMR

> **Tài liệu:** OPS-SLO-001
> **Version:** 1.0
> **Audience:** SRE, Engineering Manager, CTO
> **Cập nhật:** 2026-07-23

---

## 1. Nguyên lý Error Budget

Error budget = khoảng thời gian cho phép service bị lỗi trong một tháng mà vẫn đạt SLO.

```
Error Budget = (1 - SLO target) × thời gian đo (30 ngày = 43,200 phút)
```

**Ví dụ:** SLO 99.9% → error budget = 0.1% × 43,200 = **43.2 phút/tháng**.

Error budget dùng chung cho tất cả nguyên nhân: lỗi code, deploy fail, infrastructure failure,
planned maintenance. Khi error budget cạn kiệt, tổ chức phải ưu tiên reliability hơn feature velocity.

---

## 2. Burn Rate & Ngưỡng Hành Động

Burn rate = tốc độ tiêu thụ error budget hiện tại so với tốc độ tiêu thụ đều (1x = 1 budget/tháng).

### 2.1 Multi-Window Burn Rate Alerts

Sử dụng 2 time windows để tránh false positives — cả 2 window phải cùng vượt ngưỡng:

| Alert | Short Window | Long Window | Burn Rate | Ý nghĩa |
|-------|-------------|-------------|-----------|---------|
| **Critical** | 1 giờ | 5 phút | > 14.4x | Hết budget trong 1 giờ — page on-call |
| **Warning** | 6 giờ | 30 phút | > 6x | Hết budget trong 3 giờ — Slack notification |
| **Info** | 24 giờ | 2 giờ | > 3x | Hết budget trong 2 ngày — dashboard highlight |

### 2.2 Ngưỡng Burn Rate & Hành Động Tương Ứng

| Burn Rate | Phân loại | Hành động deploy | Hành động kỹ thuật |
|-----------|-----------|------------------|--------------------|
| **< 1x** | Bình thường | Tự do deploy, bao gồm feature flags mới | Không cần action |
| **1x – 3x** | Thận trọng | Chỉ deploy trong giờ hành chính (8:00-18:00 UTC+7) | Review error pattern, mở issue nếu trend tiếp tục |
| **3x – 6x** | Cảnh báo | Freeze deploy không khẩn cấp, chỉ hotfix được phép | SRE team phân tích root cause, escalation lên EM |
| **6x – 14.4x** | Nghiêm trọng | Dừng tất cả deploy, kể cả hotfix có risk | War room (Slack #his-hope-incidents), incident commander chỉ định |
| **> 14.4x** | Khẩn cấp | Toàn bộ pipeline deploy bị khóa tự động | PagerDuty alert → SRE on-call respond trong 5 phút |

---

## 3. SLO Targets Theo Service

| Service | Availability SLO | Latency SLO (p99) | Error Budget / tháng | Độ ưu tiên |
|---------|-----------------|-------------------|----------------------|------------|
| **clinical-service** | 99.99% | 500ms | 4.3 phút | P0 — dữ liệu lâm sàng, ảnh hưởng trực tiếp bệnh nhân |
| **billing-service** | 99.99% | 1s | 4.3 phút | P0 — giao dịch tài chính, audit required |
| **identity-service** | 99.95% | 300ms | 21.6 phút | P1 — auth toàn hệ thống, ảnh hưởng tất cả services |
| **patient-service** | 99.9% | 500ms | 43.2 phút | P1 — hồ sơ bệnh nhân, truy cập thường xuyên |
| **appointment-service** | 99.9% | 1s | 43.2 phút | P1 — lịch hẹn, ảnh hưởng vận hành bệnh viện |
| **lab-service** | 99.9% | 2s | 43.2 phút | P2 — kết quả xét nghiệm |
| **pharmacy-service** | 99.9% | 1s | 43.2 phút | P2 — kê đơn và xuất thuốc |
| **dashboard-bff** | 99.9% | 1s | 43.2 phút | P2 — dashboard nội bộ |

> **Ghi chú:** Các SLO target được thiết lập dựa trên user journey criticality.
> clinical-service và billing-service yêu cầu 99.99% vì ảnh hưởng trực tiếp đến
> an toàn bệnh nhân và giao dịch tài chính. Các SLO này được review hàng quý.

---

## 4. Deployment Gates

Quyết định deploy dựa trên % error budget **còn lại** trong tháng:

| Error Budget còn lại | Trạng thái | Chính sách deploy |
|---------------------|------------|-------------------|
| **> 50%** | XANH | Deploy bất kỳ lúc nào. Feature flags mới được phép. Canary recommended. |
| **20% – 50%** | VÀNG | Chỉ deploy trong giờ hành chính (8:00-18:00 UTC+7). **Bắt buộc canary** (5% → 25% → 100%). Không deploy feature lớn. |
| **5% – 20%** | CAM | **Freeze tất cả deploy.** Chỉ cho phép: hotfix P0/P1, rollback. Cần approval từ SRE Lead + EM. |
| **< 5%** | ĐỎ | **Freeze tuyệt đối.** Mọi thay đổi cần CTO authorization. Ưu tiên reliability work. |
| **< 0%** | ĐEN | War room mode. Toàn bộ team tập trung khôi phục error budget. SLO breach report gửi lên CTO. |

### 4.1 Quy trình Canary Deployment

```
Giai đoạn 1 (5% traffic, 10 phút) → Verify: error rate < baseline × 1.5, latency p99 < SLO
Giai đoạn 2 (25% traffic, 20 phút) → Verify: error rate < baseline × 1.2
Giai đoạn 3 (100% traffic, 30 phút) → Verify: tất cả SLO đạt, không có new alerts
```

Nếu bất kỳ giai đoạn nào fail → tự động rollback qua ArgoCD.

---

## 5. Quy Trình Review Hàng Tháng

### 5.1 Lịch Review

| Thời gian | Hoạt động | Người tham gia |
|-----------|-----------|---------------|
| **Tuần 1 hàng tháng** | Review error budget tháng trước, đánh giá SLO targets | SRE Lead, EM |
| **Tuần 2** | Review action items từ post-mortem, audit deploy gate compliance | SRE team |
| **Tuần 3** | Chaos engineering results, DR test results | SRE, Platform |
| **Tuần 4** | Capacity planning, SLO tuning proposals | SRE Lead, CTO |

### 5.2 Quy Trình Điều Chỉnh SLO

1. **Đề xuất:** SRE Lead hoặc Service Owner tạo proposal document
2. **Phân tích:** Xem xét error budget history 3 tháng gần nhất, user impact, business impact
3. **Review:** SRE team review + EM approve/reject
4. **Triển khai:** Cập nhật SLO config trong `k8s/monitoring/prometheus-rules.yaml` và `docs/slo/`
5. **Thông báo:** Slack `#his-hope-announcements` và cập nhật dashboard Grafana

### 5.3 Điều Kiện Nâng/Hạ SLO

| Điều kiện nâng SLO | Điều kiện hạ SLO |
|-------------------|-----------------|
| Error budget chưa từng cạn kiệt trong 6 tháng liên tiếp | Service liên tục breach SLO dù đã cải thiện reliability |
| p99 latency luôn dưới 50% SLO target | SLO hiện tại quá chặt, error budget thường xuyên < 20% |
| Có yêu cầu business mới đòi hỏi reliability cao hơn | Business chấp nhận lower reliability để tăng velocity |

---

## 6. Công Cụ & Tự Động Hóa

| Công cụ | Mục đích |
|---------|----------|
| **Prometheus recording rules** | Tính toán burn rate multi-window tự động |
| **Prometheus alert rules** | `SLOErrorBudgetBurnCritical`, `SLOErrorBudgetBurnWarning`, `SLOErrorBudgetExhausted` |
| **Grafana SLO Overview** | Dashboard trực quan error budget remaining, burn rate trend |
| **ArgoCD** | Tự động chặn sync khi error budget < 5% (policy hook) |
| **Slack bot** | Thông báo deploy gate status mỗi buổi sáng lúc 8:00 |
| **PagerDuty** | Alert SLO critical → page on-call SRE trong 5 phút |

### 6.1 Prometheus Recording Rules (Ví dụ)

```yaml
# Burn rate cho cửa sổ 1 giờ (dùng trong alert critical)
- record: job:slo_errors_1h:rate
  expr: rate(http_requests_total{code=~"5.."}[1h])
- record: job:slo_requests_1h:rate
  expr: rate(http_requests_total[1h])
- record: job:slo_error_ratio_1h
  expr: job:slo_errors_1h:rate / job:slo_requests_1h:rate
- record: job:slo_burn_rate_1h
  expr: job:slo_error_ratio_1h / (1 - 0.999)
```
