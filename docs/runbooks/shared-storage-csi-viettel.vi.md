# Shared CSI storage cho K3s trên Viettel IDC

## Kết luận hiện trạng

Cluster production hiện chỉ có `local-path` và `secrets-store.csi.k8s.io`.
`local-path` dùng filesystem của từng node, không phải storage dùng chung và
không cung cấp replication/restore cho PVC. Longhorn cũng không phù hợp khi
không còn disk riêng cho từng VM: Longhorn cần block device/local path trên
mỗi node để tạo replica, không thể dùng RAM hoặc Azure Blob làm disk PVC.

## Phương án đúng

Platform owner cần cung cấp một CSI storage dùng chung từ Viettel, chọn một
trong hai loại:

1. **vSphere/vSAN CSI** nếu Viettel expose datastore/storage policy cho tenant:
   vCenter endpoint, cluster ID, storage policy/datastore, TLS CA và credential
   của CSI controller.
2. **NFS CSI/RWX** nếu Viettel cấp NAS export: địa chỉ NFS, export path, NFS
   version, network ACL từ cả năm node và chính sách snapshot/backup.

Azure Blob chỉ dùng cho backup/object storage; không dùng trực tiếp làm PVC.

### Có thể dùng `local-path` không?

Có, nhưng chỉ cho dev/staging hoặc dữ liệu tạm/cache. `local-path` ghi thẳng
vào filesystem của node đang chạy pod; PVC không tự failover sang node khác,
reclaim mặc định là `Delete`, và mất node có thể mất dữ liệu. Không dùng
`local-path` cho PostgreSQL, Vault Raft, Redis/RabbitMQ, Harbor, MinIO,
Prometheus/Loki/Jaeger hoặc backup production. Không gọi `local-path` là
shared storage và không dùng nó làm backend cho Longhorn.

Argo CD controller/repo-server có thể dùng `emptyDir`/cache ephemeral trong
profile nhỏ vì desired state nằm ở Git và Kubernetes API. Đây là ngoại lệ cho
Argo, không áp dụng cho PVC dữ liệu của ứng dụng.

## Argo CD

Argo CD không cần Longhorn để giữ Git desired state; state chính nằm ở
Kubernetes API và Git. Redis/repository cache có thể chạy ephemeral trong
profile single-replica. Nếu yêu cầu HA/cache persistence, dùng cùng CSI shared
storage hoặc managed Redis/PostgreSQL được Viettel vận hành. Không tạo disk
Longhorn chỉ để phục vụ Argo CD.

## Gate bắt buộc trước khi đổi production

```powershell
$env:KUBECONFIG='D:\AI\micro\artifacts\kubeconfig-production.yaml'
pwsh scripts/validate-shared-storage-contract.ps1 `
  -Kubeconfig $env:KUBECONFIG `
  -StorageClassName '<Viettel-CSI-StorageClass>' `
  -RequireSnapshotClass `
  -RequireApprovalAnnotation `
  -OutputPath artifacts/evidence/shared-storage-contract.json
```

Có thể chạy cùng gate qua workflow được bảo vệ
`.github/workflows/shared-storage-csi-gate.yml`; workflow chỉ đọc
StorageClass/VolumeSnapshotClass, không cài CSI, không tạo disk và không
apply PVC.

Gate phải chứng minh provisioner là CSI external, không phải `local-path` hoặc
Longhorn, có expansion, VolumeSnapshotClass tương ứng và approval annotation.
Sau đó mới tạo overlay production trỏ PVC sang StorageClass này; PVC đang
`Bound` không được patch trực tiếp. Phải tạo PVC mới, copy/restore dữ liệu,
kiểm thử cô lập rồi mới cutover.

Overlay đã chuẩn bị sẵn nhưng không được sync trước khi StorageClass tồn tại:

```powershell
kubectl kustomize k8s/overlays/prod-shared-storage --load-restrictor LoadRestrictionsNone |
  Out-File artifacts/evidence/prod-shared-storage-render.yaml -Encoding utf8
kubectl kustomize k8s/observability/overlays/prod-shared-storage --load-restrictor LoadRestrictionsNone |
  Out-File artifacts/evidence/observability-shared-storage-render.yaml -Encoding utf8
```

Hai overlay chỉ đổi PVC sang `viettel-shared`; chúng không cài CSI, không tạo
disk và không di chuyển dữ liệu. Tên class phải được thay đúng tên Viettel đã
phê duyệt trước khi mở promotion PR.

Go-live gate nhận tên class đã review:

```powershell
pwsh scripts/validate-k3s-go-live.ps1 `
  -Environment production `
  -StorageClassName '<Viettel-CSI-StorageClass>' `
  -Kubeconfig artifacts/kubeconfig-production.yaml `
  -RequireCluster
```

Nếu class chưa tồn tại, PVC còn `local-path`, hoặc chưa migration/restore,
gate vẫn fail/unavailable; không được dùng Azure Blob endpoint làm tên class.

## Thông tin cần gửi để triển khai

- Tên StorageClass và CSI provisioner chính thức.
- Nếu vSAN: vCenter/cluster/storage-policy/CA/credential workflow.
- Nếu NFS: NFS server/export/version/ACL.
- Tên VolumeSnapshotClass và retention policy.
- RPO/RTO, quota dùng chung và approval của storage owner.
