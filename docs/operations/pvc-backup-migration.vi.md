# PVC/Kubernetes backup và migration gate

`local-path` chỉ lưu dữ liệu trên node local; nó không cung cấp snapshot/replication khi mất node hoặc mất cả cluster. Vì vậy không được đánh dấu PVC production-ready chỉ bằng việc tạo Velero object store.

## Lộ trình

1. Chọn storage có CSI snapshot/replication: Longhorn, Ceph hoặc Azure Disk CSI.
2. Tạo `VolumeSnapshotClass` và kiểm tra snapshot/restore trên một PVC thử nghiệm.
3. Cài Velero Azure provider với `k8s/backup/velero-azure-values.yaml`.
4. Bật node-agent filesystem backup cho PVC chưa hỗ trợ CSI.
5. Tạo backup resource-only và volume backup tách biệt.
6. Restore vào namespace cô lập, kiểm tra checksum và ứng dụng đọc được dữ liệu.
7. Chỉ sau đó migrate workload khỏi `local-path`.

## Observability hiện tại

Cluster hiện có Prometheus, Grafana, Alertmanager, Loki và Jaeger trong namespace `monitoring`; các PVC đều dùng `local-path` (Prometheus 10Gi, Loki 10Gi, Jaeger 5Gi, Grafana 2Gi). Vì vậy:

- ConfigMap dashboards/rules/config được export bằng `scripts/export-observability-config-to-azure.sh`.
- Grafana dashboard có thể tái tạo từ ConfigMap/Git, nhưng Grafana users/preferences cần PVC backup.
- Prometheus TSDB, Loki logs và Jaeger traces chỉ được coi là durable sau khi CSI/Velero restore test đạt.
- Nếu observability chỉ dùng cho troubleshooting ngắn hạn, có thể giữ retention ngắn; nếu dùng compliance/audit, phải bật off-site retention riêng.

## Không làm

- Không backup Kubernetes Secret plaintext vào Azure.
- Không xóa PVC cũ trước khi restore test đạt.
- Không coi Velero `Completed` là bằng chứng dữ liệu ứng dụng khôi phục được.
