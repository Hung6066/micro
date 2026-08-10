# Migration mật khẩu admin Identity (production)

Mật khẩu bootstrap admin được lưu ngoài Git trong `D:\secure\his-hope\admin-user` và được đưa vào Vault tại:

`secret/his-hope/identity-service/bootstrap-admin` → key `password`

Deployment chỉ đọc Secret Kubernetes `identity-bootstrap-admin`. Cờ reset mặc định là `false`; không bật cờ này trong manifest production lâu dài.

## Quy trình one-time

1. Cập nhật password vào Vault bằng tài khoản quản trị được bảo vệ, không in giá trị ra log.
2. Đồng bộ Secret `identity-bootstrap-admin` từ nguồn secret được phê duyệt.
3. Triển khai image Identity đã build và kiểm tra digest trên node.
4. Tạm thời đặt:

   ```text
   Persistence__RunMigrationsOnStartup=true
   Identity__BootstrapAdmin__ResetPassword=true
   ```

5. Chờ rollout, kiểm tra log chỉ với thông báo thành công, rồi đặt cả hai cờ về `false` và rollout lần nữa.
6. Kiểm thử đăng nhập bằng session HTTP; kết quả hợp lệ là redirect `/`, không phải `invalid_credentials`.

Không được ghi password, cookie, JWT hoặc token Vault vào log/terminal. Cơ chế reset dùng `UserManager.GeneratePasswordResetTokenAsync` và `ResetPasswordAsync`, nên không sửa trực tiếp hash trong database.

## Trạng thái runtime đã xác nhận

- Identity deployment: `1/1` ready.
- EF Core migration: completed.
- Secret Kubernetes khớp file admin bảo mật.
- Login qua VIP `172.16.102.100` với Host `admin.his-hope.local`: redirect `/`.

## Ghi chú Vault CSI

`SecretProviderClass identity-service-secrets` đã khai báo object bootstrap admin. Runtime hiện dùng projected Secret vì Vault CSI Kubernetes auth của Identity đang trả lỗi `failed to login`; cần sửa role/audience của Vault trước khi bật lại CSI mount. Trong thời gian này, Secret phải được đồng bộ bằng quy trình quản trị đã kiểm soát, không commit dữ liệu secret vào repository.
