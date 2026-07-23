# His.Hope — Hướng Dẫn Hoạch Định Năng Lực (Capacity Planning)

Tài liệu vận hành nội bộ. Cập nhật hàng tháng bởi nhóm DevOps.

---

## 1. Chỉ Số Theo Dõi Hàng Tuần

| Chỉ số | Mô tả | Nguồn dữ liệu |
|--------|-------|---------------|
| CPU trung bình/P99 | Mức sử dụng CPU mỗi service | Grafana + Prometheus |
| Memory trung bình/P99 | Mức sử dụng RAM mỗi service | Grafana + Prometheus |
| Request rate (req/s) | Lưu lượng request mỗi service | Linkerd dashboard |
| DB storage growth (GB/tuần) | Tốc độ tăng trưởng dung lượng DB | CockroachDB Admin UI |
| Queue depth | Số message tồn đọng trong RabbitMQ | RabbitMQ Management |
| Số pods mỗi service | Số lượng pod thực tế đang chạy | `kubectl get pods -n his-hope` |
| Node pool utilization | % tài nguyên đã dùng trên tổng node pool | GKE Console |

## 2. Phương Pháp Dự Báo

### 2.1 Prophet ML Model (Tự động)

- **CronJob**: `his-hope/capacity-forecast` chạy mỗi 30 phút
- **Đầu vào**: 14 ngày dữ liệu Prometheus gần nhất
- **Đầu ra**: Dự báo 60 phút tiếp theo, ghi vào Grafana annotations
- **Cảnh báo**: Nếu dự báo vượt ngưỡng → gửi Slack `#his-hope-ops-alerts`

### 2.2 Grafana Dashboard

- **Dashboard**: "His.Hope Capacity Planning"
- **Khoảng thời gian**: 30 ngày mặc định
- **Panel chính**:
  - CPU & Memory usage (7 service panels)
  - Request rate & latency (P50/P95/P99)
  - DB storage projection (linear regression)
  - KEDA scaling events timeline
  - Node pool headroom gauge

### 2.3 Kiểm Tra Thủ Công (Hàng Tháng)

```sql
SELECT pg_size_pretty(sum(size))
FROM crdb_internal.table_sizes
WHERE database_name = current_database();
```

Chạy trên từng database: `patient`, `identity`, `appointment`, `clinical`, `lab`, `billing`, `pharmacy`.

## 3. Ngưỡng Scale Cho 7 Service

| Service | Trigger Scale Up | Trigger Scale Down | KEDA Scaler |
|---------|-----------------|--------------------|-------------|
| **patient-service** | CPU > 70% HOẶC queue > 100 | CPU < 30% VÀ queue < 10 | CPU + RabbitMQ |
| **identity-service** | Auth failures > 1/s | Auth failures = 0 trong 5 phút | Prometheus |
| **appointment-service** | CPU > 70% VÀ request > 500/s | CPU < 30% VÀ request < 100/s | CPU + Prometheus |
| **clinical-service** | CPU > 60% (EMR nặng CPU) | CPU < 25% | CPU |
| **lab-service** | Queue depth > 50 | Queue depth < 5 | RabbitMQ |
| **billing-service** | Pending invoices > 1000 | Pending invoices < 100 | RabbitMQ |
| **pharmacy-service** | CPU > 70% | CPU < 30% | CPU |

**Lưu ý**: KEDA `maxReplicas` mặc định = 10. Cần review nếu thường xuyên chạm trần.

## 4. Checklist Đánh Giá Hàng Tháng

- [ ] Kiểm tra Grafana "His.Hope Capacity Planning" — có service nào gần ngưỡng?
- [ ] Review Prophet forecast 30 ngày — xu hướng tăng bất thường?
- [ ] Kiểm tra DB storage: tăng > 10GB/tuần? → Lên kế hoạch mở rộng disk
- [ ] Review node pool: thêm > 1 node/tháng? → Mở GKE support ticket tăng quota
- [ ] Kiểm tra KEDA scale history: chạm `maxReplicas`? → Tăng giới hạn
- [ ] Review SLO error budget: có tương quan với capacity không?
- [ ] Cập nhật Capacity Spreadsheet (Google Sheets: `His.Hope/Capacity-Plan`)

## 5. Mẫu Dự Báo Tăng Trưởng

| Tháng | Bệnh nhân | Nhà cung cấp | Lượt khám/ngày | API calls/ngày | Storage (GB) | Nodes cần |
|-------|-----------|-------------|----------------|----------------|-------------|-----------|
| Hiện tại | [N] | [N] | [N] | [N] | [N] | [N] |
| +3 tháng | [N+10%] | [N+5%] | [N+10%] | [N+15%] | [N+10%] | [N] |
| +6 tháng | [N+21%] | [N+10%] | [N+21%] | [N+32%] | [N+21%] | [N+1] |
| +12 tháng | [N+46%] | [N+22%] | [N+46%] | [N+75%] | [N+46%] | [N+2] |

**Công thức giả định**: Bệnh nhân tăng ~10%/quý, API calls tăng ~15%/quý (do tính năng mới).

## 6. Tối Ưu Chi Phí

### 6.1 Compute

- **Spot instances**: Dùng cho batch workload không critical (outbox processor, report generator, ML forecast jobs). Tiết kiệm 50-70%.
- **Resource requests = 50% limits**: Ví dụ `limits.cpu=1000m` → `requests.cpu=500m`. Tránh lãng phí tài nguyên dự phòng.
- **Right-size hàng quý**: So sánh `kubectl top` thực tế với `resources.requests`. Điều chỉnh nếu chênh > 30%.

### 6.2 KEDA Scaling

- **Scale to zero** cho batch services:
  - `outbox-processor`: scale về 0 khi queue trống > 15 phút
  - `report-generator`: scale về 0 khi không có job pending
- Cấu hình `minReplicas=0`, `idleReplicaCount=0`

### 6.3 Database

- **CockroachDB Serverless** cho môi trường dev/staging — trả phí theo query-hour, không cần node dedicated.
- Production: dùng CockroachDB Dedicated, chọn node size dựa trên storage forecast.

### 6.4 Theo Dõi Chi Phí

- **GKE Cost Allocation**: Gán label `app.kubernetes.io/name` cho mọi workload
- **Báo cáo hàng tháng**: Xuất GCP Billing → BigQuery → Grafana panel "His.Hope Cost"
- **Ngưỡng cảnh báo**: Chi phí vượt budget tháng > 10% → Slack `#his-hope-ops-alerts`

---

*Cập nhật lần cuối: Tháng 7/2026 — Phiên bản 1.0*
