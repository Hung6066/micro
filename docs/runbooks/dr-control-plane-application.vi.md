# DR drill: control-plane và application restore

Hai workflow này mặc định dry-run và chỉ tạo evidence `pass` sau khi thao tác
thật hoàn tất:

- `K3s Control-Plane Rebuild DR Drill`: dùng snapshot embedded-etcd đã chọn,
  chạy Ansible playbook serial 1, kiểm tra `/readyz` và toàn bộ node `Ready`.
  Production yêu cầu `apply=true`, Environment approval, SSH key/Vault
  password protected và `-AllowProduction`.
- `Application Restore Smoke DR Drill`: enqueue restore job qua Database
  Continuity API, chờ `Completed`, kiểm tra application `/health`, OIDC
  discovery, protected API trả 401/403 khi không có token, authenticated API
  trả 2xx và Deployment replicas available.

## Evidence và an toàn

Không tạo JSON evidence thủ công. Mỗi drill ghi `rpoMinutes`, `rtoMinutes`,
`restoreVerified`, target và timestamp; không ghi token/password/kubeconfig.
Chỉ chạy production trong change window đã có backup, rollback owner và
approval. `WhatIf`/`apply=false` không được tính là restore evidence.

## Protected inputs

- Control-plane: `KUBECONFIG_*_B64`, `ANSIBLE_SSH_PRIVATE_KEY`,
  `ANSIBLE_VAULT_PASSWORD`, snapshot path trên host.
- Application: `KUBECONFIG_*_B64`, `APP_RESTORE_BEARER_TOKEN`, continuity URL,
  application URL và OIDC discovery URL.
