# Vault Compose persistent mode

Compose local đã chuyển Vault khỏi `vault server -dev` sang file storage tại volume `vault_data`.

## Kiểm tra trạng thái

```powershell
docker exec -e VAULT_ADDR=http://127.0.0.1:8200 -e VAULT_TOKEN=root his-hope-vault vault status -format=json
docker exec -e VAULT_ADDR=http://127.0.0.1:8200 -e VAULT_TOKEN=root his-hope-vault vault read transit/keys/his-hope-backup-encryption
```

Kết quả yêu cầu: `initialized=true`, `sealed=false`, `storage_type=file`.

## Restart và unseal local

Persistent Vault vẫn seal khi process restart. Trong Compose local, chạy bootstrap idempotent:

```powershell
docker compose -f docker/docker-compose.yml restart vault
docker compose -f docker/docker-compose.yml run --rm --no-deps vault-init
```

Không chạy `docker compose down -v`; lệnh đó xóa volume và dữ liệu Vault.

## Kiểm tra rotation

Tạo ciphertext trước rotation, rotate transit key, sau đó decrypt ciphertext cũ và mới. Ciphertext cũ phải tiếp tục decrypt được khi `min_decryption_version` chưa nâng.

## Production boundary

Cấu hình Compose này chỉ dành cho local/test: single-node file storage, HTTP nội bộ và bootstrap token dev. Production phải dùng Vault Raft/HA, auto-unseal KMS/HSM, TLS, workload identity/AppRole ngắn hạn, policy least privilege và audit device. Không dùng root token trong service production.
