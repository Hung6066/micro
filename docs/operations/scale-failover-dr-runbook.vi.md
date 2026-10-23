# Runbook kiểm chứng scale, failover và DR

## Load tenant/outbox

Chạy trên môi trường staging đã seed dữ liệu đại diện; không dùng token mẫu:

```powershell
$env:BASE_URL = 'https://staging.example'
$env:AUTH_TOKEN = '<inject-from-secret-store>'
$env:TENANT_KEY = 'manufacturing-scale-fixture'
$env:LOAD_TARGET = '500'
$env:LOAD_DURATION = '15m'
k6 run tests/Load/k6/manufacturing-scale.js
```

Kết quả chỉ đạt khi `http_req_failed < 1%`, p95 < 750 ms và p99 < 1500 ms.
Đính kèm thêm số lượng outbox pending theo tenant trước/sau test và không ghi
token vào artifact.

Baseline service-level có thể chạy bằng `scripts/run-authenticated-load-baseline.ps1`;
script bắt buộc `AUTH_TOKEN` và từ chối tạo baseline anonymous.

## Redis/RabbitMQ/PostgreSQL failover

Local Compose drill có thể chạy bằng:

```powershell
pwsh -NoProfile -File scripts/run-compose-dependency-failover-drill.ps1
pwsh -NoProfile -File scripts/validate-compose-failover-artifact.ps1
```

Validator áp dụng SLO phục hồi mặc định: Redis `30s`, RabbitMQ `60s`, PostgreSQL `120s` (và vẫn tôn trọng `-MaxRecoverySeconds`). Artifact phải có cả thời gian dependency healthy và thời gian các HTTP health probe phục hồi.

Artifact hiện tại (`artifacts/runtime/compose-dependency-failover.json`) ghi nhận
Redis 13.58s, RabbitMQ 36.22s và PostgreSQL 70.26s phục hồi; cả bốn HTTP health
probe giữ trạng thái thành công trong lúc đo. Đây là bằng chứng
restart/recovery single-node, không phải HA failover hoặc business SLO.

- Dùng topology HA thật (không dùng Docker Compose single-node).
- Dừng leader hoặc cắt network theo từng dependency, giữ traffic read/write đại
  diện và ghi lại thời điểm lỗi, thời điểm phục hồi, error rate, p95/p99 và backlog.
- Gate SLO phải kiểm tra cả business request và readiness; port mở không được coi
  là dependency healthy.

## DR/multi-region

- Chụp mốc RPO trước drill.
- Chuyển traffic sang region phụ, khôi phục database/event transport và chạy
  authenticated smoke qua từng BFF chính.
- Đo RTO, RPO, event duplication/loss và tenant isolation trước khi quay lại.

Các drill local trong repo chỉ là evidence chuẩn bị; không thay thế failover
production-like hoặc external security assessment.
