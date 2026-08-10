# Cấu hình E2E cho môi trường

Playwright không còn cố định app URL trong test. Các biến sau được dùng theo thứ tự:

```text
E2E_CLINICAL_URL   (mặc định http://localhost:8081)
E2E_DASHBOARD_URL  (mặc định http://localhost:8082)
E2E_ADMIN_URL      (mặc định http://localhost:8083)
```

CI phải đặt ba repository variables trỏ đến cùng một môi trường đã được deploy và đặt:

- `E2E_AUTH_PROBE_URL` trong protected secrets;
- `E2E_AUTH_TOKEN` trong protected secrets;
- `E2E_AUTH_REQUIRED=true` cho test authenticated.

`verify-e2e-prerequisites.ps1` kiểm tra HTTP cả ba app và probe xác thực trước khi Playwright chạy. Nếu URL không reachable, gate phải fail rõ ràng; không đổi sang localhost để che giấu lỗi môi trường.
