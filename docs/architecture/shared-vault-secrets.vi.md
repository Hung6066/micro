# Shared Vault secrets platform

Các service ASP.NET dùng `His.Hope.Secrets` gián tiếp qua `AddHisHopeServiceDefaults`. Abstraction chung là `IVaultTransitClient`, gồm kiểm tra health/key version và `EncryptAsync`/`DecryptAsync`; service chỉ truyền tên key được cấp policy. Không service nào được log token hoặc plaintext PHI.

## Quy ước cấu hình

```text
Vault__Address=https://vault.example.internal
Vault__Token=<inject-at-runtime-or-use-Vault-Agent>
Vault__TransitMount=transit
Vault__RequireVault=true
```

Compose hiện có Vault dev mode và key `his-hope-backup-encryption` để kiểm thử. Production phải thay bằng Vault HA + TLS, AppRole/Kubernetes auth hoặc Vault Agent; không dùng `VAULT_DEV_ROOT_TOKEN_ID=root`. Mỗi service dùng policy riêng (ví dụ `transit/encrypt/his-hope-backup-encryption`), không cấp quyền operator/root.

Database Continuity dùng abstraction này để kiểm tra encryption readiness và expose metrics; Identity vẫn giữ các provider tương thích hiện có cho signing/client secret/MFA, nhưng đã được đăng ký cùng shared platform để các service mới không tự viết HTTP client Vault riêng.
