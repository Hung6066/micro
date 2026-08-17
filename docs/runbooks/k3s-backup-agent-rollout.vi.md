# Rollout backup agent K3s qua GitHub Actions

Workflow: `.github/workflows/k3s-backup-agent-rollout.yml`.

Workflow luôn chạy `ansible-playbook --check` trước. `apply=true` chỉ được
phép trong protected environment `production`, có change reference và mã xác
nhận `APPROVE-BACKUP-AGENTS`. Không chạy playbook trực tiếp từ máy cá nhân để
tránh bỏ qua audit trail.

## Secrets cần cấu hình trong environment `production`

- `ANSIBLE_SSH_PRIVATE_KEY`: private key dùng đăng nhập các host trong
  `ansible/enterprise-k3s/inventory/production.yml`.
- `ANSIBLE_VAULT_PASSWORD`: password giải mã
  `ansible/enterprise-k3s/group_vars/vault.yml`.

Lưu ý nguồn sự thật: workflow này đọc `vault_backup_sas_token` từ
`ansible/enterprise-k3s/group_vars/vault.yml`, không tự đọc
`D:\secure\his-hope\azure-production.env`. Khi xoay SAS, phải cập nhật biến
trong Ansible Vault bằng `ansible-vault edit` (hoặc quy trình rotation đã được
phê duyệt), sau đó chạy `apply=false` để kiểm tra trước khi apply. Chỉ sửa file
secure local không làm thay đổi secret mà workflow sẽ deploy.

Workflow production cũng bắt buộc secret `AZURE_PRODUCTION_ENV_B64` và đối
chiếu account, container, endpoint và SAS trong env này với nguồn Ansible
(endpoint/container nằm trong `group_vars/all.yml`, SAS nằm trong Vault) trong
runner tạm thời. Nếu hai nguồn lệch, workflow dừng trước Ansible
syntax/check/apply; không log giá trị hoặc hash của secret.

Blob preflight chỉ chấp nhận SAS container-scoped có đủ quyền `r,a,c,w,l` để
đọc/list và ghi backup, có trường hết hạn `se` hợp lệ còn trong tương lai;
preflight không upload thử dữ liệu.

Giá trị chỉ được materialize trong runner tạm thời, mode `0600`, và bị xóa ở
cleanup step. Không commit key, password hoặc file `backup.env`.

## Trình tự vận hành

1. Chạy `apply=false` với change reference để kiểm tra syntax và diff.
2. Review output, đảm bảo Azure SAS/identity trong Ansible Vault đã được cấp
   đúng quyền ghi prefix backup.
3. Mở change approval cho environment `production`.
4. Chạy lại với `apply=true` và `approval_code=APPROVE-BACKUP-AGENTS`.
5. Kiểm tra artifact `k3s-backup-agents-<run-id>`; evidence chỉ chứa trạng
   thái timer/service, host count và thời gian, không chứa secret.
6. Chỉ sau khi artifact có đủ 3 host `SUCCESS` mới chạy restore drill và cập
   nhật `azure-backup` gate.

`his-hope-k3s-etcd-snapshot.service` là `Type=oneshot`: sau khi chạy thành
công, trạng thái cần kiểm tra là `systemctl show ... Result=success`, không
phải `systemctl is-active` của service. Workflow đã tự chạy một snapshot smoke
trong nhánh `apply=true` trước khi kiểm tra tuổi snapshot.

Snapshot runtime ngày 2026-08-10 cho thấy timer trên cả ba server đang active
nhưng service snapshot đều failed (exit 1). Không restart lặp hoặc bỏ qua gate;
hãy sửa `vault_backup_sas_token` trong Ansible Vault, chạy lại check mode, rồi
apply qua workflow có approval.

Template `backup.env.j2` phải giữ SAS trong double quotes (`to_json`). Nếu bỏ
quote, systemd có thể cắt token tại các ký tự `&`/`=` và service sẽ fail dù
file nguồn chứa token đúng.

Check mode đã được xác nhận trên cả ba host (`unreachable=0`, `failed=0`); nó
chỉ dự báo thay đổi file/service và không thay đổi máy chủ.

Workflow này không thay thế CNPG restore, Vault recovery, Harbor clean-node,
Longhorn snapshot hoặc application restore evidence; các gate đó vẫn phải
được chạy độc lập.
