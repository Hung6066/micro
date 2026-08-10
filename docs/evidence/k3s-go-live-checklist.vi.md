# K3s go-live evidence checklist

Checklist này là bằng chứng vận hành, không phải tuyên bố rằng resource tồn tại là đã backup/restore thành công. Mỗi file JSON phải được tạo trong change window, không chứa token, password, kubeconfig, private key hoặc dữ liệu bệnh nhân. Mỗi bằng chứng phải có `status: pass`, `restoreVerified: true`, `target`, `rpoMinutes`, `rtoMinutes` và `executedAtUtc`.

## Bắt buộc trước production sync

| Evidence file | Nội dung tối thiểu | Trạng thái hiện tại |
|---|---|---|
| `database-restore-drill.json` | CNPG/PostgreSQL restore vào namespace cô lập, measured RPO/RTO, smoke test | Chưa có — unavailable |
| `vault-recovery-drill.json` | unseal/recovery và rotation test, chỉ ghi metadata không ghi key/token | Chưa có — unavailable |
| `harbor-clean-node-test.json` | clean node pull bằng digest, signature verification và registry health | Chưa có — unavailable |
| `control-plane-rebuild-drill.json` | rebuild control-plane từ infrastructure state, measured RTO | Chưa có — unavailable |
| `application-restore-smoke.json` | restore app/config, migration compatibility, OIDC và authorization smoke test | Chưa có — unavailable |
| `longhorn-snapshot-restore.json` | CSI snapshot/restore checksum trong namespace cô lập, measured RTO | Chưa có — storage gate blocked |

## Chạy validator

```powershell
$env:KUBECONFIG = 'D:\AI\micro\artifacts\kubeconfig-production.yaml'
pwsh scripts/validate-k3s-go-live.ps1 `
  -Environment production `
  -Kubeconfig $env:KUBECONFIG `
  -RequireCluster `
  -OutputPath artifacts/evidence/k3s-go-live-current.json
```

Validator fail-closed: `pass` chỉ được trả khi render, digest, secret scan, cluster health, Pod Security, Linkerd và toàn bộ restore evidence đều đạt. `unavailable` hoặc `environment-blocked` không được chuyển thành `pass`.

Kiểm tra riêng hợp đồng bằng chứng restore:

```powershell
pwsh scripts/validate-dr-evidence.ps1 `
  -EvidenceDirectory artifacts/evidence `
  -OutputPath artifacts/evidence/dr-evidence-contract.json
```
