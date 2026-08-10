# External HA load balancer cho K3s production

VIP `172.16.102.100` đang thuộc external load balancer hiện hữu. Không cài Keepalived và không gán VIP này vào các node K3s.

## Pool Kubernetes API và supervisor

```text
Frontend: 172.16.102.100:6443 (TCP)
Backend:
  172.16.102.7:6443
  172.16.102.8:6443
  172.16.102.9:6443
Health check: TCP connect
```

K3s dùng TCP `6443` cho Kubernetes API và supervisor traffic trong cấu hình này. Các hostname ứng dụng hiện hữu trên port 80/443 không cần thay đổi.

## Kiểm thử trước bootstrap

Từ một máy quản trị hoặc worker:

```powershell
Test-NetConnection 172.16.102.100 -Port 6443
```

Kết quả phải thành công sau khi LB listener và backend đã được cấu hình. Chỉ sau đó mới bật `enterprise_network_controls_verified` và chạy playbook K3s.
