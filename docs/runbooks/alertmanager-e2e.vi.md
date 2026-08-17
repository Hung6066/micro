# Alertmanager notification E2E

## Mục đích

Probe này kiểm tra toàn tuyến: production Alertmanager nhận alert critical,
route tới receiver thật, receiver ghi nhận `firing`, sau đó ghi nhận
`resolved` với cùng correlation id. Đây không phải là kiểm tra ConfigMap.

## Protected secrets

Tạo trong GitHub Environment `production`:

- `ALERTMANAGER_PRODUCTION_URL`: URL HTTPS của Alertmanager API (`/api/v2`).
- `ALERTMANAGER_E2E_RECEIVER_URL`: HTTPS endpoint của receiver kiểm thử, chỉ
  cho phép truy vấn bằng correlation id.
- `ALERTMANAGER_PRODUCTION_BEARER_TOKEN`: token read-only/API phù hợp (nếu
  Alertmanager ingress yêu cầu xác thực).

Receiver phải trả JSON khi gọi `GET <url>?e2e_id=<id>`:

```json
{"received":true,"e2e_id":"his-hope-alertmanager-e2e-...","status":"firing"}
```

Sau notification resolved, cùng endpoint phải trả `status: "resolved"`.
Không ghi token, webhook hoặc nội dung bệnh nhân vào response/log.

## Chạy

1. Chạy workflow `Alertmanager Notification E2E` với `run_test=false` để kiểm
   tra cấu hình mà không gửi alert.
2. Sau khi receiver được kiểm thử, chạy với `run_test=true` qua Environment
   protection/reviewer.
3. Lưu artifact `alertmanager-notification-e2e-<run_id>` làm evidence; không
   tạo file evidence thủ công.

Production go-live workflow cũng gọi probe và fail-closed nếu thiếu receiver,
Alertmanager không nhận alert, hoặc thiếu notification `resolved`.
