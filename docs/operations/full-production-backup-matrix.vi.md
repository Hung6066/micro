# Ma trận backup production

| Thành phần | Azure/off-site | MinIO/local | Cách backup | Restore gate | Trạng thái hiện tại |
|---|---|---|---|---|---|
| PostgreSQL `spire-postgres` | CNPG Barman Cloud, WAL + base, 30 ngày | Hạ tầng giữ lại cho restore nhanh/pipeline phụ | `Backup`/`ScheduledBackup` plugin | Restore cluster cô lập + PITR | Azure cấu hình, chưa apply production |
| K3s embedded etcd | Snapshot theo node server, prefix `k3s/` | Có thể giữ bản snapshot gần nhất | `k3s etcd-snapshot save` + AzCopy | Khôi phục control-plane cô lập | Script đã tạo, chưa chạy host |
| Kubernetes manifests/config | Archive không chứa Secret plaintext | Bản local tùy chọn | Export resources + repo Git | Re-apply vào cluster cô lập | Script đã tạo, chưa chạy cluster |
| PVC/workloads | Velero Azure hoặc CSI snapshot | Cache local tùy chọn | Velero + CSI/filesystem backup | Restore PVC vào namespace cô lập | Chưa đạt vì `local-path` |
| Vault | Snapshot Raft hoặc backend-native | Bản local mã hóa tùy chọn | Vault snapshot + AzCopy | Restore Vault cô lập | Script đã tạo, chưa chạy |
| Harbor | Database + registry artifacts/config | Bản local tùy chọn | Harbor-supported backup workflow | Restore Harbor cô lập | Cần xác nhận topology/storage |
| Redis | RDB/AOF | Bản local tùy chọn | `redis-cli --rdb` + AzCopy | Restore Redis cô lập + checksum | Script đã tạo, chưa chạy |
| Observability config | Grafana dashboards, Prometheus rules/config, Alertmanager, Loki/Jaeger manifests | PVC local hiện tại | Export config không chứa Secret + Velero/PVC backup | Restore config và data vào namespace cô lập | Config export script đã tạo; PVC chưa đạt |
| Observability data | Prometheus TSDB, Loki logs, Jaeger traces | Local cache ngắn hạn | CSI/Velero hoặc backend object storage | Query dữ liệu sau restore | Chưa đạt vì `local-path` |
| Secrets/certificates | Vault/secret manager, không export plaintext | Không dùng MinIO làm source | Backup Vault seal/unseal material theo policy | Key recovery drill | Chưa đạt |

## Điều kiện trước khi deploy host

Không đánh dấu production-ready nếu thiếu một trong các bằng chứng sau:

1. K3s snapshot mới nhất upload thành công và restore thử được.
2. PostgreSQL backup `Completed`, có `wals/` và `base/`, restore thử được.
3. PVC strategy không còn phụ thuộc duy nhất vào `local-path`.
4. Vault, Harbor và Redis có artifact mới, checksum và restore test.
5. Kube-context production reachable và backup verifier trả `PASS`.
