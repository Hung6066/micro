# CNPG backup strategy: Azure primary + MinIO local secondary

## Quyết định triển khai

- Azure Blob là object store chính cho physical backup và WAL archive của `spire-postgres`.
- MinIO trong namespace `backup` được giữ lại làm hạ tầng backup local để phục vụ restore nhanh và pipeline secondary sau khi được kiểm thử.
- Không cấu hình hai đích WAL trong cùng một plugin stream. Cluster chỉ trỏ `barmanObjectName` tới Azure.

## Vì sao không giả lập secondary

CloudNativePG/Barman Cloud dùng một `ObjectStore` cho luồng WAL của cluster. Việc tạo thêm ObjectStore MinIO không tự động nhân đôi WAL/backup. MinIO chỉ được gọi là secondary sau khi có pipeline copy/backup riêng và đã pass restore test.

## Cấu hình Azure

Overlay production dùng:

- `k8s/production-ha/cnpg-barman-object-store-azure.yaml`
- `k8s/production-ha/spire-postgres-cluster-azure-patch.yaml`
- `k8s/overlays/prod-spire-azure/kustomization.yaml`

Secret runtime `spire-postgres-azure-backup-credentials` được tạo bởi script bootstrap từ `D:\secure\his-hope\azure-production.env`; không commit secret vào Git.

Vault auto-unseal dùng cùng Azure service principal metadata nhưng tách mục đích khỏi backup. Script `scripts/bootstrap-vault-azure-unseal.ps1` đọc client secret từ `D:\secure\his-hope\azure_client_secret`, tạo Secret `vault-azure-unseal` trong namespace `his-hope` và apply Vault manifest. Không dùng SAS token cho Vault seal.

## Go-live gates

1. Apply overlay vào đúng production context.
2. Xác nhận ObjectStore `spire-postgres-azure-store` phase `Ready`.
3. Xác nhận cluster có `barmanObjectName` là `spire-postgres-azure-store`.
4. Tạo một `Backup` on-demand và chờ `Completed`.
5. Kiểm tra blob `wals/` và `base/` trên Azure.
6. Restore vào namespace/cluster cô lập.
7. Chỉ sau khi các bước trên đạt mới triển khai pipeline MinIO secondary.
