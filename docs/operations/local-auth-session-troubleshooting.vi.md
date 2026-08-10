# Kiểm tra session trên VIP local

## Hiện tượng

Đăng nhập tại `admin.his-hope.local` thành công nhưng chuyển sang `dashboard.his-hope.local` hoặc `app.his-hope.local` lại yêu cầu đăng nhập.

## Nguyên nhân

Profile local chạy HTTP trên VIP `172.16.102.100`, nhưng cấu hình production mặc định dùng cookie domain `.his-hope.vn`. Trình duyệt không gửi cookie đó cho các host `*.his-hope.local`.

## Đã sửa

Overlay local đặt:

```text
Authentication__CookieDomain=.his-hope.local
```

Cookie `hishop_auth` hiện được chia sẻ giữa các host local. Kiểm thử runtime đã xác nhận:

- Login `admin.his-hope.local`: HTTP 302 đến `/`.
- Gọi `/api/v1/auth/me` trên `dashboard.his-hope.local` bằng cùng session: HTTP 200.

Identity và các BFF dùng cùng thư viện JWT. Secret Vault hiện được materialize
dưới dạng chứng thư PEM (`BEGIN CERTIFICATE`), vì vậy thư viện đã được bổ sung
khả năng đọc PEM certificate, PEM public key, DER và base64 DER. Nếu thiếu xử lý
này, `/session/exchange` có thể trả 204 nhưng các request tiếp theo phát sinh 500.

Dashboard BFF production phải trỏ tới service Prometheus thực tế của kube-prometheus-stack:

```text
http://kps-kube-prometheus-stack-prometheus.monitoring.svc.cluster.local:9090
```

Khi chưa đăng nhập, `/api/resources` trả `401` là đúng; `500` là lỗi cấu hình JWT
hoặc endpoint Prometheus, không phải lỗi quyền của người dùng.

## Cảnh báo COOP

`Cross-Origin-Opener-Policy` bị trình duyệt bỏ qua khi origin là HTTP không tin cậy. Đây là cảnh báo về transport, không phải lỗi xác thực. Production phải truy cập qua HTTPS với chứng thư hợp lệ trên HAProxy/appliance và cấu hình Traefik `websecure`; không dùng HTTP local profile cho dữ liệu thật.
