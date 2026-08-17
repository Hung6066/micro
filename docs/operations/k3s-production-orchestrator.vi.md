# K3s production orchestrator

Runner duy nhất cho workflow hạ tầng là:

```powershell
pwsh -NoProfile -File .\scripts\run-k3s-production.ps1 `
  -Inventory .\ansible\enterprise-k3s\inventory\production.yml `
  -ValidationOnly
```

Runner kiểm tra WSL/Ansible, inventory, encrypted Vault file và Azure env
file mà không kết nối hoặc mutate host. Chỉ sau change approval mới chạy
không có `-ValidationOnly`; runner hỏi Vault/sudo password một lần, ghi
password file tạm ACL hạn chế, rồi xóa trong `finally`.

Phase order cố định (có thể chọn một dải liên tục bằng `-FromPhase` và
`-ToPhase`; tên hợp lệ: `preflight`, `load-balancer`, `control-plane`,
`verify`, `workers`, `backup`):

1. preflight và host security;
2. HAProxy/Keepalived và API VIP `172.16.102.100:6443`;
3. K3s control-plane;
4. verify cluster;
5. K3s workers;
6. backup agents/Azure Blob.

Evidence redacted nằm dưới `artifacts/k3s-production/run-*/summary.json` và
`ansible.log`. Khi chạy một dải phase, `requestedPhases` trong evidence cho
biết chính xác các phase đã yêu cầu. Trạng thái `PASS` chỉ có nghĩa workflow đã hoàn thành; `FAIL`
là lỗi thực thi; `BLOCKED` là thiếu prerequisite/credential. Không dùng
phase ngoài dải đã chọn; phase selector chỉ truyền các tag tương ứng cho
Ansible.

Đây không thay thế các gate runtime của application, Argo CD, Pod Security,
backup/restore hoặc production go-live aggregate.

Trước gate restore, dùng `scripts/verify-production-backup-restore.ps1` để
kiểm tra blob Azure và gọi CNPG drill trong namespace cô lập. Chế độ mặc định
chỉ dry-run; apply production phải đi qua workflow được bảo vệ với
`-Apply -AllowProduction` và evidence đo được.
